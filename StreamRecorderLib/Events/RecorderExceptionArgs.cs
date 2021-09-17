using System;

namespace StreamRecorderLib.Events
{
    public class RecorderExceptionArgs : EventArgs
    {
        public Exception Exception { get; set; }

        public RecorderExceptionArgs(Exception ex)
        {
            Exception = ex;
        }
    }
}