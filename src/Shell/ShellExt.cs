using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Com.H.Shell;

/// <summary>
/// The outcome of running an external command.
/// </summary>
public sealed class ShellCommandResult
{
    /// <summary>
    /// The process exit code, or -1 when the process had to be killed after timing out.
    /// </summary>
    public int ExitCode { get; init; }

    /// <summary>
    /// Everything the process wrote to standard output.
    /// </summary>
    public string StandardOutput { get; init; } = string.Empty;

    /// <summary>
    /// Everything the process wrote to standard error. Plenty of well behaved programs
    /// write progress and status messages here on a completely successful run, so this
    /// being non-empty does not mean the command failed.
    /// </summary>
    public string StandardError { get; init; } = string.Empty;

    /// <summary>
    /// True when the process did not exit within the allotted time and was killed.
    /// </summary>
    public bool TimedOut { get; init; }
}

/// <summary>
/// Thrown when an external command exits with a non zero exit code. Carries the full
/// result so callers can inspect the exit code rather than parse the message.
/// </summary>
public class ShellCommandException : Exception
{
    /// <summary>
    /// The full outcome of the command that failed, including both output streams.
    /// </summary>
    public ShellCommandResult Result { get; }

    /// <summary>
    /// The exit code the command returned.
    /// </summary>
    public int ExitCode => Result.ExitCode;

    /// <summary>
    /// Creates an exception carrying the outcome of the command that failed.
    /// </summary>
    public ShellCommandException(string message, ShellCommandResult result)
        : base(message) => Result = result;
}

/// <summary>
/// Runs external commands and reports what happened.
/// </summary>
public static class ShellExt
{
    /// <summary>
    /// How long to keep waiting for a redirected stream, or for a killed process, once
    /// there is no longer any reason to expect more.
    /// </summary>
    private const int GraceMs = 5000;

    /// <summary>
    /// Runs a command and returns its exit code together with whatever it wrote to standard
    /// output and standard error. Never throws because of what the command printed - inspect
    /// <see cref="ShellCommandResult.ExitCode"/> to decide whether it worked.
    /// </summary>
    /// <param name="command">Path to the executable.</param>
    /// <param name="args">Command line arguments.</param>
    /// <param name="workingDirectory">Working directory, defaults to the app base directory.</param>
    /// <param name="timeout">How long to wait, in milliseconds, before killing the process.</param>
    /// <param name="environmentVariables">Extra environment variables for the child process.</param>
    /// <param name="cancellationToken">Kills the process tree and throws when signalled.</param>
    public static async Task<ShellCommandResult> RunCommandWithResultAsync(
        this string command,
        string args,
        string? workingDirectory = null,
        int timeout = 60000,
        System.Collections.Specialized.StringDictionary? environmentVariables = null,
        CancellationToken cancellationToken = default
        )
    {
        if (string.IsNullOrWhiteSpace(command)) throw new ArgumentNullException(nameof(command));

        using var process = new Process
        {
            StartInfo = BuildStartInfo(command, args, workingDirectory, environmentVariables)
        };

        process.Start();

        // Drain both pipes from the moment the process starts. Waiting for exit before
        // reading them deadlocks as soon as the child writes more than the pipe buffer
        // holds, which chatty programs such as chromium do routinely.
        var stdOut = process.StandardOutput.ReadToEndAsync();
        var stdErr = process.StandardError.ReadToEndAsync();

        using var timeoutSource = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            timeoutSource.Token, cancellationToken);

        try
        {
            await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            KillTree(process);

            // A caller cancelling is not the same thing as the command running too long,
            // so it surfaces as cancellation rather than as a result to inspect.
            cancellationToken.ThrowIfCancellationRequested();

            return new ShellCommandResult
            {
                ExitCode = -1,
                TimedOut = true,
                StandardOutput = await DrainAsync(stdOut).ConfigureAwait(false),
                StandardError = await DrainAsync(stdErr).ConfigureAwait(false)
            };
        }

