using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Com.H.Threading
{
    public class AsyncMonitor
    {
        internal class MonitorItem
        {
            public CancellationTokenSource Cts { get; set; }
            public AtomicGate Gate { get; set; }
        }
        private readonly ConcurrentDictionary<object, MonitorItem> waitList = new();
        /// <summary>
        /// Asynchronous version of Monitor.Enter(lockObj, timeout)
        /// </summary>
        /// <param name="lockObj">The lock object</param>
        /// <param name="timeout">Optional timeout for the returned task</param>
        /// <param name="cToken">Optional cancellation token to cancel the awaitable returned task</param>
        /// <returns>Awaitable Task</returns>
        public async Task Enter(object lockObj, TimeSpan? timeout = null, CancellationToken? cToken = null)
        {
            MonitorItem mItem = null;
            bool successfullyEntered = false;
            bool timedOut = true;
            try
            {
                await Task.Delay(
                    (
                        successfullyEntered = (mItem = waitList
                        .AddOrUpdate(lockObj,
                            _ => new MonitorItem()
                            {
                                Cts = (cToken == null ? new CancellationTokenSource()
                                    : CancellationTokenSource.CreateLinkedTokenSource((CancellationToken)cToken)),
                                Gate = new AtomicGate()
                            }
                            , (_, oldMItem) =>
                            {
                                if (oldMItem?.Cts?.IsCancellationRequested != true)
                                    return oldMItem;
                                oldMItem.Cts = (cToken == null ? new CancellationTokenSource()
                                : CancellationTokenSource.CreateLinkedTokenSource((CancellationToken)cToken));
                                oldMItem.Gate.TryClose();
                                return oldMItem;
                            }
                            )
                    )
                    .Gate.TryOpen()) ? TimeSpan.Zero : timeout ?? Timeout.InfiniteTimeSpan,
                    mItem.Cts.Token);
            }
            catch (System.Threading.Tasks.TaskCanceledException) { timedOut = false; }

            if (successfullyEntered || timedOut) return;
            await Enter(lockObj, timeout, cToken);
        }

        /// <summary>
        /// Exit the lock
        /// </summary>
        /// <param name="lockObj">The lock object</param>
        public void Exit(object lockObj)
        {
            _ = waitList.AddOrUpdate(lockObj,
                            _ => new MonitorItem() { Cts = new CancellationTokenSource(), Gate = new AtomicGate() },
                            (_, oldMItem) =>
                            {
                                oldMItem.Cts.Cancel();
                                return oldMItem;
                            });
        }

    }
}
