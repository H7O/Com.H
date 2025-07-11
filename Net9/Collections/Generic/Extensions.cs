using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        /// <returns></returns>
        public static IEnumerable<dynamic> ToChamberedEnumerable(
            this IEnumerable<dynamic>? enumerable,
            int chamberSize = 1)
        {
            var (result, _) = enumerable.ToChamberedEnumerableWithCount(chamberSize);
            return result;
        }

        /// <summary>
        /// Takes an enumerable, fetches only the first 
        /// enumerable item (or items depending on the chamber size), 
        /// to force enumerable evaluation 
        /// (which helps in executing Link-to-SQL queries) 
        /// without iterating through all the enumerator 
        /// items, then returns the first fetched item (or the first 
        /// chamber size items) and stitches it along with the 
        /// remaining items back as an enumerable, along with the actual count chambered
        /// </summary>
        /// <param name="enumerable"></param>
        /// <param name="chamberSize"></param>
        /// <returns>A tuple containing the enumerable and the actual count of items chambered</returns>
        public static (IEnumerable<dynamic> Result, int ChamberedCount) ToChamberedEnumerableWithCount(
            this IEnumerable<dynamic>? enumerable,
            int chamberSize = 1)
        {
            if (enumerable is null)
            {
                return (Enumerable.Empty<dynamic>(), 0);
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
                return (takenItems, takenItems.Count);
            }
            
            // Return the taken items concatenated with remaining items
            return (Enumerable.Concat(takenItems, enumerator.RemainingItems()), takenItems.Count);
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

    }
}
