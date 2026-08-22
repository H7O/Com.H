namespace Com.H.Shell;

/// <summary>
/// Bridges the blocking overloads onto the asynchronous implementations so there is only
/// one copy of the process handling logic.
/// </summary>
/// <remarks>
/// Task.Run keeps the wait off whatever synchronisation context the caller is on, so a UI
/// or classic ASP.NET thread cannot deadlock itself the way a bare <c>.Result</c> would.
/// The extra thread pool hop is irrelevant next to the cost of starting a process.
/// </remarks>
internal static class SyncRunner
{
    internal static T Run<T>(Func<Task<T>> work)
        => Task.Run(work).GetAwaiter().GetResult();

    internal static void Run(Func<Task> work)
        => Task.Run(work).GetAwaiter().GetResult();
}
