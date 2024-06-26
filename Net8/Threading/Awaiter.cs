﻿using System;
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


        public Awaiter(CancellationToken? cToken = null)
            => this._cToken = cToken;

        public Awaiter(TimeSpan delay, CancellationToken? cToken = null) 
            => (this._delay, this._cToken) = (delay, cToken);


        private readonly ConcurrentDictionary<object, Lazy<CancellationTokenSource>> waitList = new();
        private bool disposedValue;

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
        /// <param name="lockObj"></param>
        /// <returns></returns>
        public bool IsLocked(object lockObj)
            => this.waitList.TryGetValue(lockObj, out var item)
                && item?.Value?.IsCancellationRequested == false;

        public bool AreAllLocked(IEnumerable<object> lockObjs)
            => lockObjs.All(x => this.IsLocked(x));

        public bool IsUnlocked(object lockObj)
            => this.waitList.TryGetValue(lockObj, out var item)
                && item?.Value?.IsCancellationRequested == true;


        public bool AreAllUnlocked(IEnumerable<object> lockObjs)
            => lockObjs.All(x => this.IsUnlocked(x));



        public async Task WaitFor(object lockObj, CancellationToken? cToken)
        => await this.WaitFor(lockObj, null, cToken);
        


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

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
