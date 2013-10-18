using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;

namespace Dache.Client.Configuration
{
    /// <summary>
    /// An application configuration section that allows a user to specify cache client settings.
    /// </summary>
    public class CacheClientConfigurationSection : ConfigurationSection
    {
        // The static readonly cache client configuration section of the application configuration
        private static readonly CacheClientConfigurationSection _settings = ConfigurationManager.GetSection("cacheClientSettings") as CacheClientConfigurationSection;
        
        /// <summary>
        /// The cache host settings.
        /// </summary>
        public static CacheClientConfigurationSection Settings
        {
            get
            {
                return _settings;
            }
        }

        /// <summary>
        /// How often to attempt to re-establish the connection to a disconnected cache host, in milliseconds.
        /// </summary>
        [IntegerValidator(MinValue = 1000, MaxValue = 60000)]
        [ConfigurationProperty("hostReconnectIntervalMilliseconds", IsRequired = true, DefaultValue = 10000)]
        public int HostReconnectIntervalMilliseconds
        {
            get
            {
                return (int)this["hostReconnectIntervalMilliseconds"];
            }
        }

        /// <summary>
        /// The cache hosts collection.
        /// </summary>
        [ConfigurationProperty("cacheHosts", IsDefaultCollection = false)]
        [ConfigurationCollection(typeof(CacheHostsCollection), AddItemName = "add", RemoveItemName = "remove", ClearItemsName = "clear")]
        public CacheHostsCollection CacheHosts
        {
            get
            {
                return (CacheHostsCollection)this["cacheHosts"];
            }
        }
    }
}
