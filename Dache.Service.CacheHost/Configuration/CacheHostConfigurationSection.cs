using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading;

namespace Dache.Service.CacheHost.Configuration
{
    /// <summary>
    /// An application configuration section that allows a user to specify cache host settings.
    /// </summary>
    public class CacheHostConfigurationSection : ConfigurationSection
    {
        // The static readonly cache host configuration section of the application configuration
        private static readonly CacheHostConfigurationSection _settings = ConfigurationManager.GetSection("cacheHostSettings") as CacheHostConfigurationSection;

        /// <summary>
        /// The cache host settings.
        /// </summary>
        public static CacheHostConfigurationSection Settings
        {
            get
            {
                return _settings;
            }
        }

        /// <summary>
        /// The cache manager.
        /// </summary>
        [ConfigurationProperty("cacheManager", IsRequired = true)]
        public CacheManagerElement CacheManager
        {
            get
            {
                return (CacheManagerElement)this["cacheManager"];
            }
        }

        /// <summary>
        /// The custom logger.
        /// </summary>
        [ConfigurationProperty("customLogger", IsRequired = false)]
        public CustomLoggerElement CustomLogger
        {
            get
            {
                return (CustomLoggerElement)this["customLogger"];
            }
        }

        /// <summary>
        /// The cache host address.
        /// </summary>
        [StringValidator(InvalidCharacters = @"\:")]
        [ConfigurationProperty("address", IsRequired = true)]
        public string Address
        {
            get
            {
                return (string)this["address"];
            }
            set
            {
                this["address"] = value;
            }
        }

        /// <summary>
        /// The cache host port.
        /// </summary>
        [IntegerValidator(MinValue = 1, MaxValue = int.MaxValue)]
        [ConfigurationProperty("port", IsRequired = true, DefaultValue = 33333)]
        public int Port
        {
            get
            {
                return (int)this["port"];
            }
            set
            {
                this["port"] = value;
            }
        }

        /// <summary>
        /// The cache memory limit, as a percentage of the total system memory. Valid range is 20 to 90.
        /// </summary>
        [IntegerValidator(MinValue = 20, MaxValue = 90)]
        [ConfigurationProperty("cacheMemoryLimitPercentage", IsRequired = true, DefaultValue = 80)]
        public int CacheMemoryLimitPercentage
        {
            get
            {
                return (int)this["cacheMemoryLimitPercentage"];
            }
            set
            {
                this["cacheMemoryLimitPercentage"] = value;
            }
        }
    }
}
