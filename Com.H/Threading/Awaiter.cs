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
    /// await a.WaitFor("abc");
    /// // logic after waiting
    /// 
    /// Thread 2:
    /// a.Unlock("abc");
    /// // ^ causes Thread 1 to resume
    /// 
    /// e.g. 2
    /// Main:
    /// Awaiter a = new Awaiter();
    /// 
    /// Thread 1:
    /// await a.WaitFor(new string[] {"abc", "efg"});
    /// // logic after waiting
    /// 
    /// Thread 2:
    /// a.Unlock("abc");
    /// // ^ causes lock 1 to be released
    /// 
    /// Thread 3:
    /// a.Unlock("efg");
    /// // ^ causes lock 2 to be released, which causes Thread 1 to resume
    /// </summary>
    public class Awaiter
    {

        private readonly ConcurrentDictionary<object, CancellationTokenSource> waitList = new();


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
    }
}
