using System;

namespace StreamRecorderLib.Events
{
    public class BufferProgressEvent : EventArgs
    {
        public double TotalSeconds { get; set; }

        public BufferProgressEvent(double totalSeconds)
        {
            TotalSeconds = totalSeconds;
        }
    }
}