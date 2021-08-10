using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Com.H.Cache
{
    /// <summary>
    /// Thread-safe memory cache
    /// </summary>
    public class MemoryCache
    {
        internal class CacheItem
        {
            internal DateTime ExpiryDate { get; set; }
            internal object Value { get; set; }
            internal bool Expired
            {
                get => DateTime.Now >= this.ExpiryDate;
            }

        }
        private readonly ConcurrentDictionary<object, CacheItem> cacheItems = new();

        public T Get<T>(object key, Func<T> getValue, TimeSpan? timeSpan = null)
            => (T)this.cacheItems.AddOrUpdate(key,
                _ => new CacheItem()
                {
                    ExpiryDate = (timeSpan == null ? DateTime.Today.AddDays(1)
                    : DateTime.Now.Add((TimeSpan)timeSpan)),
                    Value = getValue == null ? default : getValue()
                },
                (_, oldValue) =>
                {
                    if (!oldValue.Expired) return oldValue;
                    oldValue.Value = getValue == null ? default : getValue();
                    oldValue.ExpiryDate = (timeSpan == null ? DateTime.Today.AddDays(1)
                     : DateTime.Now.Add((TimeSpan)timeSpan));
                    return oldValue;
                }).Value;

        public void ClearExpired()
        {
            foreach (var item in this.cacheItems.Where(x => x.Value.Expired))
                this.cacheItems.TryRemove(item.Key, out _);
        }

        //public T Get<T>(object key, Func<T> getValue, TimeSpan? timeSpan = null)
        //    => (T)this.Get(key, getValue, timeSpan);




    }
}
