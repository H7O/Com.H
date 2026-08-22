using System.Diagnostics;
using System.Runtime.InteropServices;
using Com.H.Shell;

namespace Com.H.Tests;

/// <summary>
/// Builds the small shell scripts these tests drive, in whichever dialect the host OS speaks.
/// </summary>
internal static class Sh
{
    internal static bool OnWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    internal static string Shell => OnWindows
        ? Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe"
        : "/bin/sh";

    private static string Args(string windows, string posix)
        => OnWindows ? $"/c {windows}" : $"-c \"{posix}\"";

    /// <summary>Writes to standard error and still exits successfully - what chromium does.</summary>
    internal static string SucceedsNoisily
        => Args("echo 290640 bytes written to file out.pdf 1>&2 & exit /b 0",
                "echo 290640 bytes written to file out.pdf 1>&2; exit 0");

    internal static string Exit(int code) => Args($"exit /b {code}", $"exit {code}");

    internal static string ExitNoisily(int code)
        => Args($"echo boom 1>&2 & exit /b {code}", $"echo boom 1>&2; exit {code}");

    internal static string Echo(string text) => Args($"echo {text}", $"echo {text}");

    internal static string Dump(string path) => Args($"type \"{path}\"", $"cat \"{path}\"");

    /// <summary>
    /// Runs for far longer than any test waits, holding the given file open through a
    /// redirection so a surviving child keeps the handle.
    /// </summary>
    internal static string RunLongHolding(string path)
        => Args($"ping -n 30 127.0.0.1 > \"{path}\"", $"sleep 30 > \"{path}\"");

    /// <summary>
    /// Deleting a file a killed child still owns fails on Windows, so this is how the tests
    /// tell a killed process tree from an orphaned one. Retries briefly because handle
    /// release is not instantaneous; a survivor would hold on for its full 30 seconds.
    /// </summary>
    internal static void AssertReleasedWithin(string path, int milliseconds = 3000)
    {
        var sw = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                File.Delete(path);
                return;
            }
            catch (IOException) when (sw.ElapsedMilliseconds < milliseconds)
            {
                Thread.Sleep(100);
            }
        }
    }
}

public class ShellExtTests
{
    #region success and failure are decided by the exit code

    [Fact]
    public void RunCommand_WroteToStandardErrorButExitedZero_DoesNotThrow()
    {
        // The regression this whole change exists for: chromium reports success on stderr,
        // so treating any stderr output as failure threw on every working conversion.
        var output = Sh.Shell.RunCommand(Sh.SucceedsNoisily);
        Assert.NotNull(output);
    }

    [Fact]
    public void RunCommand_NonZeroExit_ThrowsCarryingTheExitCode()
    {
        var ex = Assert.Throws<ShellCommandException>(() => Sh.Shell.RunCommand(Sh.Exit(21)));
        Assert.Equal(21, ex.ExitCode);
        Assert.Equal(21, ex.Result.ExitCode);
        Assert.False(ex.Result.TimedOut);
    }

    [Fact]
    public void RunCommand_NonZeroExit_ExceptionCarriesStandardError()
    {
        var ex = Assert.Throws<ShellCommandException>(() => Sh.Shell.RunCommand(Sh.ExitNoisily(5)));
        Assert.Equal(5, ex.ExitCode);
        Assert.Contains("boom", ex.Result.StandardError);
    }

    [Fact]
    public void RunCommand_ReturnsStandardOutput()
    {
        var output = Sh.Shell.RunCommand(Sh.Echo("hello"));
        Assert.Contains("hello", output);
    }

    [Fact]
    public async Task RunCommandAsync_ReturnsStandardOutput()
    {
        var output = await Sh.Shell.RunCommandAsync(Sh.Echo("hello"));
        Assert.Contains("hello", output);
    }

    [Fact]
    public void RunCommandWithResult_FailingCommand_ReportsInsteadOfThrowing()
    {
        var result = Sh.Shell.RunCommandWithResult(Sh.ExitNoisily(5));
        Assert.Equal(5, result.ExitCode);
        Assert.False(result.TimedOut);
        Assert.Contains("boom", result.StandardError);
    }

    [Fact]
    public void RunCommand_BlankCommand_ThrowsArgumentNullException()
        => Assert.Throws<ArgumentNullException>(() => "  ".RunCommand("whatever"));

    #endregion

    #region both pipes are drained while the process runs

