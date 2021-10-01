using System.Collections.Generic;

namespace StreamRecorderLib.Domain
{
    public class AppSettings
    {
        public int DaysToKeep { get; set; }
        public bool RecordOn { get; set; }
        public bool AutoRestart { get; set; }
        public string SaveFolder { get; set; }
        public Station Station { get; set; }
    }
}