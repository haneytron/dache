using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Dache.Core.DataStructures.Routing
{
    /// <summary>
    /// A static container for a routing table.
    /// </summary>
    public static class RoutingTableContainer
    {
        /// <summary>
        /// Static constructor.
        /// </summary>
        static RoutingTableContainer()
        {
            Lock = new ReaderWriterLockSlim();
        }

        /// <summary>
        /// The routing table instance.
        /// </summary>
        public static RoutingTable Instance { get; set; }

        /// <summary>
        /// The lock used to ensure state.
        /// </summary>
        public static ReaderWriterLockSlim Lock { get; private set; }
    }
}
