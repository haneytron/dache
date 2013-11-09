using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Dache.Client.Extensions
{
    public static class MemoryStreamExtensions
    {
        private static readonly Encoding _encoding = CommunicationClient.CommunicationEncoding;
        private static readonly byte[] _spaceByte = _encoding.GetBytes(" ");

        public static void Write(this MemoryStream memoryStream, string value)
        {
            var bytes = _encoding.GetBytes(value);
            memoryStream.Write(bytes, 0, bytes.Length);
        }

        public static void Write(this MemoryStream memoryStream, string format, params object[] args)
        {
            var bytes = _encoding.GetBytes(string.Format(format, args));
            memoryStream.Write(bytes, 0, bytes.Length);
        }

        public static void Write(this MemoryStream memoryStream, byte[] bytes)
        {
            memoryStream.Write(bytes, 0, bytes.Length);
        }

        public static void WriteBase64(this MemoryStream memoryStream, byte[] value)
        {
            var bytes = _encoding.GetBytes(Convert.ToBase64String(value));
            memoryStream.Write(bytes, 0, bytes.Length);
        }

        public static void WriteSpace(this MemoryStream memoryStream)
        {
            memoryStream.Write(_spaceByte, 0, _spaceByte.Length);
        }
    }
}
