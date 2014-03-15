using System;
using System.IO;
using System.Reflection;

namespace Dache.Core.Logging
{
    public class FileLogger : ILogger
    {
        #region ILogger Members

        private readonly string _filePath;

        /// <summary>
        /// The constructor.
        /// </summary>
        /// <param name="fileName">The name of the log file to write to. Defaults to 'log.txt'</param>
        public FileLogger(string fileName = "log.txt")
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "fileName");
            }

            var currentDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            _filePath = currentDirectory + "\\" + fileName;
            if (!File.Exists(_filePath))
                File.Create(_filePath);
        }

        public void Info(string title, string message)
        {
            WriteLine("INFO", title + " : " + message);
        }

        public void Warn(string title, string message)
        {
            WriteLine("WARN", title + " : " + message);
        }

        public void Warn(string prependedMessage, Exception ex)
        {
            WriteLine("WARN", prependedMessage + " : " + ex);
        }

        public void Warn(Exception ex)
        {
            WriteLine("WARN", ex.ToString());
        }

        public void Error(string title, string message)
        {
            WriteLine("ERROR", title + " : " + message);
        }

        public void Error(string prependedMessage, Exception ex)
        {
            WriteLine("ERROR", prependedMessage + " : " + ex);
        }

        public void Error(Exception ex)
        {
            WriteLine("ERROR", ex.ToString());
        }

        protected virtual void WriteLine(string level, string message)
        {
            WriteRaw(string.Format("{0} {1} {2}{3}", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"), level, message, Environment.NewLine));
        }

        protected virtual void WriteRaw(string message)
        {
            using (var writer = File.AppendText(_filePath))
            {
                writer.Write(message);
            }
        }



        #endregion
    }
}
