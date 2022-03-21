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
            => orderedFilter.Join(dictionary, o => o, d => d.Key, (o, d) => d.Value);

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

        /// <summary>
        /// Find and return an item within a hierarchical tree structure by traversing it's child elements using a string
        /// path that denotes its tree elements seperated by a pre-defined string delimiter.
        /// </summary>
        /// <typeparam name="T">Traversable object</typeparam>
        /// <param name="traversableItem">An item that carries children of the same type as itself</param>
        /// <param name="path">A string path representing the decendants tree seperated by a delimiter</param>
        /// <param name="findChild">A delegate that takes a parent element and tries to find a direct decendant wihin its children that corresponds to the child path sub-string</param>
        /// <param name="pathDelimiters">Delimieters to be used to distinguish between decendant elements in the path string</param>
        /// <returns></returns>
        public static T FindDescendant<T>(
            this T traversableItem,
            string path,
            Func<T, string, T> findChild,
            string[] pathDelimiters
            )
            => string.IsNullOrEmpty(path)
                || EqualityComparer<T>.Default.Equals(traversableItem, default)
                || findChild is null
                || pathDelimiters == null
                || pathDelimiters.Length < 1
                ? default
            : path.Split(pathDelimiters,
                StringSplitOptions.RemoveEmptyEntries)
                .Aggregate(traversableItem, (i, n) =>
                   i == null
                   || EqualityComparer<T>.Default.Equals(i, default) ?
                   default
                   :
                   findChild(i, n)
                );

        /// <summary>
        /// Find and return an item within a hierarchical tree structure by traversing it's child elements using a string
        /// path that denotes its tree elements seperated by a pre-defined string delimiter.
        /// </summary>
        /// <typeparam name="T">Traversable object</typeparam>
        /// <param name="traversableItem">An item that carries children of the same type as itself</param>
        /// <param name="path">A string path representing the decendants tree seperated by a delimiter</param>
        /// <param name="findChild">A delegate that takes a parent element and tries to find a direct decendant wihin its children that corresponds to the child path sub-string</param>
        /// <param name="pathDelimiters">Delimieters to be used to distinguish between decendant elements in the path string</param>
        /// <returns></returns>
        public static T FindDescendant<T>(this T traversableItem,
            string path,
            Func<T, string, T> findChild,
            char[] pathDelimiters)
            => string.IsNullOrEmpty(path)
                || EqualityComparer<T>.Default.Equals(traversableItem, default)
                || findChild is null
                || pathDelimiters == null
                || pathDelimiters.Length < 1
                ? default
            : path.Split(pathDelimiters,
                StringSplitOptions.RemoveEmptyEntries)
                .Aggregate(traversableItem, (i, n) =>
                   i == null
                   || EqualityComparer<T>.Default.Equals(i, default) ?
                   default
                   :
                   findChild(i, n)
                );



        /// <summary>
        /// Find and return item(s) within a hierarchical tree structure by traversing it's child elements using a string
        /// path that denotes its tree elements seperated by a pre-defined string delimiter.
        /// </summary>
        /// <typeparam name="T">Traversable object</typeparam>
        /// <param name="traversableItem">An item that carries children of the same type as itself</param>
        /// <param name="path">A string path representing the decendants tree seperated by a delimiter</param>
        /// <param name="findChildren">A delegate that takes a parent element and tries to find direct decendants wihin its children that corresponds to the child path sub-string</param>
        /// <param name="pathDelimiters">Delimieters to be used to distinguish between decendant elements in the path string</param>

        public static IEnumerable<T> FindDescendants<T>(
            this T traversableItem,
            string path,
            Func<T, string, IEnumerable<T>> findChildren,
            string[] pathDelimiters
            ) =>
            string.IsNullOrEmpty(path)
                  || EqualityComparer<T>.Default.Equals(traversableItem, default)
                  || findChildren is null
                  || pathDelimiters == null
                  || pathDelimiters.Length < 1
                  ? default
              : path.Split(pathDelimiters,
                  StringSplitOptions.RemoveEmptyEntries)
                  .Aggregate(Enumerable.Empty<T>().Append(traversableItem), (i, n) =>
                      i is null
                      ?
                      null
                      :
                       i?.Where(x => x is not null && !EqualityComparer<T>.Default.Equals(x, default))
                           .SelectMany(x => x is null ? null : findChildren(x, n))
                       .Where(x => x != null && !EqualityComparer<T>.Default.Equals(x, default))
                  );


        /// <summary>
        /// Find and return item(s) within a hierarchical tree structure by traversing it's child elements using a string
        /// path that denotes its tree elements seperated by a pre-defined string delimiter.
        /// </summary>
        /// <typeparam name="T">Traversable object</typeparam>
        /// <param name="traversableItem">An item that carries children of the same type as itself</param>
        /// <param name="path">A string path representing the decendants tree seperated by a delimiter</param>
        /// <param name="findChildren">A delegate that takes a parent element and tries to find direct decendants wihin its children that corresponds to the child path sub-string</param>
        /// <param name="pathDelimiters">Delimieters to be used to distinguish between decendant elements in the path string</param>

        public static IEnumerable<T> FindDescendants<T>(
            this T traversableItem,
            string path,
            Func<T, string, IEnumerable<T>> findChildren,
            char[] pathDelimiters
            ) =>
            string.IsNullOrEmpty(path)
                  || EqualityComparer<T>.Default.Equals(traversableItem, default)
                  || findChildren is null
                  || pathDelimiters == null
                  || pathDelimiters.Length < 1
                  ? default
              : path.Split(pathDelimiters,
                  StringSplitOptions.RemoveEmptyEntries)
                  .Aggregate(Enumerable.Empty<T>().Append(traversableItem), (i, n) =>
                      i is null
                      ?
                      null
                      :
                       i?.Where(x => x is not null && !EqualityComparer<T>.Default.Equals(x, default))
                           .SelectMany(x => x is null ? null : findChildren(x, n))
                       .Where(x => x != null && !EqualityComparer<T>.Default.Equals(x, default))
                  );


    }
}
