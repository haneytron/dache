using System;
using System.Collections.Generic;
using System.Threading;

namespace SimplSockets
{
    /// <summary>
    /// A queue that wraps a regular generic queue but when empty will block Dequeue threads until an item is available.
    /// </summary>
    /// <typeparam name="T">The type of the object contained in the queue.</typeparam>
    internal sealed class BlockingQueue<T>
    {
        // The underlying queue
        Queue<T> _queue = null;
        // The semaphore used for blocking
        Semaphore _semaphore = new Semaphore(0, Int32.MaxValue);

        /// <summary>
        /// The constructor.
        /// </summary>
        public BlockingQueue()
        {
            _queue = new Queue<T>();
        }

        /// <summary>
        /// The constructor.
        /// </summary>
        /// <param name="capacity">Sets the initial queue capacity.</param>
        public BlockingQueue(int capacity)
        {
            _queue = new Queue<T>(capacity);
        }

        /// <summary>
        /// Enqueues an item.
        /// </summary>
        /// <param name="item">An item.</param>
        public void Enqueue(T item)
        {
            lock (_queue)
            {
                _queue.Enqueue(item);
            }

            _semaphore.Release();
        }

        /// <summary>
        /// Dequeues an item. Will block if the queue is empty until an item becomes available.
        /// </summary>
        /// <returns>An item.</returns>
        public T Dequeue()
        {
            _semaphore.WaitOne();

            lock (_queue)
            {
                return _queue.Dequeue();
            }
        }
    }
}
