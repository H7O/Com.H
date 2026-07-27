using System.Diagnostics;
using Com.H.Threading;

namespace Com.H.Tests;

public class CancellableTests
{
    // ---------- Happy paths ----------

    [Fact]
    public void CancellableRun_Action_ExecutesToCompletion()
    {
        using var cts = new CancellationTokenSource();
        bool executed = false;

        Cancellable.CancellableRun(() => executed = true, cts.Token);

        Assert.True(executed);
    }

    [Fact]
    public void CancellableRun_Func_ReturnsResult()
    {
        using var cts = new CancellationTokenSource();

        var result = Cancellable.CancellableRun(() => 42, cts.Token);

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task CancellableRunAsync_Action_ExecutesToCompletion()
    {
        using var cts = new CancellationTokenSource();
        bool executed = false;

        await Cancellable.CancellableRunAsync(() => executed = true, cts.Token);

        Assert.True(executed);
    }

    [Fact]
    public async Task CancellableRunAsync_Func_ReturnsResult()
    {
        using var cts = new CancellationTokenSource();

        var result = await Cancellable.CancellableRunAsync(() => "done", cts.Token);

        Assert.Equal("done", result);
    }

    // ---------- Argument guards ----------

    [Fact]
    public void CancellableRun_NullAction_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => Cancellable.CancellableRun(null!, CancellationToken.None));
    }

    [Fact]
    public void CancellableRun_NullFunc_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => Cancellable.CancellableRun<int>(null, CancellationToken.None));
    }

    [Fact]
    public async Task CancellableRunAsync_NullAction_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => Cancellable.CancellableRunAsync((Action)null!, CancellationToken.None));
    }

    [Fact]
    public async Task CancellableRunAsync_NullFunc_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => Cancellable.CancellableRunAsync((Func<int>)null!, CancellationToken.None));
    }

    // ---------- Pre-cancelled tokens ----------

    [Fact]
    public void CancellableRun_AlreadyCancelledToken_DoesNotRunAction()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        bool executed = false;

        Cancellable.CancellableRun(() => executed = true, cts.Token);

        Assert.False(executed);
    }

    [Fact]
    public void CancellableRun_Func_AlreadyCancelledToken_ReturnsDefault()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = Cancellable.CancellableRun(() => "finished", cts.Token);

        Assert.Null(result);
    }

    // Guards the born-cancelled regression: a Task constructed with an
    // already-cancelled token is born Canceled, and Start() on it throws
    // InvalidOperationException instead of returning a canceled task.
    [Fact]
    public async Task CancellableRunAsync_AlreadyCancelledToken_ReturnsCanceledTask()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        bool executed = false;

        var task = Cancellable.CancellableRunAsync(() => executed = true, cts.Token);

        Assert.True(task.IsCanceled);
        await Assert.ThrowsAsync<TaskCanceledException>(() => task);
        Assert.False(executed);
    }

    [Fact]
    public async Task CancellableRunAsync_Func_AlreadyCancelledToken_ReturnsCanceledTask()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        bool executed = false;

        var task = Cancellable.CancellableRunAsync(() => { executed = true; return 1; }, cts.Token);

        Assert.True(task.IsCanceled);
        await Assert.ThrowsAsync<TaskCanceledException>(() => task);
        Assert.False(executed);
    }

    // ---------- Cancellation interrupts the worker, not the canceller ----------

    // Guards the wrong-thread regression: token.Register callbacks run on the
    // thread that calls Cancel(), so Thread.CurrentThread.Interrupt() there hits
    // the canceller and leaves the hung worker running for the full 30 seconds.
    // The canceller in these tests is always a dedicated thread, for two reasons:
    // the registered callback runs synchronously on whichever thread calls Cancel()
    // (so Cancel() blocks for the grace period), and if a wrongly-aimed interrupt
    // ever targets the canceller again it must land on a thread the test owns —
    // never a shared thread-pool thread — where the post-cancel sleep detonates it.
    //
    // The started-event handshake matters too: the action signals it as its first
    // statement, and the action runs strictly after token.Register inside the
    // worker. Cancelling only after the signal guarantees the callback fires on
    // the canceller (not inline on the worker during Register) and that the task
    // can't be born-cancelled — which would skip the interrupt path entirely and
    // let these regression guards pass without testing anything.

    [Fact]
    public void CancellableRun_CancelWithTimeout_InterruptsHungWorker_NotCanceller()
    {
        using var cts = new CancellationTokenSource();
        using var started = new ManualResetEventSlim(false);
        bool cancellerInterrupted = false;

        var canceller = new Thread(() =>
        {
            try
            {
                started.Wait(TimeSpan.FromSeconds(30));
                cts.Cancel();
                Thread.Sleep(50); // a pending interrupt aimed at this thread would throw here
            }
            catch (ThreadInterruptedException) { cancellerInterrupted = true; }
        });
        canceller.Start();

        var sw = Stopwatch.StartNew();
        Cancellable.CancellableRun(
            () => { started.Set(); Thread.Sleep(TimeSpan.FromSeconds(30)); },
            cts.Token,
            timeout: TimeSpan.FromSeconds(1));
        sw.Stop();

        canceller.Join();

        Assert.False(cancellerInterrupted);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(15),
            $"Took {sw.Elapsed} — the hung worker was never interrupted.");
    }

    [Fact]
    public void CancellableRun_Func_CancelWithTimeout_ReturnsDefaultWhenInterrupted()
    {
        using var cts = new CancellationTokenSource();
        using var started = new ManualResetEventSlim(false);
        var canceller = new Thread(() => { started.Wait(TimeSpan.FromSeconds(30)); cts.Cancel(); });
        canceller.Start();

        var sw = Stopwatch.StartNew();
        var result = Cancellable.CancellableRun<string>(
            () => { started.Set(); Thread.Sleep(TimeSpan.FromSeconds(30)); return "finished"; },
            cts.Token,
            timeout: TimeSpan.FromSeconds(1));
        sw.Stop();

        canceller.Join();

        Assert.Null(result);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(15),
            $"Took {sw.Elapsed} — the hung worker was never interrupted.");
    }

    [Fact]
    public void CancellableRun_CancelWithoutTimeout_InterruptsImmediately()
    {
        using var cts = new CancellationTokenSource();
        using var started = new ManualResetEventSlim(false);
        var canceller = new Thread(() => { started.Wait(TimeSpan.FromSeconds(30)); cts.Cancel(); });
        canceller.Start();

        var sw = Stopwatch.StartNew();
        Cancellable.CancellableRun(
            () => { started.Set(); Thread.Sleep(TimeSpan.FromSeconds(30)); },
            cts.Token);
        sw.Stop();

        canceller.Join();

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10),
            $"Took {sw.Elapsed} — the hung worker was never interrupted.");
    }

    [Fact]
    public async Task CancellableRunAsync_CancelWithTimeout_InterruptsHungWorker()
    {
        using var cts = new CancellationTokenSource();
        using var started = new ManualResetEventSlim(false);
        bool interrupted = false;
        bool cancellerInterrupted = false;

        var task = Cancellable.CancellableRunAsync(() =>
        {
            started.Set();
            try { Thread.Sleep(TimeSpan.FromSeconds(30)); }
            catch (ThreadInterruptedException) { interrupted = true; throw; }
        }, cts.Token, TimeSpan.FromSeconds(1));

        var canceller = new Thread(() =>
        {
            try
            {
                started.Wait(TimeSpan.FromSeconds(30));
                cts.Cancel();
                Thread.Sleep(50); // a pending interrupt aimed at this thread would throw here
            }
            catch (ThreadInterruptedException) { cancellerInterrupted = true; }
        });
        canceller.Start();

        var finished = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(15)));

        Assert.Same(task, finished);
        await task; // interrupt is swallowed inside — completes normally
        Assert.True(interrupted);
        canceller.Join();
        Assert.False(cancellerInterrupted);
    }

    // ---------- Completion during the grace period skips the interrupt ----------

    // Guards the stale-interrupt regression: the done flag must be set the moment
    // the action finishes (on the worker), otherwise the grace-period poll never
    // observes it — cancellation then waits out the full grace window and fires
    // an interrupt at a thread whose work already completed.
    [Fact]
    public void CancellableRun_ActionFinishesDuringGracePeriod_NoInterruptAndNoGraceWait()
    {
        using var cts = new CancellationTokenSource();
        using var started = new ManualResetEventSlim(false);
        bool cancellerInterrupted = false;

        var canceller = new Thread(() =>
        {
            try
            {
                started.Wait(TimeSpan.FromSeconds(30));
                cts.Cancel();
                Thread.Sleep(50);
            }
            catch (ThreadInterruptedException) { cancellerInterrupted = true; }
        });
        canceller.Start();

        // Outlives the cancel but finishes well inside the 10s grace window.
        // The 8s bound must stay BELOW the grace: both the old-code regression
        // and a mis-scheduled cancel elapse ~the full grace, so a bound above
        // it could not tell a fixed build from a broken one.
        var sw = Stopwatch.StartNew();
        Cancellable.CancellableRun(
            () => { started.Set(); Thread.Sleep(700); },
            cts.Token,
            timeout: TimeSpan.FromSeconds(10));
        sw.Stop();

        canceller.Join();

        Assert.False(cancellerInterrupted);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(8),
            $"Took {sw.Elapsed} — cancellation waited out the grace period instead of observing completion.");
    }

    // ---------- CancellableWait ----------

    [Fact]
    public void CancellableWait_TaskCompletesInTime_DoesNotInvokeTimeoutAction()
    {
        bool timedOut = false;

        Task.FromResult(1).CancellableWait(timeout: 5000, actionOnTimeout: () => timedOut = true);

        Assert.False(timedOut);
    }

    [Fact]
    public void CancellableWait_TaskExceedsTimeout_InvokesTimeoutAction()
    {
        bool timedOut = false;
        var tcs = new TaskCompletionSource<int>();

        tcs.Task.CancellableWait(timeout: 100, actionOnTimeout: () => timedOut = true);

        Assert.True(timedOut);
        tcs.SetResult(1);
    }

    [Fact]
    public void CancellableWait_TokenCancelled_InvokesTimeoutAction()
    {
        bool signalled = false;
        using var cts = new CancellationTokenSource(100);
        var tcs = new TaskCompletionSource<int>();

        tcs.Task.CancellableWait(timeout: 30000, token: cts.Token, actionOnTimeout: () => signalled = true);

        Assert.True(signalled);
        tcs.SetResult(1);
    }
}
