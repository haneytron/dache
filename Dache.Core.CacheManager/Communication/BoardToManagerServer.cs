using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceModel;
using System.Text;
using Dache.Communication.BoardToManager;
using Dache.Core.CacheManager.State;

namespace Dache.Core.CacheManager.Communication
{
    /// <summary>
    /// The WCF server for Dacheboard to manager communication.
    /// </summary>
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple, UseSynchronizationContext = false, MaxItemsInObjectGraph = int.MaxValue, Namespace = "http://schemas.getdache.net/dacheboard")]
    public class BoardToManagerServer : IBoardToManagerContract
    {
        /// <summary>
        /// Obtains performance information from the cache manager.
        /// </summary>
        /// <returns>The performance counters indexed at the key of cache host address.</returns>
        public IList<KeyValuePair<string, PerformanceCounter[]>> GetPerformanceInformation()
        {
            return CacheHostManager.GetPerformanceCounters();
        }
    }
}
