using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;

namespace Dache.Service.CacheManager.Configuration
{
    /// <summary>
    /// Provides a custom logger element for configuration.
    /// </summary>
    public class CustomLoggerElement : ConfigurationElement
    {
        /// <summary>
        /// The custom logger type.
        /// </summary>
        [StringValidator(InvalidCharacters = @"\: ")]
        [ConfigurationProperty("type", IsRequired = true)]
        public string Type
        {
            get
            {
                return (string)this["type"];
            }
            set
            {
                this["type"] = value;
            }
        }
    }
}
