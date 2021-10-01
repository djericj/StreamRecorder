using System.Collections.Generic;

namespace StreamRecorderLib.Domain
{
    public class Station
    {
        public string Name { get; set; }
        public string CallSign { get; set; }
        public string Genre { get; set; }
        public string Location { get; set; }
        public List<string> Playlist { get; set; }
        public List<Show> Shows { get; set; }
    }
}