        return new ShellCommandResult
        {
            ExitCode = process.ExitCode,
            TimedOut = false,
            StandardOutput = await DrainAsync(stdOut).ConfigureAwait(false),
            StandardError = await DrainAsync(stdErr).ConfigureAwait(false)
        };
    }

    /// <summary>
    /// Runs a command and returns its standard output, throwing if it timed out or exited
    /// with a non zero exit code.
    /// </summary>
    /// <exception cref="TimeoutException">The command did not finish in time.</exception>
    /// <exception cref="ShellCommandException">The command exited with a non zero code.</exception>
    public static async Task<string> RunCommandAsync(
        this string command,
        string args,
        string? workingDirectory = null,
        int timeout = 60000,
        System.Collections.Specialized.StringDictionary? environmentVariables = null,
        CancellationToken cancellationToken = default
        )
        => Judge(command, timeout,
            await command.RunCommandWithResultAsync(
                args, workingDirectory, timeout, environmentVariables, cancellationToken)
                .ConfigureAwait(false));

    /// <summary>
    /// Blocking equivalent of <see cref="RunCommandWithResultAsync"/>.
    /// </summary>
    public static ShellCommandResult RunCommandWithResult(
        this string command,
        string args,
        string? workingDirectory = null,
        int timeout = 60000,
        System.Collections.Specialized.StringDictionary? environmentVariables = null
        )
        => SyncRunner.Run(() => command.RunCommandWithResultAsync(
            args, workingDirectory, timeout, environmentVariables));

    /// <summary>
    /// Blocking equivalent of <see cref="RunCommandAsync"/>.
    /// </summary>
    /// <exception cref="TimeoutException">The command did not finish in time.</exception>
    /// <exception cref="ShellCommandException">The command exited with a non zero code.</exception>
    public static string RunCommand(
        this string command,
        string args,
        string? workingDirectory = null,
        int timeout = 60000,
        System.Collections.Specialized.StringDictionary? environmentVariables = null
        )
        => Judge(command, timeout,
            command.RunCommandWithResult(args, workingDirectory, timeout, environmentVariables));

    private static string Judge(string command, int timeout, ShellCommandResult result)
    {
        if (result.TimedOut)
            throw new TimeoutException(
                $"'{command}' did not exit within {timeout} ms and was terminated."
                + Describe(result));

        if (result.ExitCode != 0)
            throw new ShellCommandException(
                $"'{command}' exited with code {result.ExitCode}." + Describe(result),
                result);

        return result.StandardOutput;
    }

    private static ProcessStartInfo BuildStartInfo(
        string command,
        string args,
        string? workingDirectory,
        System.Collections.Specialized.StringDictionary? environmentVariables)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory))
            workingDirectory = AppDomain.CurrentDomain.BaseDirectory;

        var startInfo = new ProcessStartInfo(command, args)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = workingDirectory,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };

        if (environmentVariables != null)
        {
            foreach (var key in environmentVariables.Keys)
            {
                var keyStr = key as string;
                if (string.IsNullOrWhiteSpace(keyStr))
                    continue;
                startInfo.EnvironmentVariables[keyStr] = environmentVariables[keyStr];
            }
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            startInfo.LoadUserProfile = true; // Only set this on Windows

        return startInfo;
    }

    private static string Describe(ShellCommandResult result)
    {
        var error = result.StandardError?.Trim();
        return string.IsNullOrWhiteSpace(error)
            ? " The command produced no error output."
            : $"{Environment.NewLine}{Environment.NewLine}{Tail(error!)}";
    }

    /// <summary>
    /// Keeps error messages readable when a program produces pages of diagnostics.
    /// </summary>
    private static string Tail(string text, int maxLength = 2000)
        => text.Length <= maxLength ? text : "..." + text.Substring(text.Length - maxLength);

    /// <summary>
    /// Waits a short while for a redirected stream to reach end of file. A grandchild that
    /// inherited the handle can hold the pipe open after the process we started has gone,
    /// so this gives up rather than waiting for ever.
    /// </summary>
    private static async Task<string> DrainAsync(Task<string> readTask)
    {
        try
        {
            var finished = await Task.WhenAny(readTask, Task.Delay(GraceMs)).ConfigureAwait(false);
            return ReferenceEquals(finished, readTask)
                ? await readTask.ConfigureAwait(false)
                : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void KillTree(Process process)
    {
        try
        {
#if NETSTANDARD2_0
            KillTreeLegacy(process);
#else
            // chromium and friends spawn a tree of child processes; killing only the parent
            // leaves those behind holding on to the profile and the output file.
            process.Kill(entireProcessTree: true);
#endif
        }
        catch { }

        try { process.WaitForExit(GraceMs); } catch { }
    }

#if NETSTANDARD2_0
    /// <summary>
    /// netstandard2.0 has no Kill(entireProcessTree). taskkill /T does the same job on
    /// Windows, which is where this target actually gets consumed; anywhere else fall back
    /// to killing just the process we started.
    /// </summary>
    private static void KillTreeLegacy(Process process)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                using var killer = Process.Start(new ProcessStartInfo("taskkill", $"/PID {process.Id} /T /F")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });
                killer?.WaitForExit(GraceMs);
                if (process.HasExited) return;
            }
            catch { }
        }

        process.Kill();
    }
#endif
}
