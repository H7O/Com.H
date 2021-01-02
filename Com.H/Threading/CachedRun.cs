using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Com.H.Threading
{
    internal interface ICachedRunItem 
    {
        object UniqueKey { get; set; }
        DateTime? CacheUntil { get; set; }
    }
    internal class CachedRunItem<T> : ICachedRunItem
    {
        public object UniqueKey { get; set; }
        public T Value { get; set; }
        public DateTime? CacheUntil { get; set; }
    }

    public class CachedRun : IDisposable
    {

        #region properties
        public DateTime? DefaultCacheUntil { get; set; }
        private ReaderWriterLockSlim RWLock { get; set; }
        private CancellationTokenSource Cts { get; set; }
        private AtomicGate CleanupSwitch { get; set; }
        private bool disposedValue;
        private ConcurrentDictionary<object, ICachedRunItem> CachedItems { get; set; }

        #endregion

        #region constructor
        public CachedRun()
        {
            this.CachedItems = new ConcurrentDictionary<object, ICachedRunItem>();
            this.DefaultCacheUntil = DateTime.Today.AddDays(1);
            this.RWLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
            this.CleanupSwitch = new AtomicGate();
        }

        #endregion

        #region run

        /// <summary>
        /// Runs the func once and caches its result for subsequent calls throughout the duration of the timespan.
        /// Once the timespan elapses, the process repeats again.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="func"></param>
        /// <param name="duration"></param>
        /// <param name="uniqueKey"></param>
        /// <returns></returns>
        public T Run<T>(
            Func<T> func,
            TimeSpan duration,
            object uniqueKey = null
            ) => this.Run<T>(func, DateTime.Now.Add(duration), uniqueKey);
        
        public T Run<T>(
            Func<T> func,
            DateTime? cacheUntil = null,
            object uniqueKey = null)
        {
            if (func == null) throw new ArgumentNullException(nameof(func));
            if (uniqueKey == null) uniqueKey = func;
            this.RemoveExpired();

            try
            {
                this.RWLock.EnterReadLock();
                if (this.CachedItems.ContainsKey(uniqueKey))
                    return ((CachedRunItem<T>)this.CachedItems[uniqueKey]).Value;
            }
            catch { throw; }
            finally { this.RWLock.ExitReadLock(); }
            ICachedRunItem item;
            try
            {
                this.RWLock.EnterWriteLock();
                this.CachedItems[uniqueKey] = item = new CachedRunItem<T>()
                {
                    UniqueKey = uniqueKey,
                    Value = func(),
                    CacheUntil = cacheUntil ?? this.DefaultCacheUntil ??
                    DateTime.Today.AddDays(1)
                };
            }
            catch { throw; }
            finally
            {
                this.RWLock.ExitWriteLock();
            }
            if (item == null) return default;
            return ((CachedRunItem<T>)item).Value;
        }


        public void Run(
            Action action,
            TimeSpan duration,
            object uniqueKey = null
            ) => this.Run(action, DateTime.Now.Add(duration), uniqueKey);


        public void Run(
            Action action,
            DateTime? cacheUntil = null,
            object uniqueKey = null) 
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (uniqueKey == null) uniqueKey = action;
            this.RemoveExpired();

            try
            {
                this.RWLock.EnterReadLock();
                if (this.CachedItems.ContainsKey(uniqueKey))
                    return;
            }
            catch { throw; }
            finally { this.RWLock.ExitReadLock(); }
            ICachedRunItem item;
            try
            {
                this.RWLock.EnterWriteLock();
                action();
                this.CachedItems[uniqueKey] = item = new CachedRunItem<object>()
                {
                    UniqueKey = uniqueKey,
                    CacheUntil = cacheUntil ?? this.DefaultCacheUntil ??
                    DateTime.Today.AddDays(1)
                };
            }
            catch { throw; }
            finally
            {
                this.RWLock.ExitWriteLock();
            }
        }


        #endregion

        #region cache cleanup
        /// <summary>
        /// Enable automated cache cleanup on specific interval.
        /// This is optional, as there is already manual cleanup in place that gets executed on every Run call
        /// </summary>
        /// <param name="cleanupInterval"></param>
        /// <param name="cToken"></param>
        /// <returns></returns>
        public Task EnableAutoCleanup(
            int cleanupInterval = 1000,
            CancellationToken? cToken = null
            )
        {
            if (!this.CleanupSwitch.TryOpen()) return Task.CompletedTask;

            if (cToken == null)
            {
                if (this.Cts == null || this.Cts.IsCancellationRequested)
                    this.Cts = new CancellationTokenSource();
            }
            else
                this.Cts = CancellationTokenSource
                    .CreateLinkedTokenSource((CancellationToken)cToken);
            Task cleanupMonitoring = new Task(() =>
            {
                while (!this.Cts.Token.IsCancellationRequested)
                {
                    Task.Delay(cleanupInterval, this.Cts.Token)
                    .GetAwaiter().GetResult();
                    this.RemoveExpired();
                }
            });
            cleanupMonitoring.ConfigureAwait(false);
            cleanupMonitoring.ContinueWith((_) =>
            {
                this.CleanupSwitch.TryClose();
            }, TaskScheduler.Default).ConfigureAwait(false);
            cleanupMonitoring.Start();
            return cleanupMonitoring;
        }

        public void DisableAutoCleanup()
        {
            if (!this.CleanupSwitch.IsOpen) return;
            this.Cts.Cancel();
        }

        private void RemoveExpired()
        {
            try
            {
                
                this.RWLock.EnterWriteLock();
                var expired = this.CachedItems
                    .Values
                    .Where(x => x.CacheUntil != null && x.CacheUntil <= DateTime.Now)
                    .Select(x => x.UniqueKey).ToList();
                foreach (var key in expired)
                    this.CachedItems.TryRemove(key, out _);
            }
            catch { throw; }
            finally
            {
                this.RWLock.ExitWriteLock();
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    try
                    {
                        if (this.Cts != null && !this.Cts.IsCancellationRequested)
                            this.Cts.Cancel();
                    }
                    catch { }
                    try
                    {
                        this.RWLock.Dispose();
                    }
                    catch { }
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~TimedCache()
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
        #endregion

    }
}
