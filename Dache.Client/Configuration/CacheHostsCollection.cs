using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;

namespace Dache.Client.Configuration
{
    /// <summary>
    /// Provides a configuration collection of cache host elements.
    /// </summary>
    public class CacheHostsCollection : ConfigurationElementCollection
    {
        /// <summary>
        /// Creates a new cache host element.
        /// </summary>
        /// <returns>A new cache host element.</returns>
        protected override ConfigurationElement CreateNewElement()
        {
            return new CacheHostElement();
        }

        /// <summary>
        /// Gets the key of a given configuration element.
        /// </summary>
        /// <param name="element">The configuration element.</param>
        /// <returns>The key.</returns>
        protected override object GetElementKey(ConfigurationElement element)
        {
            CacheHostElement service = (CacheHostElement)element;
            return service.Address + ":" + service.Port;
        }

        /// <summary>
        /// Gets or sets the cache host element for the given index.
        /// </summary>
        /// <param name="index">The index of the cache host element to get or set.</param>
        /// <returns>The cache host element.</returns>
        public CacheHostElement this[int index]
        {
            get
            {
                return (CacheHostElement)BaseGet(index);
            }
            set
            {
                if (BaseGet(index) != null)
                {
                    BaseRemove(index);
                }

                BaseAdd(index, value);
            }
        }

        /// <summary>
        /// Gets or sets the cache host element for the given name.
        /// </summary>
        /// <param name="name">The name of the cache host element to get or set.</param>
        /// <returns>The cache host element.</returns>
        public new CacheHostElement this[string name]
        {
            get
            {
                return (CacheHostElement)BaseGet(name);
            }
        }

        /// <summary>
        /// Gets the number of cache host elements.
        /// </summary>
        public new int Count
        {
            get
            {
                return base.Count;
            }
        }

        public int IndexOf(CacheHostElement service)
        {
            return BaseIndexOf(service);
        }

        public void RemoveAt(int index)
        {
            BaseRemoveAt(index);
        }

        public void Add(CacheHostElement item)
        {
            BaseAdd(item);
        }

        public void Clear()
        {
            BaseClear();
        }

        public bool Contains(CacheHostElement item)
        {
            return BaseIndexOf(item) >= 0;
        }

        public void CopyTo(CacheHostElement[] array, int arrayIndex)
        {
            base.CopyTo(array, arrayIndex);
        }

        public new bool IsReadOnly
        {
            get
            {
                return false;
            }
        }

        public bool Remove(CacheHostElement item)
        {
            if (BaseIndexOf(item) >= 0)
            {
                BaseRemove(item);
                return true;
            }

            return false;
        }
    }
}
