using NAudio.Lame;
using NAudio.Wave;
using System;
using System.IO;
using System.Threading.Tasks;

namespace StreamRecorder.Converter
{
    public class ConverterService : IConverterService
    {
        public event EventHandler<ProgressEventArgs> Progress;

        public event EventHandler<string> Message;

        private static long input_length = 0;
        private bool _converting = false;

        public void Convert(string source, string target, LAMEPreset bitRate, ID3TagData tag = null)
        {
            ConvertWav(source, target, bitRate, tag);
        }

        public void Convert(string source, string target, LAMEPreset bitRate,
            string title = "", string artist = "", string album = "", string year = "", string comment = "", string genre = "")
        {
            ID3TagData tag = new ID3TagData
            {
                Title = title,
                Artist = artist,
                Album = album,
                Year = year,
                Comment = comment,
                Genre = genre
                //Subtitle = "From the Calligraphy theme"
            };

            Convert(source, target, bitRate, tag);
        }

        public async Task ConvertAsync(string source, string target, LAMEPreset bitRate, ID3TagData tag = null)
        {
            await Task.Run(() =>
            {
                ConvertWav(source, target, bitRate, tag);
            });
        }

        public async Task ConvertAsync(string source, string target, LAMEPreset bitRate,
            string title = "", string artist = "", string album = "", string year = "", string comment = "", string genre = "")
        {
            await Task.Run(() =>
            {
                Convert(source, target, bitRate, title, artist, album, year, comment, genre);
            });
        }

        private void Writer_OnProgress(object writer, long inputBytes, long outputBytes, bool finished)
        {
            Progress?.Invoke(this, new ProgressEventArgs((inputBytes * 100.0) / input_length, outputBytes, ((double)inputBytes) / Math.Max(1, outputBytes)));
            if (finished)
                Message?.Invoke(this, $"Conversion done.");
        }

        private void ConvertWav(string source, string target, LAMEPreset bitRate, ID3TagData tag = null)
        {
            if (!_converting)
            {
                Message?.Invoke(this, $"Converting {source} to {target}");
                if (File.Exists(target)) File.Delete(target);
                using (var reader = new WaveFileReader(source))
                using (var writer = new LameMP3FileWriter(target, reader.WaveFormat, bitRate, tag))
                {
                    _converting = true;
                    writer.MinProgressTime = 250;
                    input_length = reader.Length;
                    writer.OnProgress += Writer_OnProgress;

                    reader.CopyTo(writer);
                }
                _converting = false;
            }
        }
    }
}