using System;

namespace MapBoard.IO.Formats.Gps
{
    /// <summary>
    /// GPX相关异常
    /// </summary>
    public class GpxException : Exception
    {
        public GpxException()
        {
        }

        public GpxException(string message) : base(message)
        {
        }

        public GpxException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}