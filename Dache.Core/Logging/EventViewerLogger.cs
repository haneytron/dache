using System;
using System.Diagnostics;
using System.Security;

namespace Dache.Core.Logging
{
    /// <summary>
    /// Logs messages and exceptions to the windows event viewer. Thread safe.
    /// </summary>
    public class EventViewerLogger : ILogger
    {
        // The event log
        private readonly EventLog _eventLog = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventViewerLogger"/> class.
        /// </summary>
        /// <param name="source">The source name by which the application is registered on the local computer.</param>
        /// <param name="logName">The name of the log the source's entries are written to.</param>
        public EventViewerLogger(string source, string logName)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(source))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "source");
            }

            if (string.IsNullOrWhiteSpace(logName))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "logName");
            }

            // Attempt to ensure that the event log source exists
            try
            {
                if (!EventLog.SourceExists(source))
                {
                    EventLog.CreateEventSource(source, logName);
                }
            }
            catch (SecurityException)
            {
                // Ignore it...
            }

            // Initialize the event log for logging
            _eventLog = new EventLog
            {
                Source = source,
                Log = logName
            };
        }

        /// <summary>
        /// Logs an information level message.
        /// </summary>
        /// <param name="title">The title.</param>
        /// <param name="message">The message.</param>
        public void Info(string title, string message)
        {
            _eventLog.WriteEntry(title + " : " + message, EventLogEntryType.Information);
        }

        /// <summary>
        /// Logs a warning level message.
        /// </summary>
        /// <param name="title">The title.</param>
        /// <param name="message">The message.</param>
        public void Warn(string title, string message)
        {
            _eventLog.WriteEntry(title + " : " + message, EventLogEntryType.Warning);
        }

        /// <summary>
        /// Logs a warning level exception.
        /// </summary>
        /// <param name="prependedMessage">The message to prepend to the exception.</param>
        /// <param name="ex">The exception.</param>
        public void Warn(string prependedMessage, Exception ex)
        {
            _eventLog.WriteEntry(prependedMessage + " : " + ex.ToString(), EventLogEntryType.Warning);
        }

        /// <summary>
        /// Logs a warning level exception.
        /// </summary>
        /// <param name="ex">The exception.</param>
        public void Warn(Exception ex)
        {
            _eventLog.WriteEntry(ex.ToString(), EventLogEntryType.Warning);
        }

        /// <summary>
        /// Logs an error level message.
        /// </summary>
        /// <param name="title">The title.</param>
        /// <param name="message">The message.</param>
        public void Error(string title, string message)
        {
            _eventLog.WriteEntry(title + " : " + message, EventLogEntryType.Error);
        }

        /// <summary>
        /// Logs an error level exception.
        /// </summary>
        /// <param name="prependedMessage">The message to prepend to the exception.</param>
        /// <param name="ex">The exception.</param>
        public void Error(string prependedMessage, Exception ex)
        {
            _eventLog.WriteEntry(prependedMessage + " : " + ex.ToString(), EventLogEntryType.Error);
        }

        /// <summary>
        /// Logs an error level exception.
        /// </summary>
        /// <param name="ex">The exception.</param>
        public void Error(Exception ex)
        {
            _eventLog.WriteEntry(ex.ToString(), EventLogEntryType.Error);
        }
    }
}
