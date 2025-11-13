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
        private readonly CancellationToken? _cToken;
        private readonly TimeSpan? _delay;


        /// <summary>
        /// Initializes a new instance of the Awaiter class with an optional cancellation token.
        /// </summary>
        /// <param name="cToken">Optional cancellation token to cancel waiting operations</param>
        public Awaiter(CancellationToken? cToken = null)
            => this._cToken = cToken;

        /// <summary>
        /// Initializes a new instance of the Awaiter class with a default delay and optional cancellation token.
        /// </summary>
        /// <param name="delay">Default delay to use when waiting</param>
        /// <param name="cToken">Optional cancellation token to cancel waiting operations</param>
        public Awaiter(TimeSpan delay, CancellationToken? cToken = null) 
            => (this._delay, this._cToken) = (delay, cToken);


        private readonly ConcurrentDictionary<object, Lazy<CancellationTokenSource>> waitList = new();
        private bool disposedValue;

        /// <summary>
        /// Unlocks the specified lock object, allowing any waiting threads to proceed.
        /// </summary>
        /// <param name="lockObj">The lock object to unlock</param>
        public void Unlock(object lockObj)
        {
            this.waitList.GetOrAdd(lockObj, _ =>
            {
                return new Lazy<CancellationTokenSource>
                (
                    new CancellationTokenSource()
                );
            }).Value?.Cancel();
        }


        //private async Task WaitForE(IEnumerable<object> lockObjs, TimeSpan? delay, CancellationToken? cToken)
        //=> await Task.WhenAll(lockObjs.Select(async x => await WaitFor(x, delay, cToken)));

        /// <summary>
        /// True if the lockObj is still locked, false if it's unlocked or doesn't exist
        /// </summary>
        /// <param name="lockObj">The lock object to check</param>
        /// <returns>True if locked, false otherwise</returns>
        public bool IsLocked(object lockObj)
            => this.waitList.TryGetValue(lockObj, out var item)
                && item?.Value?.IsCancellationRequested == false;

        /// <summary>
        /// Returns true if all specified lock objects are locked.
        /// </summary>
        /// <param name="lockObjs">Collection of lock objects to check</param>
        /// <returns>True if all lock objects are locked, false otherwise</returns>
        public bool AreAllLocked(IEnumerable<object> lockObjs)
            => lockObjs.All(x => this.IsLocked(x));

        /// <summary>
        /// Returns true if the lock object is unlocked or doesn't exist.
        /// </summary>
        /// <param name="lockObj">The lock object to check</param>
        /// <returns>True if unlocked, false otherwise</returns>
        public bool IsUnlocked(object lockObj)
            => this.waitList.TryGetValue(lockObj, out var item)
                && item?.Value?.IsCancellationRequested == true;


        /// <summary>
        /// Returns true if all specified lock objects are unlocked.
        /// </summary>
        /// <param name="lockObjs">Collection of lock objects to check</param>
        /// <returns>True if all lock objects are unlocked, false otherwise</returns>
        public bool AreAllUnlocked(IEnumerable<object> lockObjs)
            => lockObjs.All(x => this.IsUnlocked(x));



        /// <summary>
        /// Wait for the specified lock object to be unlocked.
        /// </summary>
        /// <param name="lockObj">The lock object to wait for</param>
        /// <param name="cToken">Optional cancellation token</param>
        /// <returns>A task that completes when the lock is unlocked</returns>
        public async Task WaitFor(object lockObj, CancellationToken? cToken)
        => await this.WaitFor(lockObj, null, cToken);
        


        /// <summary>
        /// Wait for a complete call on a lock object or a group of lock objects (group of lock objects can be passed in IEnumerable of objects) 
        /// </summary>
        /// <param name="lockObj">Could be a single object, or an IEnumerable of objects</param>
        /// <param name="delay">Optional delay timeout</param>
        /// <param name="cToken">Optional cancellation token</param>
        /// <returns>A task that completes when all specified locks are unlocked</returns>
        public async Task WaitFor(object lockObj, TimeSpan? delay = null, CancellationToken? cToken = null)
        {
            if (lockObj is null) throw new ArgumentNullException(nameof(lockObj));
            if (typeof(IEnumerable<object>).IsAssignableFrom(lockObj.GetType()))
            {
                await Task.WhenAll(((IEnumerable<object>)lockObj)
                    .Select(async x => await WaitFor(x, delay ?? this._delay, cToken ?? this._cToken)));
                return;
            }

            var cts = this.waitList.GetOrAdd(lockObj, _ =>
            {
                return new Lazy<CancellationTokenSource>( 
                    ((cToken??this._cToken) == null ? new CancellationTokenSource()
                    // no possible path for null
#pragma warning disable CS8629 // Nullable value type may be null. 
                : CancellationTokenSource.CreateLinkedTokenSource((CancellationToken)(cToken ?? this._cToken)))
#pragma warning restore CS8629 // Nullable value type may be null.
                );
            });

            if (cts.Value.IsCancellationRequested) return;
            try
            {
                await Task.Delay(delay ?? Timeout.InfiniteTimeSpan, cts.Value.Token);
            }
            catch (TaskCanceledException)
            { }
        }

        /// <summary>
        /// Disposes the Awaiter, releasing all locks and cancelling all waiting operations.
        /// </summary>
        /// <param name="disposing">True if disposing managed resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (this.waitList is not null)
                        foreach (var lockObj in this.waitList.Values)
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

        /// <summary>
        /// Disposes the Awaiter, releasing all resources.
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
