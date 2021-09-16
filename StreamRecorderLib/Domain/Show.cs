using System;

namespace StreamRecorderLib.Domain
{
    public class Show
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string FileName { get; set; }
    }
}