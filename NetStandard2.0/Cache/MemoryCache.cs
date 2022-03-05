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
    /// Thread-safe memory cache
    /// </summary>
    public class MemoryCache : IDisposable
    {
        private CancellationTokenSource Cts { get; set; }
        private AtomicGate CleanupSwitch { get; set; } = new AtomicGate();
        internal class CacheItem
        {
            internal DateTime ExpiryDate { get; set; }
            internal object Value { get; set; }
            internal bool Expired
            {
                get => DateTime.Now >= this.ExpiryDate;
            }

        }
        private readonly ConcurrentDictionary<object, Lazy<CacheItem>> cacheItems = new ConcurrentDictionary<object, Lazy<CacheItem>>();

        public T Get<T>(object key, Func<T> getValue, TimeSpan? timeSpan = null) 
            => (T)this.cacheItems.AddOrUpdate(key,
                _ => new Lazy<CacheItem>(()=> new CacheItem()
                {
                    ExpiryDate = (timeSpan == null ? DateTime.Today.AddDays(1)
                    : DateTime.Now.Add((TimeSpan)timeSpan)),
                    Value = getValue == null ? default : getValue()
                }),
                (_, oldLazyValue) =>
                {
                    var oldValue = oldLazyValue.Value;
                    if (!oldValue.Expired) return oldLazyValue;
                    return new Lazy<CacheItem>(() =>
                    {
                        oldValue.Value = getValue == null ? default : getValue();
                        oldValue.ExpiryDate = (timeSpan == null ? DateTime.Today.AddDays(1)
                         : DateTime.Now.Add((TimeSpan)timeSpan));
                        return oldValue;
                    });
                }).Value.Value;
        /// <summary>
        /// Manually clear expired cache items
        /// </summary>
        public void ClearExpired()
        {
            foreach (var item in this.cacheItems.Where(x => x.Value.Value.Expired))
                this.cacheItems.TryRemove(item.Key, out _);
        }

        /// <summary>
        /// Starts automatic cache cleanup thread.
        /// </summary>
        /// <param name="cToken">Cancellation token to stop the automatic cleanup, if none provided, the cleanup thread stops when the MemoryCache instance runs out of scope / disposed</param>
        /// <param name="interval">How often to check the expired cache items for cleanup. Default (if omitted) is 1 second</param>
        /// <returns></returns>
        public async Task StartAutoCleanup(TimeSpan? interval = null, CancellationToken? cToken = null)
        {
            try
            { 
                if (!this.CleanupSwitch.TryOpen())
                {
                    try
                    {
                        this.Cts?.Cancel();
                    }
                    catch { }
                }
                if (cToken is null) this.Cts = new CancellationTokenSource();
                else this.Cts = CancellationTokenSource.CreateLinkedTokenSource((CancellationToken)cToken);
                if (interval is null) interval = TimeSpan.FromSeconds(1);
                if (interval.Value.TotalMilliseconds > int.MaxValue)
                    throw new ArgumentOutOfRangeException($"{nameof(interval)} shouldn't exceed {int.MaxValue} miliseconds");
                while (!this.Cts.Token.IsCancellationRequested)
                {
                    await Task.Delay((int)interval.Value.TotalMilliseconds, this.Cts.Token);
                    this.ClearExpired();
                }
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
                this.ClearExpired();
                this.Cts?.Cancel();
                GC.SuppressFinalize(this);
            }
            catch { }
        }
    }
}
