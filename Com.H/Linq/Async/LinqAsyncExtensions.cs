using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Com.H.Linq.Async
{
    public static class LinqAsyncExtensions
    {
        public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> items,
            CancellationToken? cancellationToken = default)
        {
            var list = new List<T>();

            if (cancellationToken is not null) 
                await foreach (var item in items
                    .WithCancellation((CancellationToken) cancellationToken))
                list.Add(item);
            else
                await foreach (var item in items)
                    list.Add(item);

            return list;
        }

    }
}
