using System;

namespace StreamRecorder
{
    public class StreamEventMessage : EventArgs
    {
        public string Message { get; set; }

        public StreamEventMessage(string message)
        {
            Message = message;
        }
    }
}