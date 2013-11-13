using System;
using System.Collections.Generic;

namespace SimplSockets
{
    /// <summary>
    /// A pool of objects that can be reused to manage memory efficiently.
    /// </summary>
    /// <typeparam name="T">The type of object that is pooled.</typeparam>
    internal sealed class Pool<T> where T : class
    {
        // The queue that holds the items
        private readonly Queue<T> _queue = null;
        // The initial pool count
        private readonly int _initialPoolCount = 0;
        // The method that creates a new item
        private readonly Func<T> _newItemMethod = null;
        // The method that resets an item's state
        private readonly Action<T> _resetItemMethod = null;

        /// <summary>
        /// The constructor.
        /// </summary>
        /// <param name="poolCount">The count of items in the pool.</param>
        /// <param name="newItemMethod">The method that creates a new item.</param>
        /// <param name="resetItemMethod">The method that resets an item's state.</param>
        public Pool(int poolCount, Func<T> newItemMethod, Action<T> resetItemMethod)
        {
            _queue = new Queue<T>(poolCount);
            _initialPoolCount = poolCount;
            _newItemMethod = newItemMethod;
            _resetItemMethod = resetItemMethod;
        }

        /// <summary>
        /// Pushes an item into the pool for later re-use.
        /// </summary>
        /// <param name="item">The item.</param>
        public void Push(T item)
        {
            // Limit queue size
            if (_queue.Count > _initialPoolCount)
            {
                return;
            }

            lock (_queue)
            {
                _queue.Enqueue(item);
            }
        }

        /// <summary>
        /// Pops an item out of the pool for use. The item will have its state reset.
        /// </summary>
        /// <returns></returns>
        public T Pop()
        {
            T result = null;

            // Cheap check
            if (_queue.Count == 0)
            {
                result = _newItemMethod();
                if (_resetItemMethod != null)
                {
                    _resetItemMethod(result);
                }
                return result;
            }

            lock (_queue)
            {
                // Double lock check
                if (_queue.Count == 0)
                {
                    result = _newItemMethod();
                    if (_resetItemMethod != null)
                    {
                        _resetItemMethod(result);
                    }
                    return result;
                }

                result = _queue.Dequeue();
                if (_resetItemMethod != null)
                {
                    _resetItemMethod(result);
                }
                return result;
            }
        }
    }
}
