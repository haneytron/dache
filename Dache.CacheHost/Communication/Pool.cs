using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dache.CacheHost.Communication
{
    internal sealed class Pool<T> where T : class
    {
        private readonly Queue<T> _queue = null;
        private readonly int _initialPoolCount = 0;
        private readonly Func<T> _newItemMethod = null;
        private readonly Action<T> _resetItemMethod = null;

        public Pool(int poolCount, Func<T> newItemMethod, Action<T> resetItemMethod)
        {
            _queue = new Queue<T>(poolCount);
            _initialPoolCount = poolCount;
            _newItemMethod = newItemMethod;
            _resetItemMethod = resetItemMethod;
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
            T result = null;

            if (_queue.Count == 0)
            {
                result = _newItemMethod();
                _resetItemMethod(result);
                return result;
            }

            lock (_queue)
            {
                if (_queue.Count == 0)
                {
                    return _newItemMethod();
                }

                result = _queue.Dequeue();
                _resetItemMethod(result);
                return result;
            }
        }
    }
}
