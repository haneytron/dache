using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dache.Core.CacheHost.Storage
{
    /// <summary>
    /// Contains an instance of a mem cache.
    /// </summary>
    public static class MemCacheContainer
    {
        // The instance
        private static MemCache _instance = null;

        /// <summary>
        /// The mem cache instance.
        /// </summary>
        public static MemCache Instance
        {
            get
            {
                return _instance;
            }
            set
            {
                // Sanitize
                if (value == null)
                {
                    throw new ArgumentNullException("value");
                }
                if (_instance != null)
                {
                    throw new NotSupportedException("The mem cache instance cannot be set more than once");
                }

                _instance = value;
            }
        }
    }
}
