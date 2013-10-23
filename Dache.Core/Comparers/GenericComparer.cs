using System;
using System.Collections.Generic;

namespace Dache.Core.Comparers
{
    /// <summary>
    /// A generic comparer that takes as input a function which determines how comparisons are made.
    /// </summary>
    /// <typeparam name="T">The type to compare.</typeparam>
    public class GenericComparer<T> : IComparer<T>
    {
        // The comparer func
        private readonly Func<T, T, int> _comparerFunc = null;

        /// <summary>
        /// The constructor.
        /// </summary>
        /// <param name="comparerFunc">The function which compares two objects.</param>
        public GenericComparer(Func<T, T, int> comparerFunc)
        {
            // Sanitize
            if (comparerFunc == null)
            {
                throw new ArgumentNullException("comparerFunc");
            }

            _comparerFunc = comparerFunc;
        }

        /// <summary>
        /// Compares two objects.
        /// </summary>
        /// <param name="x">The first object.</param>
        /// <param name="y">The second object.</param>
        /// <returns>-1 if x is less than y, 1 if x is greater than y, and 0 if the two are equal.</returns>
        public int Compare(T x, T y)
        {
            return _comparerFunc(x, y);
        }
    }
}
