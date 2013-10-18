using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceModel;
using System.Text;

namespace Dache.Communication.BoardToManager
{
    /// <summary>
    /// Represents the communication contract between the Dacheboard and the cache manager.
    /// </summary>
    [ServiceContract(Namespace = "http://schemas.getdache.net/dacheboard", Name = "A")]
    public interface IBoardToManagerContract
    {
        /// <summary>
        /// Obtains performance information from the cache manager.
        /// </summary>
        /// <returns>The performance counters indexed at the key of cache host address.</returns>
        [OperationContract(Name = "A")]
        IList<KeyValuePair<string, PerformanceCounter[]>> GetPerformanceInformation();
    }
}
