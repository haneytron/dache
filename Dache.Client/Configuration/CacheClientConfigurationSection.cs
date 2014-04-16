using System.Configuration;

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
        /// Gets the cache host settings.
        /// </summary>
        public static CacheClientConfigurationSection Settings
        {
            get
            {
                return _settings;
            }
        }

        /// <summary>
        /// Gets the host reconnect interval expressed in seconds.
        /// </summary>
        /// <value>
        /// The host reconnect interval in seconds.
        /// </value>
        /// <remarks>
        /// How often to attempt to re-establish the connection to a disconnected cache host, in seconds.
        /// </remarks>
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
        /// Gets the local cache memory limit percentage.
        /// </summary>
        /// <value>
        /// The local cache memory limit percentage.
        /// </value>
        /// <remarks>
        /// The local cache memory limit, as a percentage of the total system memory. Valid range is 20 to 90.
        /// </remarks>
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
        /// Gets the local cache absolute expiration expressed in seconds.
        /// </summary>
        /// <value>
        /// The local cache absolute expiration in seconds.
        /// </value>
        /// <remarks>
        /// When to expire locally cached entries, in seconds.
        /// </remarks>
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
        /// Gets the custom logger.
        /// </summary>
        /// <value>
        /// The custom logger.
        /// </value>
        [ConfigurationProperty("customLogger", IsRequired = false)]
        public CustomTypeElement CustomLogger
        {
            get
            {
                return (CustomTypeElement)this["customLogger"];
            }
        }

        /// <summary>
        /// Gets the custom serializer.
        /// </summary>
        /// <value>
        /// The custom serializer.
        /// </value>
        [ConfigurationProperty("customSerializer", IsRequired = false)]
        public CustomTypeElement CustomSerializer
        {
            get
            {
                return (CustomTypeElement)this["customSerializer"];
            }
        }

        /// <summary>
        /// Gets the cache hosts collection.
        /// </summary>
        /// <value>
        /// The cache hosts collection.
        /// </value>
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
