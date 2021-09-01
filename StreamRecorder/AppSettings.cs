using StreamRecorder.Domain;
using System.Collections.Generic;

namespace StreamRecorder
{
    public class AppSettings
    {
        public List<string> Playlist { get; set; }
        public bool RecordOn { get; set; }
        public string SaveFolder { get; set; }
        public List<Show> Schedule { get; set; }
    }
}