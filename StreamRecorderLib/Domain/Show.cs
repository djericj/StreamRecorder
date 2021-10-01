using StreamRecorderLib.Domain.Types;
using System;

namespace StreamRecorderLib.Domain
{
    public class Show
    {
        public Show()
        {
            Status = ShowStatusTypes.ShowStatusType.Unknown;
        }

        public int Id { get; set; }
        public string Title { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string FileName { get; set; }
        public ShowStatusTypes.ShowStatusType Status { get; set; }
    }
}