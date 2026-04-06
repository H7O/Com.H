using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
namespace Com.H.Collections.Generic
{
    /// <summary>
    /// A SortedDictionary that has a limit on the number of items it can store.
    /// If an item is added that would exceed the limit, 
    /// the item with the highest key (in respect to the comparer) is removed.
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public class LimitedSortedDictionary<TKey, TValue> : IReadOnlyDictionary<TKey, TValue> where TKey : IComparable<TKey>
    {
        private SortedDictionary<TKey, TValue> _sortedDictionary;
        private int _limit;

        public LimitedSortedDictionary(int limit)
        {
            if (limit <= 0)
            {
                throw new ArgumentException("Limit must be greater than 0", nameof(limit));
            }

            _limit = limit;
            _sortedDictionary = new SortedDictionary<TKey, TValue>();
        }
        public LimitedSortedDictionary(int limit, IComparer<TKey> comparer)
        {
            if (limit <= 0)
            {
                throw new ArgumentException("Limit must be greater than 0", nameof(limit));
            }

            _limit = limit;
            _sortedDictionary = new SortedDictionary<TKey, TValue>(comparer);
        }

        public LimitedSortedDictionary(
            int limit,
            IEnumerable<KeyValuePair<TKey, TValue>> keyValuePairs)
        {
            if (limit <= 0)
            {
                throw new ArgumentException("Limit must be greater than 0", nameof(limit));
            }
            this._sortedDictionary = new SortedDictionary<TKey, TValue>(
                keyValuePairs.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                );
        }

        public LimitedSortedDictionary(
            int limit,
            IEnumerable<KeyValuePair<TKey, TValue>> keyValuePairs,
            IComparer<TKey> comparer)
        {
            if (limit <= 0)
            {
                throw new ArgumentException("Limit must be greater than 0", nameof(limit));
            }
            this._sortedDictionary = new SortedDictionary<TKey, TValue>(
                           keyValuePairs
                           .Take(limit)
                           .ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                            comparer);
        }



        public void Add(TKey key, TValue value)
        {
            if (_sortedDictionary.ContainsKey(key))
            {
                // Update the value if the key already exists
                _sortedDictionary[key] = value;
                return;
            }

            if (_sortedDictionary.Count < _limit)
            {
                _sortedDictionary.Add(key, value);
            }
            else
            {
                TKey lastKey = _sortedDictionary.Keys.Last();
                if (key.CompareTo(lastKey) < 0)
                {
                    // Remove the last item and add the new item
                    _sortedDictionary.Remove(lastKey);
                    _sortedDictionary.Add(key, value);
                }
                // Otherwise, ignore the new item as it would be after the limit
            }
        }

        public bool Remove(TKey key)
        {
            return _sortedDictionary.Remove(key);
        }

        public bool ContainsKey(TKey key)
        {
            return ((IReadOnlyDictionary<TKey, TValue>)_sortedDictionary).ContainsKey(key);
        }

        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
        {
            return ((IReadOnlyDictionary<TKey, TValue>)_sortedDictionary).TryGetValue(key, out value);
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return ((IEnumerable<KeyValuePair<TKey, TValue>>)_sortedDictionary).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)_sortedDictionary).GetEnumerator();
        }

        public TValue this[TKey key]
        {
            get { return _sortedDictionary[key]; }
            set { Add(key, value); }
        }

        public IEnumerable<TKey> Keys => _sortedDictionary.Keys;
        public IEnumerable<TValue> Values => _sortedDictionary.Values;
        public int Count => _sortedDictionary.Count;

        public void Clear()
        {
            _sortedDictionary.Clear();
        }
    }

}