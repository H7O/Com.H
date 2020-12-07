using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Com.H.Linq
{
    public static class JoinExtensions
    {

        /// <summary>
        /// Performs a full outter join between two IEnumerable objects
        /// </summary>
        /// <typeparam name="TOuter"></typeparam>
        /// <typeparam name="TInner"></typeparam>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="outer"></param>
        /// <param name="inner"></param>
        /// <param name="outerKeySelector"></param>
        /// <param name="innerKeySelector"></param>
        /// <param name="resultSelector"></param>
        /// <returns></returns>
        public static IEnumerable<TResult> FullOuterJoin<TOuter, TInner, TKey, TResult>(
            this IEnumerable<TOuter> outer,
            IEnumerable<TInner> inner,
            Func<TOuter, TKey> outerKeySelector,
            Func<TInner, TKey> innerKeySelector,
            Func<TOuter, TInner, TResult> resultSelector)
        {
            var innerLookup = inner.ToLookup(innerKeySelector);
            var outerLookup = outer.ToLookup(outerKeySelector);

            var innerJoinItems = inner
                .Where(innerItem => !outerLookup.Contains(innerKeySelector(innerItem)))
                .Select(innerItem => resultSelector(default(TOuter), innerItem));

            return outer
                .SelectMany(outerItem =>
                {
                    var innerItems = innerLookup[outerKeySelector(outerItem)];

                    return innerItems.Any() ? innerItems : new TInner[] { default(TInner) };
                }, resultSelector)
                .Concat(innerJoinItems);
        }

        /// <summary>
        /// Performs a left join between two IEnumerable
        /// </summary>
        /// <typeparam name="TOuter"></typeparam>
        /// <typeparam name="TInner"></typeparam>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="outer"></param>
        /// <param name="inner"></param>
        /// <param name="outerKeySelector"></param>
        /// <param name="innerKeySelector"></param>
        /// <param name="resultSelector"></param>
        /// <returns></returns>
        public static IEnumerable<TResult> LeftJoin<TOuter, TInner, TKey, TResult>(
            this IEnumerable<TOuter> outer,
            IEnumerable<TInner> inner,
            Func<TOuter, TKey> outerKeySelector,
            Func<TInner, TKey> innerKeySelector,
            Func<TOuter, TInner, TResult> resultSelector)
        {
            return outer.GroupJoin(
                inner,
                outerKeySelector,
                innerKeySelector,
                (o, i) =>
                    new { o, i = i.DefaultIfEmpty() })
                    .SelectMany(m => m.i.Select(inn =>
                        resultSelector(m.o, inn)
                        ));

        }


        /// <summary>
        /// Performs a right join between two IEnumerable objects
        /// </summary>
        /// <typeparam name="TOuter"></typeparam>
        /// <typeparam name="TInner"></typeparam>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="outer"></param>
        /// <param name="inner"></param>
        /// <param name="outerKeySelector"></param>
        /// <param name="innerKeySelector"></param>
        /// <param name="resultSelector"></param>
        /// <returns></returns>
        public static IEnumerable<TResult> RightJoin<TOuter, TInner, TKey, TResult>(
            this IEnumerable<TOuter> outer,
            IEnumerable<TInner> inner,
            Func<TOuter, TKey> outerKeySelector,
            Func<TInner, TKey> innerKeySelector,
            Func<TOuter, TInner, TResult> resultSelector)
        {
            return inner.GroupJoin(
                outer,
                innerKeySelector,
                outerKeySelector,
                (i, o) =>
                    new { i, o = o.DefaultIfEmpty() })
                    .SelectMany(m => m.o.Select(outt =>
                        resultSelector(outt, m.i)
                        ));

        }


        public static Dictionary<TKey, TVal> Merge<TKey, TVal>(this Dictionary<TKey, TVal> first, Dictionary<TKey, TVal> second)
        {
            return first.Union(second).ToDictionary(k => k.Key, v => v.Value);
        }

    }
}
