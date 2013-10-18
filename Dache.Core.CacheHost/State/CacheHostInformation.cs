using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dache.Core.CacheHost.State
{
    /// <summary>
    /// Allows access to information about this cache host.
    /// </summary>
    internal static class CacheHostInformation
    {
        /// <summary>
        /// The host address.
        /// </summary>
        public static string HostAddress { get; set; }
    }
}
