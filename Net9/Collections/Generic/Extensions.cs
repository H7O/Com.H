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
        /// enumerable item, to force enumerable evaluation 
        /// (which helps in executing Link-to-SQL queries) 
        /// without iterating through all the enumerator 
        /// items, then returns the first fetched item 
        /// along with the remaining items back as an enumerable
        /// </summary>
        /// <param name="enumerable"></param>
        /// <returns></returns>
        public static IEnumerable<dynamic> ToChamberedEnumerable(
            this IEnumerable<dynamic>? enumerable)
        {
            if (enumerable is null)
            {
                return Enumerable.Empty<dynamic>();
            }
            var enumerator = enumerable.GetEnumerator();
            if (enumerator.MoveNext())
            {
                return Enumerable.Concat(
                    new[] { enumerator.Current },
                    enumerator.RemainingItems());
            }
            return Enumerable.Empty<dynamic>();

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
