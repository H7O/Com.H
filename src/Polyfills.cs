#if NETSTANDARD2_0
using System.Threading;

// Declared in System.Collections.Generic so that every file which already imports that
// namespace picks the shims up without an extra using. They are internal, so they never
// leak into Com.H's public surface or collide with a consumer's own polyfills, and the
// whole file compiles out on net8.0+ where the BCL provides the real implementations.
namespace System.Collections.Generic
{
    /// <summary>
    /// netstandard2.0 shims for <see cref="IAsyncEnumerable{T}"/> helpers that only exist
    /// on the modern target frameworks.
    /// </summary>
    internal static class AsyncEnumerablePolyfills
    {
        /// <summary>
        /// Approximates <c>IAsyncEnumerable&lt;T&gt;.ToBlockingEnumerable()</c> (available in .NET 7+).
        /// Blocks the calling thread on each MoveNextAsync.
        /// </summary>
        public static IEnumerable<T> ToBlockingEnumerable<T>(
            this IAsyncEnumerable<T> source, CancellationToken cancellationToken = default)
        {
            var enumerator = source.GetAsyncEnumerator(cancellationToken);
            try
            {
                while (true)
                {
                    var moveNextTask = enumerator.MoveNextAsync();
                    if (!moveNextTask.AsTask().GetAwaiter().GetResult()) break;
                    yield return enumerator.Current;
                }
            }
            finally
            {
                enumerator.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        }
    }
}
#endif
