using System;
using System.ComponentModel;
using System.Configuration;
using Dache.CacheHost.Storage;
using Dache.Client.Configuration;

namespace Dache.CacheHost.Configuration
{
    /// <summary>
    /// An application configuration section that allows a user to specify cache host settings.
    /// </summary>
    public class CacheHostConfigurationSection : ConfigurationSection
    {
        // The static readonly cache host configuration section of the application configuration
        private static readonly CacheHostConfigurationSection _settings = ConfigurationManager.GetSection("cacheHostSettings") as CacheHostConfigurationSection;

        /// <summary>
        /// Gets the cache host settings.
        /// </summary>
        /// <value>
        /// The cache host settings.
        /// </value>
        public static CacheHostConfigurationSection Settings
        {
            get { return _settings; }
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
            get { return (CustomTypeElement)this["customLogger"]; }
        }

        /// <summary>
        /// Gets or sets the cache host port.
        /// </summary>
        /// <value>
        /// The cache host port.
        /// </value>
        [IntegerValidator(MinValue = 1, MaxValue = int.MaxValue)]
        [ConfigurationProperty("port", IsRequired = true, DefaultValue = 33333)]
        public int Port
        {
            get { return (int)this["port"]; }
            set { this["port"] = value; }
        }

        /// <summary>
        /// Gets or sets the cache memory limit, as a percentage of the total system memory. Valid range is 20 to 90.
        /// </summary>
        /// <value>
        /// The cache memory limit percentage.
        /// </value>
        [IntegerValidator(MinValue = 5, MaxValue = 90)]
        [ConfigurationProperty("cacheMemoryLimitPercentage", IsRequired = true, DefaultValue = 80)]
        public int CacheMemoryLimitPercentage
        {
            get { return (int)this["cacheMemoryLimitPercentage"]; }
            set { this["cacheMemoryLimitPercentage"] = value; }
        }

        /// <summary>
        /// Gets or sets the storage provider.
        /// </summary>
        /// <value>
        /// The storage provider.
        /// </value>
        [TypeConverter(typeof(TypeNameConverter))]
        [ConfigurationProperty("storageProvider", IsRequired = false, DefaultValue = typeof(MemCache))]
        public Type StorageProvider
        {
            get { return this["storageProvider"] as Type; }
            set { this["storageProvider"] = value; }
        }
    }
}
