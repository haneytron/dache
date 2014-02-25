using System.Configuration;

namespace Dache.Client.Configuration
{
    /// <summary>
    /// Provides a cache host element for configuration.
    /// </summary>
    public class CacheHostElement : ConfigurationElement
    {
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
    }
}
