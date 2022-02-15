using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Com.H.Threading
{
    /// <summary>
    /// Offers thread-locking-free atomic gate mechanism.
    /// </summary>
    public class AtomicGate
    {
        private int gate;
        public AtomicGate() => gate = 0;

        /// <summary>
        /// Retuns true if called for the first time, 
        /// and false on subsequent calls until TryClose() is called.
        /// </summary>
        /// <returns>Opens the gate and returns true, and returns false if the gate was already open</returns>
        public bool TryOpen() =>
            Interlocked.CompareExchange(ref gate, 1, 0) == 0;

        /// <summary>
        /// Retuns true if TryOpen() is called prior calling it, 
        /// and false on subsequent calls until TryOpen() is called again.
        /// </summary>
        /// <returns>Closes the gate and returns true, and returns false if the gate was already close</returns>
        public bool TryClose() =>
            Interlocked.CompareExchange(ref gate, 0, 1) == 1;

        /// <summary>
        /// Tells whether or not the gate is open
        /// </summary>
        public bool IsOpen => gate == 1;
    }
}
