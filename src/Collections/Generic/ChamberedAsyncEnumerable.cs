#if NET8_0_OR_GREATER
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
    public class ChamberedAsyncEnumerable<T> : IAsyncEnumerable<T>, IAsyncDisposable
    {
        private readonly IAsyncEnumerable<T> _asyncEnumerable;
        private readonly object? _disposalTarget;
        private bool _disposed = false;

        /// <summary>
        /// Gets the actual number of items that were chambered (pre-fetched)
        /// </summary>
        public int ChamberedCount { get; }

        /// <summary>
        /// Gets a value indicating whether the async enumerable was exhausted during chambering
        /// </summary>
        public bool WasExhausted(int requestedChamberSize) => ChamberedCount < requestedChamberSize;

        internal ChamberedAsyncEnumerable(
            IAsyncEnumerable<T> asyncEnumerable,
            int chamberedCount,
            object? disposalTarget = null)
        {
            _asyncEnumerable = asyncEnumerable ?? throw new ArgumentNullException(nameof(asyncEnumerable));
            _disposalTarget = disposalTarget;
            ChamberedCount = chamberedCount;
        }

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            => _asyncEnumerable.GetAsyncEnumerator(cancellationToken);

        // returns IEmerable<T> for compatibility with LINQ methods
        public IEnumerable<T> AsEnumerable() => _asyncEnumerable.ToBlockingEnumerable();

        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                // Dispose the original source, if the factory provided one. The source
                // typically owns external resources (e.g. a DbDataReader) that the
                // ConcatAsyncEnumerables wrapper in _asyncEnumerable can't reach on its own.
                if (_disposalTarget is IAsyncDisposable targetAsyncDisposable)
                {
                    await targetAsyncDisposable.DisposeAsync();
                }
                else if (_disposalTarget is IDisposable targetDisposable)
                {
                    targetDisposable.Dispose();
                }

                // Dispose the underlying enumerable if it's disposable
                if (_asyncEnumerable is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync();
                }
                else if (_asyncEnumerable is IDisposable disposable)
                {
                    disposable.Dispose();
                }

                _disposed = true;
            }
        }
    }

}
#endif
