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
        private readonly ConcurrentDictionary<object, Lazy<MonitorItem>> waitList = new ConcurrentDictionary<object, Lazy<MonitorItem>>();

        public async Task Enter(object lockOb, CancellationToken cToken)
            => await this.Enter(lockOb, null, cToken);
        /// <summary>
        /// Asynchronous version of Monitor.Enter(lockObj, timeout)
        /// </summary>
        /// <param name="lockObj">The lock object</param>
        /// <param name="timeout">Optional timeout for the returned task</param>
        /// <param name="cToken">Optional cancellation token to cancel the awaitable returned task</param>
        /// <returns>Awaitable Task</returns>
        public async Task Enter(object lockObj, TimeSpan? timeout = null, CancellationToken? cToken = null)
        {
            if (lockObj is null) throw new ArgumentNullException(nameof(lockObj));
            MonitorItem mItem = null;
            bool successfullyEntered = false;
            bool timedOut = true;
            try
            {
                await Task.Delay(
                    (
                        successfullyEntered = (mItem = waitList
                        .AddOrUpdate(lockObj,
                            _ => new Lazy<MonitorItem>(()=>
                            new MonitorItem()
                            {
                                Cts = (cToken == null ? new CancellationTokenSource()
                                    : CancellationTokenSource.CreateLinkedTokenSource((CancellationToken)cToken)),
                                Gate = new AtomicGate()
                            })
                            , (_, lazyOldMItem) =>
                            {
                                var oldMItem = lazyOldMItem.Value;
                                if (oldMItem?.Cts?.IsCancellationRequested != true)
                                    return lazyOldMItem;
                                return new Lazy<MonitorItem>(() =>
                                   {
                                       oldMItem.Cts = (cToken == null ? new CancellationTokenSource()
                                       : CancellationTokenSource.CreateLinkedTokenSource((CancellationToken)cToken));
                                       oldMItem.Gate?.TryClose();
                                       return oldMItem;
                                   });
                            }
                            ).Value
                    )
                    .Gate?.TryOpen() == true) ? TimeSpan.Zero : timeout ?? Timeout.InfiniteTimeSpan,
                    // no possible path for null Cts
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    mItem.Cts.Token);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
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
            this.waitList.TryGetValue(lockObj, out var lazyValue);
            lazyValue?.Value?.Cts?.Cancel();
            //_ = waitList.AddOrUpdate(lockObj,
            //                _ => new Lazy<MonitorItem>(()=>new MonitorItem() { Cts = new CancellationTokenSource(), Gate = new AtomicGate() }),
            //                (_, lazyOldMItem) =>
            //                {
            //                    oldMItem.Cts.Cancel();
            //                    return oldMItem;
            //                });
        }

    }
}
