using System;

namespace StreamRecorder
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