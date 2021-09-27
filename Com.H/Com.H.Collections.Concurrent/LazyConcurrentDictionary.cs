using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Com.H.Collections.Concurrent
{
    public class LazyConcurrentDictionary<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>
    {
        private readonly ConcurrentDictionary<TKey, Lazy<TValue>> _dic = new ConcurrentDictionary<TKey, Lazy<TValue>>();

        public LazyConcurrentDictionary(IEnumerable<KeyValuePair<TKey, TValue>> keyValuePairs)
        => this._dic = new ConcurrentDictionary<TKey, Lazy<TValue>>(
            keyValuePairs.Select(x =>
                    new KeyValuePair<TKey, Lazy<TValue>>(x.Key, new Lazy<TValue>(x.Value))
            ));

        public TValue this[TKey key]
        {
            get
            {
                if (key is null
                    || !this._dic.ContainsKey(key)
                    ) return default;
                this._dic.TryGetValue(key, out var lv);
                return lv is null ? default : lv.Value;
            }
            set
            {
                if (key is null) return;
                _ = this.AddOrUpdate(key, value, (_, oldItem) => value);
            }
        }

        public IEnumerable<TKey> Keys => this._dic.Keys;

        public IEnumerable<TValue> Values => this._dic.Values.Select(x => x.Value);

        public int Count => this._dic.Count;

        public TValue AddOrUpdate(TKey key, TValue value, Func<TKey, TValue, TValue> updateValueFactory)
        {
            return this._dic.AddOrUpdate(
                key,
                new Lazy<TValue>(value),
                (key, oldItem) => new Lazy<TValue>(() => updateValueFactory(key, oldItem.Value)))
                .Value;
        }

        public TValue AddOrUpdate(TKey key,
            Func<TKey, TValue> addValueFactory,
            Func<TKey, TValue, TValue> updateValueFactory)
        {
            return this._dic.AddOrUpdate(
                key,
                new Lazy<TValue>(() => addValueFactory(key)),
                (key, oldItem) => new Lazy<TValue>(() => updateValueFactory(key, oldItem.Value)))
                .Value;
        }

        public TValue AddOrUpdate<TArg>(TKey key,
            Func<TKey, TArg, TValue> addValueFactory,
            Func<TKey, TValue, TArg, TValue> updateFactory,
            TArg factoryArgument
            )
        {
            return this._dic.AddOrUpdate(
                key,
                new Lazy<TValue>(() => addValueFactory(key, factoryArgument)),
                (key, oldItem) => new Lazy<TValue>(() => updateFactory(key, oldItem.Value, factoryArgument)))
                .Value;
        }

        public bool ContainsKey(TKey key)
            =>
            this._dic.ContainsKey(key);

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
            =>
            this._dic.Select(x => new KeyValuePair<TKey, TValue>(x.Key, x.Value.Value))
                .GetEnumerator();


        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
        {
            Lazy<TValue> outValue;
            if (this._dic.TryGetValue(key, out outValue))
            {
                value = outValue.Value;
                return true;
            }
            value = default;
            return false;
        }

        public bool TryRemove(TKey key, out TValue value)
        {
            Lazy<TValue> outValue;
            if (this._dic.TryRemove(key, out outValue))
            {
                value = outValue.Value;
                return true;
            }
            value = default;
            return false;
        }

        IEnumerator IEnumerable.GetEnumerator()
            =>
            this._dic.Select(x => new KeyValuePair<TKey, TValue>(x.Key, x.Value.Value))
                .GetEnumerator();
    }
}
