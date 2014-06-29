using System.Configuration;
using Dache.CacheHostService.Configuration;

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
        /// How often to attempt to re-establish the connection to a disconnected cache host, in seconds.
        /// </summary>
        [IntegerValidator(MinValue = 1, MaxValue = 300)]
        [ConfigurationProperty("hostReconnectIntervalSeconds", IsRequired = true, DefaultValue = 10)]
        public int HostReconnectIntervalSeconds
        {
            get
            {
                return (int)this["hostReconnectIntervalSeconds"];
            }
        }

        /// <summary>
        /// The local cache memory limit, as a percentage of the total system memory. Valid range is 20 to 90.
        /// </summary>
        [IntegerValidator(MinValue = 5, MaxValue = 90)]
        [ConfigurationProperty("localCacheMemoryLimitPercentage", IsRequired = true, DefaultValue = 80)]
        public int LocalCacheMemoryLimitPercentage
        {
            get
            {
                return (int)this["localCacheMemoryLimitPercentage"];
            }
        }

        /// <summary>
        /// When to expire locally cached entries, in seconds.
        /// </summary>
        [IntegerValidator(MinValue = 1, MaxValue = int.MaxValue)]
        [ConfigurationProperty("localCacheAbsoluteExpirationSeconds", IsRequired = true, DefaultValue = 10)]
        public int LocalCacheAbsoluteExpirationSeconds
        {
            get
            {
                return (int)this["localCacheAbsoluteExpirationSeconds"];
            }
        }

        /// <summary>
        /// The custom logger.
        /// </summary>
        [ConfigurationProperty("customLogger", IsRequired = false)]
        public CustomTypeElement CustomLogger
        {
            get
            {
                return (CustomTypeElement)this["customLogger"];
            }
        }

        /// <summary>
        /// The custom serializer.
        /// </summary>
        [ConfigurationProperty("customSerializer", IsRequired = false)]
        public CustomTypeElement CustomSerializer
        {
            get
            {
                return (CustomTypeElement)this["customSerializer"];
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
