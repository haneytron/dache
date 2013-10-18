using Dache.Communication.CacheToManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dache.Core.CacheHost.Communication.CacheToManager
{
    /// <summary>
    /// Represents a cache manager client.
    /// </summary>
    public interface ICacheManagerClient : ICacheToManagerContract
    {
        /// <summary>
        /// Closes the connection to the cache host.
        /// </summary>
        void CloseConnection();

        /// <summary>
        /// Event that fires when the cache host is disconnected from the cache manager.
        /// </summary>
        event EventHandler Disconnected;

        /// <summary>
        /// Event that fires when the cache host is successfully reconnected to the disconnected cache manager.
        /// </summary>
        event EventHandler Reconnected;
    }
}
