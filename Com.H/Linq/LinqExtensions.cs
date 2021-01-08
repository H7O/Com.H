using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Com.H.Linq
{
    public static class LinqExtensions
    {
        /// <summary>
        /// Filter a dictionary based on the passed keys in orderedFilter argument.
        /// Then return the values those keys represents ordered in the order of the given orderFilter keys
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="dictionary"></param>
        /// <param name="orderedFilter"></param>
        /// <returns></returns>
        public static IEnumerable<TValue> OrdinalFilter<TKey, TValue>(
            this IDictionary<TKey, TValue> dictionary, IEnumerable<TKey> orderedFilter)
            => orderedFilter == null ? null
            : orderedFilter.Join(dictionary, o => o, d => d.Key, (o, d) => d.Value);

        /// <summary>
        /// Encloses a signle item into an Enumerable of its type, then returns the resulting Enumerable.
        /// Alternatively, if the object passed is already an Enumerable, it just returns the Enumerable back as is.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static IEnumerable<T> EnsureEnumerable<T>(this object obj)
            =>
                typeof(IEnumerable<T>).IsAssignableFrom(obj.GetType())
                            ? (IEnumerable<T>)obj
                            : Enumerable.Empty<T>().Append((T)obj);

        /// <summary>
        /// Encloses a signle item into an Enumerable of dynamic, then returns the resulting Enumerable.
        /// Alternatively, if the object passed is already an Enumerable, it just returns the Enumerable back as is.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static IEnumerable<dynamic> EnsureEnumerable(this object obj)
             => EnsureEnumerable<dynamic>(obj);

    }
}
