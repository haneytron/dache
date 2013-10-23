using System;

namespace Dache.Core.Logging
{
    /// <summary>
    /// Contains a logger that logs information while the services run.
    /// </summary>
    public static class LoggerContainer
    {
        // The logger
        private static ILogger _logger = null;

        /// <summary>
        /// The logger instance.
        /// </summary>
        public static ILogger Instance
        {
            get
            {
                return _logger;
            }
            set
            {
                // Sanitize
                if (value == null)
                {
                    throw new ArgumentNullException("value");
                }

                _logger = value;
            }
        }
    }
}
