using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Com.H.Collections.Generic
{



    public static class Extensions
    {
        /// <summary>
        /// Takes an enumerable, fetches only the first 
        /// enumerable item (or items depending on the chamber size), 
        /// to force enumerable evaluation 
        /// (which helps in executing Link-to-SQL queries) 
        /// without iterating through all the enumerator 
        /// items, then returns the first fetched item (or the first 
        /// chamber size items) and stitches it along with the 
        /// remaining items back as an enumerable
        /// </summary>
        /// <param name="enumerable"></param>
        /// <param name="chamberSize"></param>
        /// <returns>A ChamberedEnumerable that implements IEnumerable and provides chambered count information</returns>
        public static ChamberedEnumerable<dynamic> ToChamberedEnumerable(
            this IEnumerable<dynamic>? enumerable,
            int chamberSize = 1)
        {
            if (enumerable is null)
            {
                return new ChamberedEnumerable<dynamic>(Enumerable.Empty<dynamic>(), 0);
            }
            if (chamberSize < 1)
                chamberSize = 1;
            
            var enumerator = enumerable.GetEnumerator();
            var takenItems = new List<dynamic>();
            
            // Take the first 'chamberSize' items
            for (int i = 0; i < chamberSize && enumerator.MoveNext(); i++)
            {
                takenItems.Add(enumerator.Current);
            }
            
            // If we took fewer items than requested, the enumerable was exhausted
            if (takenItems.Count < chamberSize)
            {
                return new ChamberedEnumerable<dynamic>(takenItems, takenItems.Count);
            }
            
            // Return the taken items concatenated with remaining items
            var result = Enumerable.Concat(takenItems, enumerator.RemainingItems());
            return new ChamberedEnumerable<dynamic>(result, takenItems.Count);
        }

        /// <summary>
        /// Takes an async enumerable, fetches only the first 
        /// enumerable item (or items depending on the chamber size), 
        /// to force enumerable evaluation 
        /// (which helps in executing async database queries) 
        /// without iterating through all the enumerator 
        /// items, then returns the first fetched item (or the first 
        /// chamber size items) and stitches it along with the 
        /// remaining items back as an async enumerable
        /// </summary>
        /// <param name="asyncEnumerable"></param>
        /// <param name="chamberSize"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>A ChamberedAsyncEnumerable that implements IAsyncEnumerable and provides chambered count information</returns>
        public static async Task<ChamberedAsyncEnumerable<dynamic>> ToChamberedEnumerableAsync(
            this IAsyncEnumerable<dynamic>? asyncEnumerable,
            int chamberSize = 1,
            CancellationToken cancellationToken = default)
        {
            if (asyncEnumerable is null)
            {
                return new ChamberedAsyncEnumerable<dynamic>(EmptyAsyncEnumerable(), 0);
            }
            if (chamberSize < 1)
                chamberSize = 1;
            
            var enumerator = asyncEnumerable.GetAsyncEnumerator(cancellationToken);
            var takenItems = new List<dynamic>();
            
            // Take the first 'chamberSize' items
            for (int i = 0; i < chamberSize && await enumerator.MoveNextAsync(); i++)
            {
                takenItems.Add(enumerator.Current);
            }
            
            // If we took fewer items than requested, the enumerable was exhausted
            if (takenItems.Count < chamberSize)
            {
                return new ChamberedAsyncEnumerable<dynamic>(ToAsyncEnumerable(takenItems), takenItems.Count);
            }
            
            // Return the taken items concatenated with remaining items
            var result = ConcatAsyncEnumerables(ToAsyncEnumerable(takenItems), enumerator.RemainingItemsAsync());
            return new ChamberedAsyncEnumerable<dynamic>(result, takenItems.Count);
        }

        /// <summary>
        /// Takes an enumerator and returns the rest of the 
        /// items as enumerable
        /// </summary>
        /// <param name="enumerator"></param>
        /// <returns></returns>
        public static IEnumerable<dynamic> RemainingItems(
            this IEnumerator<dynamic>? enumerator)
        {
            if (enumerator is not null)
            {
                while (enumerator.MoveNext())
                {
                    yield return enumerator.Current;
                }
            }
        }

        /// <summary>
        /// Takes an async enumerator and returns the rest of the 
        /// items as async enumerable
        /// </summary>
        /// <param name="enumerator"></param>
        /// <returns></returns>
        public static async IAsyncEnumerable<dynamic> RemainingItemsAsync(
            this IAsyncEnumerator<dynamic>? enumerator)
        {
            if (enumerator is not null)
            {
                while (await enumerator.MoveNextAsync())
                {
                    yield return enumerator.Current;
                }
            }
        }

        /// <summary>
        /// Creates an empty async enumerable
        /// </summary>
        /// <returns></returns>
        private static async IAsyncEnumerable<dynamic> EmptyAsyncEnumerable()
        {
            yield break;
        }

        /// <summary>
        /// Converts a regular enumerable to an async enumerable
        /// </summary>
        /// <param name="enumerable"></param>
        /// <returns></returns>
        private static async IAsyncEnumerable<dynamic> ToAsyncEnumerable(IEnumerable<dynamic> enumerable)
        {
            foreach (var item in enumerable)
            {
                yield return item;
            }
        }

        /// <summary>
        /// Concatenates two async enumerables
        /// </summary>
        /// <param name="first"></param>
        /// <param name="second"></param>
        /// <returns></returns>
        private static async IAsyncEnumerable<dynamic> ConcatAsyncEnumerables(
            IAsyncEnumerable<dynamic> first, 
            IAsyncEnumerable<dynamic> second)
        {
            await foreach (var item in first)
            {
                yield return item;
            }
            await foreach (var item in second)
            {
                yield return item;
            }
        }
    }
}
