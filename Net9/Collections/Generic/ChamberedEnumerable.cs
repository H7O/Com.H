using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Com.H.Collections.Generic
{
    /// <summary>
    /// An enumerable wrapper that provides information about how many items were chambered
    /// during the ToChamberedEnumerable operation
    /// </summary>
    /// <typeparam name="T">The type of elements in the enumerable</typeparam>
    public class ChamberedEnumerable<T> : IEnumerable<T>
    {
        private readonly IEnumerable<T> _enumerable;

        /// <summary>
        /// Gets the actual number of items that were chambered (pre-fetched)
        /// </summary>
        public int ChamberedCount { get; }

        /// <summary>
        /// Gets a value indicating whether the enumerable was exhausted during chambering
        /// </summary>
        public bool WasExhausted(int requestedChamberSize) => ChamberedCount < requestedChamberSize;

        internal ChamberedEnumerable(IEnumerable<T> enumerable, int chamberedCount)
        {
            _enumerable = enumerable ?? throw new ArgumentNullException(nameof(enumerable));
            ChamberedCount = chamberedCount;
        }

        public IEnumerator<T> GetEnumerator() => _enumerable.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
