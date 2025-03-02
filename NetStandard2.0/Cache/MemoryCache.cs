using Com.H.Threading;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Com.H.Cache
{
    /// <summary>
    /// Thread-safe memory cache with automatic cleanup of expired items.
    /// </summary>
    public class MemoryCache : IDisposable
    {
        /// <summary>
        /// If true, the cache will store null values.
        /// </summary>
        public bool CacheNullValues { get; set; } = false;
        /// <summary>
        /// If true, the cache will store factories that cause exceptions when generating values.
        /// </summary>
        public bool CacheValueFactoriesThatCauseExceptions { get; set; } = false;
        private CancellationTokenSource Cts { get; set; }
        private AtomicGate CleanupSwitch { get; set; } = new AtomicGate();
        internal class CacheItem
        {
            internal DateTime ExpiryDate { get; set; }
            internal object Value { get; set; }
            internal bool Expired => DateTime.Now >= ExpiryDate;
        }
        private readonly ConcurrentDictionary<object, Lazy<CacheItem>> cacheItems =
            new ConcurrentDictionary<object, Lazy<CacheItem>>();

        /// <summary>
        /// Retrieves a cached item or creates it using the provided value factory.
        /// </summary>
        /// <param name="key">The key for the cached item.</param>
        /// <param name="getValue">The function to generate the value if it's not in the cache.</param>
        /// <param name="timeSpan">The lifespan of the cached item.</param>
        /// <returns>The cached item.</returns>
        public T Get<T>(object key, Func<T> getValue, TimeSpan? timeSpan = null)
        {
            lock (key)
            {
                T value;
                try
                {
                    value = (T)this.cacheItems.AddOrUpdate(key,
                    _ => new Lazy<CacheItem>(() => new CacheItem()
                    {
                        ExpiryDate = timeSpan == null ? DateTime.Today.AddDays(1)
                        : DateTime.Now.Add((TimeSpan)timeSpan),
                        Value = getValue == null ? default : getValue()
                    }),
                    (_, oldLazyValue) =>
                    {
                        var oldValue = oldLazyValue.Value;
                        if (!oldValue.Expired) return oldLazyValue;
                        oldValue.Value = getValue == null ? default : getValue();
                        oldValue.ExpiryDate = (timeSpan == null ? DateTime.Today.AddDays(1)
                         : DateTime.Now.Add((TimeSpan)timeSpan));
                        return oldLazyValue;
                    }).Value.Value;
                }
                catch
                {
                    if (this.CacheValueFactoriesThatCauseExceptions)
                        throw;

                    this.cacheItems.TryRemove(key, out _);
                    throw;
                }

                if (value == null && !this.CacheNullValues)
                    this.cacheItems.TryRemove(key, out _);

                return value;
            }
        }
        /// <summary>
        /// Clears expired items from the cache.
        /// </summary>
        public void ClearExpired()
        {
            var expiredItemsKeys = this.cacheItems.Where(x => x.Value.Value.Expired).Select(x => x.Key).ToList();
            foreach (var key in expiredItemsKeys)
                this.cacheItems.TryRemove(key, out _);
        }

        /// <summary>
        /// Starts an automatic cleanup task to clear expired cache items at a specified interval.
        /// </summary>
        /// <param name="interval">The interval between cleanup operations.</param>
        /// <param name="cToken">A cancellation token to stop the cleanup task.</param>
        public async Task StartAutoCleanup(TimeSpan? interval = null, CancellationToken? cToken = null)
        {
            try
            {
                if (!this.CleanupSwitch.TryOpen())
                {
                    try
                    {
                        if (this.Cts != null && !this.Cts.IsCancellationRequested)
                            this.Cts?.Cancel();
                    }
                    catch { }
                }

                this.Cts = cToken.HasValue ?
                    CancellationTokenSource.CreateLinkedTokenSource(cToken.Value)
                    : new CancellationTokenSource();
                //if (cToken is null) this.Cts = new CancellationTokenSource();
                //else this.Cts = CancellationTokenSource.CreateLinkedTokenSource((CancellationToken)cToken);

                if (interval == null) interval = TimeSpan.FromSeconds(1);

                if (interval.HasValue && interval.Value.TotalMilliseconds < 100)
                    throw new ArgumentOutOfRangeException(nameof(interval), "Interval must be at least 100 milliseconds.");


                while (!this.Cts.Token.IsCancellationRequested)
                {
                    await Task.Delay(interval.Value, this.Cts.Token);
                    this.ClearExpired();
                }
            }
            catch (TaskCanceledException)
            {
                // Task was cancelled
            }
            finally
            {
                _ = this.CleanupSwitch.TryClose();
            }
        }

        public void Dispose()
        {
            try
            {
                this.Cts?.Cancel();
                this.ClearExpired();
                GC.SuppressFinalize(this);
            }
            catch { }
        }
    }
}
