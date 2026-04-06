using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Com.H.Collections.Generic;

namespace Com.H.Collections.Concurrent
{
    /// <summary>
    /// A thread-safe sorted dictionary.
    /// The implementation at this moment doesn't offer the same level of thread-safety as the ConcurrentDictionary where
    /// it allows you to add or update multiple keys at the same time.
    /// This is a simple implementation that allows you to add or update a single key at a time in a thread-safe manner.
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public class ConcurrentLimitedSortedDictionary<TKey, TValue> : IReadOnlyDictionary<TKey, TValue> where TKey : IComparable<TKey>
    {
        private readonly LimitedSortedDictionary<TKey, TValue> _dic;
        private readonly ReaderWriterLockSlim _lock = new();

        public ConcurrentLimitedSortedDictionary(int limit)
        {
            if (limit <= 0)
            {
                throw new ArgumentException("Limit must be greater than 0", nameof(limit));
            }

            this._dic = new LimitedSortedDictionary<TKey, TValue>(limit);
        }

        public ConcurrentLimitedSortedDictionary(int limit, IComparer<TKey> comparer)
        {
            if (limit <= 0)
            {
                throw new ArgumentException("Limit must be greater than 0", nameof(limit));
            }
            this._dic = new LimitedSortedDictionary<TKey, TValue>(limit, comparer);
        }
        public ConcurrentLimitedSortedDictionary(
            int limit,
            IEnumerable<KeyValuePair<TKey, TValue>> keyValuePairs)
        {
            if (limit <= 0)
            {
                throw new ArgumentException("Limit must be greater than 0", nameof(limit));
            }
            this._dic = new LimitedSortedDictionary<TKey, TValue>(
                limit,
                keyValuePairs.ToDictionary(x => x.Key, x => x.Value)
                );
        }

        public ConcurrentLimitedSortedDictionary(
            int limit,
            IEnumerable<KeyValuePair<TKey, TValue>> keyValuePairs,
            IComparer<TKey> comparer)

        {
            if (limit <= 0)
            {
                throw new ArgumentException("Limit must be greater than 0", nameof(limit));
            }
            this._dic = new LimitedSortedDictionary<TKey, TValue>(
                limit,
                keyValuePairs.ToDictionary(x => x.Key, x => x.Value), comparer);
        }


        public TValue this[TKey key]
        {
            get
            {
                this._lock.EnterReadLock();
                try
                {
                    if (key is null
                        || !this._dic.ContainsKey(key)
                        ) return default!;
                    this._dic.TryGetValue(key, out var v);
                    return v!;
                }
                finally
                {
                    this._lock.ExitReadLock();
                }
            }
            set
            {
                if (key is null) return;
                this._lock.EnterWriteLock();
                try
                {
                    if (this._dic.ContainsKey(key))
                    {
                        this._dic[key] = value;
                    }
                    else
                    {
                        this._dic.Add(key, value);
                    }
                }
                finally
                {
                    this._lock.ExitWriteLock();
                }
            }
        }

        public IEnumerable<TKey> Keys => this._dic.Keys;

        public IEnumerable<TValue> Values => this._dic.Values;

        public int Count => this._dic.Count;

        public bool ContainsKey(TKey key)
        {
            this._lock.EnterReadLock();
            try
            {
                return this._dic.ContainsKey(key);
            }
            finally
            {
                this._lock.ExitReadLock();
            }
        }

        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
        {
            this._lock.EnterReadLock();
            try
            {
                return this._dic.TryGetValue(key, out value);
            }
            finally
            {
                this._lock.ExitReadLock();
            }
        }
        /// <summary>
        /// This isn't thread-safe in the sense that it doesn't lock the dictionary while enumerating.
        /// So you need to be mindful of that when using this method.
        /// </summary>
        /// <returns></returns>
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            this._lock.EnterReadLock();
            try
            {
                return this._dic.GetEnumerator();
            }
            finally
            {
                this._lock.ExitReadLock();
            }
        }

