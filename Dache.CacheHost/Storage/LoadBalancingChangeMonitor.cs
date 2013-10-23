using System;
using System.Runtime.Caching;

namespace Dache.CacheHost.Storage
{
    /// <summary>
    /// A change monitor that allows for load balancing via redistribution of cached items to hosts.
    /// </summary>
    internal class LoadBalancingChangeMonitor : ChangeMonitor
    {
        // The unique ID
        private readonly string _uniqueId = null;
        // The load balancing method
        private readonly Action _loadBalancingMethod = null;

        /// <summary>
        /// The constructor.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="loadBalancingMethod">The load balancing method.</param>
        public LoadBalancingChangeMonitor(string cacheKey, Action loadBalancingMethod)
        {
            // Set the unique ID
            _uniqueId = cacheKey;
            // Set the load balancing method
            _loadBalancingMethod = loadBalancingMethod;

            // Call initialization complete
            InitializationComplete();
        }

        /// <summary>
        /// The load balancing required method.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The event args.</param>
        public void LoadBalancingRequired(object sender, EventArgs e)
        {
            // Notify OnChanged first
            OnChanged(_uniqueId);

            // Invoke the load balancing method
            _loadBalancingMethod();
        }

        /// <summary>
        /// The unique ID of the change monitor.
        /// </summary>
        public override string UniqueId
        {
            get 
            {
                return _uniqueId;
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose();
        }
    }
}
