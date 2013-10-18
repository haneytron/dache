using Dache.Communication.ClientToCache;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dache.Core.CacheHost.Communication.CacheToCache
{
    /// <summary>
    /// Represents a cache to cache communication client.
    /// </summary>
    public interface ICacheToCacheClient : IClientToCacheContract
    {
        /// <summary>
        /// Closes the connection to the cache host.
        /// </summary>
        void CloseConnection();

        /// <summary>
        /// Event that fires when the cache client is disconnected from a cache host.
        /// </summary>
        event EventHandler Disconnected;

        /// <summary>
        /// Event that fires when the cache client is successfully reconnected to a disconnected cache host.
        /// </summary>
        event EventHandler Reconnected;
    }
}
