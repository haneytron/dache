using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dache.CacheHost.Communication
{
    internal sealed class Pool<T> where T : class, new()
    {
        private readonly Queue<T> _queue = null;
        private readonly int _initialPoolCount = 0;

        public Pool(int poolCount)
        {
            _queue = new Queue<T>(poolCount);
            _initialPoolCount = poolCount;
        }

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

        public T Pop()
        {
            if (_queue.Count == 0)
            {
                return new T();
            }

            lock (_queue)
            {
                if (_queue.Count == 0)
                {
                    return new T();
                }

                return _queue.Dequeue();
            }
        }
    }
}
