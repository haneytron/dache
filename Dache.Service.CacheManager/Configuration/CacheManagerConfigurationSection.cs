using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;

namespace Dache.Service.CacheManager.Configuration
{
    /// <summary>
    /// An application configuration section that allows a user to specify cache manager settings.
    /// </summary>
    public class CacheManagerConfigurationSection : ConfigurationSection
    {
        // The static readonly cache manager configuration section of the application configuration
        private static readonly CacheManagerConfigurationSection _settings = ConfigurationManager.GetSection("cacheManagerSettings") as CacheManagerConfigurationSection;
        
        /// <summary>
        /// The cache manager settings.
        /// </summary>
        public static CacheManagerConfigurationSection Settings
        {
            get
            {
                return _settings;
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
        /// The cache manager address.
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
        /// The cache manager port.
        /// </summary>
        [IntegerValidator(MinValue = 1, MaxValue = int.MaxValue)]
        [ConfigurationProperty("port", IsRequired = true, DefaultValue = 33334)]
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
        /// The Dacheboard port.
        /// </summary>
        [IntegerValidator(MinValue = 1, MaxValue = int.MaxValue)]
        [ConfigurationProperty("dacheboardport", IsRequired = true, DefaultValue = 33335)]
        public int DacheboardPort
        {
            get
            {
                return (int)this["dacheboardport"];
            }
            set
            {
                this["dacheboardport"] = value;
            }
        }

        /// <summary>
        /// The cache host information polling interval in milliseconds. Valid range is 1000 to 60000.
        /// </summary>
        [IntegerValidator(MinValue = 1000, MaxValue = 60000)]
        [ConfigurationProperty("cacheHostInformationPollingIntervalMilliseconds", IsRequired = true, DefaultValue = 5000)]
        public int CacheHostInformationPollingIntervalMilliseconds
        {
            get
            {
                return (int)this["cacheHostInformationPollingIntervalMilliseconds"];
            }
            set
            {
                this["cacheHostInformationPollingIntervalMilliseconds"] = value;
            }
        }
    }
}
