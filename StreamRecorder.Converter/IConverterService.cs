using NAudio.Lame;
using System.Threading.Tasks;

namespace StreamRecorder.Converter
{
    public interface IConverterService
    {
        void Convert(string source, string target, LAMEPreset bitRate, ID3TagData tag = null);

        void Convert(string source, string target, LAMEPreset bitRate,
            string title = "", string artist = "", string album = "", string year = "", string comment = "", string genre = "");

        Task ConvertAsync(string source, string target, LAMEPreset bitRate, ID3TagData tag = null);

        Task ConvertAsync(string source, string target, LAMEPreset bitRate,
            string title = "", string artist = "", string album = "", string year = "", string comment = "", string genre = "");
    }
}