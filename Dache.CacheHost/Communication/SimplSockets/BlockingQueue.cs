using System;
using System.Collections.Generic;
using System.Threading;

namespace SimplSockets
{
    internal sealed class BlockingQueue<T>
    {
        Queue<T> _queue = null;
        Semaphore _semaphore = new Semaphore(0, Int32.MaxValue);

        public BlockingQueue()
        {
            new Queue<T>();
        }

        public BlockingQueue(int capacity)
        {
            _queue = new Queue<T>(capacity);
        }

        public void Enqueue(T item)
        {
            lock (_queue)
            {
                _queue.Enqueue(item);
            }

            _semaphore.Release();
        }

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
