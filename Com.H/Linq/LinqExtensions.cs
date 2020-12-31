using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Com.H.Linq
{
    public static class LinqExtensions
    {
        public static IEnumerable<TValue> OrdinalFilter<TKey, TValue>(
            this IDictionary<TKey, TValue> dictionary, IEnumerable<TKey> orderedFilter)
            => orderedFilter==null?null
            :orderedFilter.Join(dictionary, o => o, d => d.Key, (o, d) => d.Value);

    }
}
