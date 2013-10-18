using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dache.Communication.CacheToManager;

namespace Dache.Core.CacheHost.Communication.CacheToManager
{
    /// <summary>
    /// Contains an instance of a cache manager client.
    /// </summary>
    public static class ManagerClientContainer
    {
        /// <summary>
        /// The cache manager client instance.
        /// </summary>
        public static ICacheManagerClient Instance { get; set; }
    }
}
