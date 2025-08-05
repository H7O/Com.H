using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Com.H.Collections.Generic
{
    /// <summary>
    /// An async enumerable wrapper that provides information about how many items were chambered
    /// during the ToChamberedEnumerableAsync operation
    /// </summary>
    /// <typeparam name="T">The type of elements in the async enumerable</typeparam>
    public class ChamberedAsyncEnumerable<T> : IAsyncEnumerable<T>
    {
        private readonly IAsyncEnumerable<T> _asyncEnumerable;

        /// <summary>
        /// Gets the actual number of items that were chambered (pre-fetched)
        /// </summary>
        public int ChamberedCount { get; }

        /// <summary>
        /// Gets a value indicating whether the async enumerable was exhausted during chambering
        /// </summary>
        public bool WasExhausted(int requestedChamberSize) => ChamberedCount < requestedChamberSize;

        internal ChamberedAsyncEnumerable(IAsyncEnumerable<T> asyncEnumerable, int chamberedCount)
        {
            _asyncEnumerable = asyncEnumerable ?? throw new ArgumentNullException(nameof(asyncEnumerable));
            ChamberedCount = chamberedCount;
        }

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            => _asyncEnumerable.GetAsyncEnumerator(cancellationToken);

        // returns IEmerable<T> for compatibility with LINQ methods
        public IEnumerable<T> AsEnumerable() => _asyncEnumerable.ToBlockingEnumerable();
    }

}
