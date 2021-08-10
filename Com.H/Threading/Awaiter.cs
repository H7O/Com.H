using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Com.H.Threading
{
    /// <summary>
    /// Offers a mean for a multithreaded code to wait for a specific condition (or specific multiple conditions) in order for it to proceed its execution flow.
    /// The condition (or conditions) are denoted by a lock (or locks) passed to the WaitFor() function.
    /// When a seperate thread calls Unlock() on that specific lock (or locks), the waiting thread is then resumes its execution flow.
    /// e.g. 1
    /// Main:
    /// Awaiter a = new Awaiter();
    /// 
    /// Thread 1:
    /// await a.WaitFor("lock 1");
    /// // logic after waiting
    /// 
    /// Thread 2:
    /// a.Unlock("lock 1");
    /// // ^ causes Thread 1 to resume
    /// 
    /// e.g. 2
    /// Main:
    /// Awaiter a = new Awaiter();
    /// 
    /// Thread 1:
    /// await a.WaitFor(new string[] {"lock 1", "lock 2"});
    /// // logic after waiting
    /// 
    /// Thread 2:
    /// a.Unlock("lock 1");
    /// // ^ causes lock 1 to be released
    /// 
    /// Thread 3:
    /// a.Unlock("lock 2");
    /// // ^ causes lock 2 to be released, which causes Thread 1 to resume
    /// </summary>
    public class Awaiter : IDisposable
    {

        private readonly ConcurrentDictionary<object, CancellationTokenSource> waitList = new();
        private bool disposedValue;

        public void Unlock(object lockObj)
        {
            if (this.waitList.TryGetValue(lockObj, out CancellationTokenSource cts))
                cts?.Cancel();
        }


        //private async Task WaitForE(IEnumerable<object> lockObjs, TimeSpan? delay, CancellationToken? cToken)
        //=> await Task.WhenAll(lockObjs.Select(async x => await WaitFor(x, delay, cToken)));



        /// <summary>
        /// Wait for a complete call on a lock object or a group of lock objects (group of lock objects can be passed in IEnumerable of objects) 
        /// </summary>
        /// <param name="lockObj">Could be a single object, or an IEnumerable of objects</param>
        /// <param name="delay"></param>
        /// <returns></returns>
        public async Task WaitFor(object lockObj, TimeSpan? delay = null, CancellationToken? cToken = null)
        {
            if (lockObj is null) throw new ArgumentNullException(nameof(lockObj));
            if (typeof(IEnumerable<object>).IsAssignableFrom(lockObj.GetType()))
            {
                await Task.WhenAll(((IEnumerable<object>)lockObj)
                    .Select(async x => await WaitFor(x, delay, cToken)));
                return;
            }

            var cts = this.waitList.GetOrAdd(lockObj, _ =>
            {
                return (cToken == null ? new CancellationTokenSource()
                : CancellationTokenSource.CreateLinkedTokenSource((CancellationToken)cToken));
            });

            if (cts.IsCancellationRequested) return;
            try
            {
                await Task.Delay(delay ?? Timeout.InfiniteTimeSpan, cts.Token);
            }
            catch (TaskCanceledException)
            { }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    foreach (var lockObj in this.waitList?.Values)
                    {
                        try
                        {
                            this.Unlock(lockObj);
                        }
                        catch { }
                    }
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~Awaiter()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
