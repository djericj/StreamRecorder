using System.Collections.Generic;

namespace StreamRecorderLib.Domain
{
    public class AppSettings
    {
        public List<string> Playlist { get; set; }
        public int DaysToKeep { get; set; }
        public bool RecordOn { get; set; }
        public bool AutoRestart { get; set; }
        public string SaveFolder { get; set; }
        public List<Show> Schedule { get; set; }
    }
}