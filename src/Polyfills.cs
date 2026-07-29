#if NETSTANDARD2_0
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

// Each shim below is declared in the same namespace as the BCL type it extends, so that
// every file which already imports that namespace picks it up without an extra using.
// They are internal, so they never leak into Com.H's public surface or collide with a
// consumer's own polyfills, and the whole file compiles out on net8.0+ where the BCL
// provides the real implementations.

namespace System.IO
{
    /// <summary>
    /// netstandard2.0 shims for <see cref="Stream"/> members that only exist on the modern
    /// target frameworks.
    /// </summary>
    internal static class StreamPolyfills
    {
        /// <summary>
        /// Approximates <c>Stream.WriteAsync(ReadOnlyMemory&lt;byte&gt;, CancellationToken)</c>
        /// (available in .NET Core 2.1+). Uses the underlying array when the memory is backed
        /// by one — which it always is for <c>byte[]</c> and <c>ArrayPool</c> buffers — and
        /// falls back to a copy otherwise.
        /// </summary>
        public static Task WriteAsync(
            this Stream stream, ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> segment) && segment.Array is not null)
            {
                return stream.WriteAsync(segment.Array, segment.Offset, segment.Count, cancellationToken);
            }

            var copy = buffer.ToArray();
            return stream.WriteAsync(copy, 0, copy.Length, cancellationToken);
        }
    }
}

namespace System.Text
{
    /// <summary>
    /// netstandard2.0 shims for <see cref="Encoding"/> members that only exist on the modern
    /// target frameworks.
    /// </summary>
    internal static class EncodingPolyfills
    {
        /// <summary>
        /// Approximates <c>Encoding.GetBytes(ReadOnlySpan&lt;char&gt;, Span&lt;byte&gt;)</c>
        /// (available in .NET Core 2.1+), returning the number of bytes written.
        /// </summary>
        /// <remarks>
        /// Allocates two intermediate arrays, which the modern span-based overload does not.
        /// A pointer-based implementation would avoid that but requires AllowUnsafeBlocks;
        /// the allocation is the better trade for a legacy-target fallback path.
        /// </remarks>
        public static int GetBytes(this Encoding encoding, ReadOnlySpan<char> chars, Span<byte> bytes)
        {
            if (chars.IsEmpty) return 0;

            byte[] encoded = encoding.GetBytes(chars.ToArray());
            encoded.AsSpan().CopyTo(bytes);
            return encoded.Length;
        }
    }
}

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
