using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dache.Core.DataStructures.Performance;

namespace Dache.Core.CacheHost.Performance
{
    /// <summary>
    /// Contains an instance of a custom performance counter manager.
    /// </summary>
    public static class CustomPerformanceCounterManagerContainer
    {
        /// <summary>
        /// The instance.
        /// </summary>
        public static CustomPerformanceCounterManager Instance { get; set; }
    }
}