        /// <summary>
        /// This isn't thread-safe in the sense that it doesn't lock the dictionary while enumerating.
        /// So you need to be mindful of that when using this method.
        /// </summary>
        /// <returns></returns>
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        public void Clear()
        {
            this._lock.EnterWriteLock();
            try
            {
                this._dic.Clear();
            }
            finally
            {
                this._lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Adds a key/value pair to the ConcurrentSortedDictionary if the key does not already exist, 
        /// or updates a key/value pair in the ConcurrentSortedDictionary by using the specified function if the key already exists.
        /// </summary>
        /// <param name="key">The key to be added or whose value should be updated</param>
        /// <param name="value">The value to be added or updated</param>
        /// <param name="updateValueFactory">The function used to generate a new value for an existing key based on the key's existing value</param>
        /// <returns>The new value for the key. 
        /// This will be either be the value passed in the value parameter if the key is new, 
        /// or the result of the updateValueFactory if the key already exists.
        /// </returns>
        public TValue AddOrUpdate(TKey key, TValue value, Func<TKey, TValue, TValue> updateValueFactory)
        {
            this._lock.EnterWriteLock();
            try
            {
                if (this._dic.ContainsKey(key))
                {
                    this._dic[key] = updateValueFactory(key, this._dic[key]);
                    return this._dic[key];
                }
                else
                {
                    this._dic.Add(key, value);
                    return value;
                }
            }
            finally
            {
                this._lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Adds or updates the dictionary.
        /// </summary>
        /// <param name="key">The key to add or update.</param>
        /// <param name="addValueFactory">The add value factory.</param>
        /// <param name="updateValueFactory">The update value factory.
        /// The update value factory takes the key and the old value and returns the new value.
        /// </param>
        /// <returns>The new value for the key.</returns>
        public TValue AddOrUpdate(TKey key,
            Func<TKey, TValue> addValueFactory,
            Func<TKey, TValue, TValue> updateValueFactory)
        {
            this._lock.EnterWriteLock();
            try
            {
                if (this._dic.ContainsKey(key))
                {
                    this._dic[key] = updateValueFactory(key, this._dic[key]);
                    return this._dic[key];
                }
                else
                {
                    var value = addValueFactory(key);
                    this._dic.Add(key, value);
                    return value;
                }
            }
            finally
            {
                this._lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Adds or updates the dictionary.
        /// </summary>
        /// <typeparam name="TArg">The type of the factory argument.</typeparam>
        /// <param name="key">The key to add or update.</param>
        /// <param name="addValueFactory">The add value factory.</param>
        /// <param name="updateValueFactory">The update value factory.
        /// The update value factory takes the key, the old value, and the factory argument and returns the new value.
        /// The factory argument is passed to the add value factory and the update value factory.
        /// </param>
        /// <param name="factoryArgument">The factory argument.
        /// Helpful when the add value factory and the update value factory need additional information to create the value.
        /// This is the value that is going to be passed to both the add value factory and the update value factory.
        /// E.g., consider the following add factory implementation where `arg` is a factory argument of type `string`:
        /// Func<string, string?, string?> addFactory = (key, arg) => $"{key} {arg}";
        /// and update factory implementation where `arg` is a factory argument of type `string`:
        /// Func<string, string?, string?, string?> updateFactory = (key, oldValue, arg) => $"{oldValue} {arg}";
        /// when calling AddOrUpdate with the key "key" and the factory argument "abc 123":
        /// dict.AddOrUpdate("key", addFactory, updateFactory, "abc 123");
        /// the add factory will be called with the key "key" and the factory argument "abc 123" which will return "key abc 123".
        /// And the update factory will be called with the key "key", the old value (if it exists), and the factory argument "abc 123".
        /// </param>
        /// <returns>The new value for the key.</returns>
        public TValue AddOrUpdate<TArg>(TKey key,
            Func<TKey, TArg, TValue> addValueFactory,
            Func<TKey, TValue, TArg, TValue> updateValueFactory,
            TArg factoryArgument
            )
        {
            this._lock.EnterWriteLock();
            try
            {
                if (this._dic.ContainsKey(key))
                {
                    this._dic[key] = updateValueFactory(key, this._dic[key], factoryArgument);
                    return this._dic[key];
                }
                else
                {
                    var value = addValueFactory(key, factoryArgument);
                    this._dic.Add(key, value);
                    return value;
                }
            }
            finally
            {
                this._lock.ExitWriteLock();
            }
        }

        public bool TryRemove(TKey key, [MaybeNullWhen(false)] out TValue value)
        {
            this._lock.EnterWriteLock();
            try
            {
                if (this._dic.TryGetValue(key, out value))
                {
                    this._dic.Remove(key);
                    return true;
                }
                return false;
            }
            finally
            {
                this._lock.ExitWriteLock();
            }
        }

        public bool TryAdd(TKey key, TValue value)
        {
            this._lock.EnterWriteLock();
            try
            {
                if (this._dic.ContainsKey(key)) return false;
                this._dic.Add(key, value);
                return true;
            }
            finally
            {
                this._lock.ExitWriteLock();
            }
        }

    }

}