    [Fact]
    public void RunCommand_LargeStandardOutput_DoesNotDeadlock()
    {
        // Reading only after WaitForExit deadlocks once the child fills the pipe buffer, so
        // this used to sit there until the 60 second timeout expired.
        var path = Path.Combine(Path.GetTempPath(), $"comh_big_{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, new string('x', 4_000_000));
        try
        {
            var sw = Stopwatch.StartNew();
            var result = Sh.Shell.RunCommandWithResult(Sh.Dump(path));
            sw.Stop();

            Assert.Equal(0, result.ExitCode);
            Assert.True(result.StandardOutput.Length >= 4_000_000,
                $"expected the whole 4 MB back, got {result.StandardOutput.Length} characters");
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(20),
                $"took {sw.Elapsed.TotalSeconds:0.0}s, which means it blocked on the pipe");
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }

    #endregion

    #region timeouts

    [Fact]
    public void RunCommand_Timeout_ThrowsTimeoutException()
    {
        var path = Path.Combine(Path.GetTempPath(), $"comh_to_{Guid.NewGuid():N}.txt");
        try
        {
            Assert.Throws<TimeoutException>(() =>
                Sh.Shell.RunCommand(Sh.RunLongHolding(path), timeout: 1500));
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }

    [Fact]
    public void RunCommandWithResult_Timeout_ReportsTimedOutWithoutThrowing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"comh_to_{Guid.NewGuid():N}.txt");
        try
        {
            var result = Sh.Shell.RunCommandWithResult(Sh.RunLongHolding(path), timeout: 1500);
            Assert.True(result.TimedOut);
            Assert.Equal(-1, result.ExitCode);
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }

    [Fact]
    public void RunCommand_Timeout_KillsTheWholeProcessTree()
    {
        // Only Windows holds a file open against deletion, which is what makes an orphaned
        // grandchild observable. The kill itself runs on every platform.
        if (!Sh.OnWindows) return;

        var path = Path.Combine(Path.GetTempPath(), $"comh_tree_{Guid.NewGuid():N}.txt");
        try
        {
            Assert.Throws<TimeoutException>(() =>
                Sh.Shell.RunCommand(Sh.RunLongHolding(path), timeout: 1500));

            // The grandchild inherited the redirected handle. If only the parent was killed
            // it still owns the file and this throws.
            Sh.AssertReleasedWithin(path);
            Assert.False(File.Exists(path));
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }

    #endregion

    #region cancellation

    [Fact]
    public async Task RunCommandAsync_Cancelled_ThrowsOperationCanceled()
    {
        var path = Path.Combine(Path.GetTempPath(), $"comh_cancel_{Guid.NewGuid():N}.txt");
        using var cts = new CancellationTokenSource();
        try
        {
            var task = Sh.Shell.RunCommandAsync(Sh.RunLongHolding(path), timeout: 60000,
                cancellationToken: cts.Token);
            await Task.Delay(600);
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }

    [Fact]
    public async Task RunCommandAsync_Cancelled_KillsTheWholeProcessTree()
    {
        if (!Sh.OnWindows) return;

        var path = Path.Combine(Path.GetTempPath(), $"comh_cancel_{Guid.NewGuid():N}.txt");
        using var cts = new CancellationTokenSource();
        try
        {
            var task = Sh.Shell.RunCommandAsync(Sh.RunLongHolding(path), timeout: 60000,
                cancellationToken: cts.Token);
            await Task.Delay(600);
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);

            Sh.AssertReleasedWithin(path);
            Assert.False(File.Exists(path));
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }

    [Fact]
    public async Task RunCommandAsync_TokenAlreadyCancelled_ThrowsWithoutHanging()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Sh.Shell.RunCommandAsync(Sh.Echo("hello"), cancellationToken: cts.Token));
    }

    #endregion

    #region the blocking overloads are safe to call from a captured context

    private sealed class BlockedContext : SynchronizationContext
    {
        // Never runs the callback, exactly like a UI message loop that is already blocked.
        public override void Post(SendOrPostCallback d, object? state) { }
        public override void Send(SendOrPostCallback d, object? state) { }
    }

    [Fact]
    public void RunCommand_UnderACapturedSynchronizationContext_DoesNotDeadlock()
    {
        Exception? failure = null;
        var finished = new ManualResetEventSlim(false);

        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new BlockedContext());
            try { Sh.Shell.RunCommand(Sh.Echo("hello")); }
            catch (Exception ex) { failure = ex; }
            finally { finished.Set(); }
        })
        { IsBackground = true };

        thread.Start();

        Assert.True(finished.Wait(TimeSpan.FromSeconds(20)),
            "the blocking overload deadlocked on the captured synchronization context");
        Assert.Null(failure);
    }

    #endregion
}
