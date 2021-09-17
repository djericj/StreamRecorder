using Microsoft.Extensions.Logging;

namespace StreamRecorderLib.Events
{
    public class RecorderEventArgs
    {
        public string Message { get; set; }
        public LogLevel LogLevel { get; set; }

        public RecorderEventArgs(string message, LogLevel logLevel)
        {
            Message = message;
            LogLevel = logLevel;
        }
    }
}