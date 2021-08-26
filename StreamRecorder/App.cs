using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static StreamRecorder.StreamRecorder;

namespace StreamRecorder
{
    public class App
    {
        private readonly IConfigurationRoot _config;
        private readonly ILogger<App> _logger;

        public App(IConfigurationRoot config, ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<App>();
            _config = config;
        }

        public async Task Run()
        {
            var streamRecorder = new StreamRecorder(_config);
            streamRecorder.StreamEventMessage += StreamRecorder_StreamEventMessage;
            var playlist = ReadPlaylist();
            streamRecorder.Play(playlist.ToArray());
            _logger.LogInformation("P = Play/Pause, S = Stop, Q = Quit");
            while (true)
            {
                if (Console.ReadKey().Key == ConsoleKey.P)
                {
                    if (streamRecorder.PlaybackState == StreamingPlaybackState.Playing) streamRecorder.Pause();
                    else streamRecorder.UnPause();
                }
                else if (Console.ReadKey().Key == ConsoleKey.S)
                {
                    streamRecorder.Stop();
                }
                else if (Console.ReadKey().Key == ConsoleKey.Q)
                {
                    streamRecorder.Stop();
                    break;
                }
            }
        }

        private void StreamRecorder_StreamEventMessage(object sender, StreamEventMessage e)
        {
            _logger.LogInformation(e.Message);
        }

        private IEnumerable<string> ReadPlaylist()
        {
            var playlistFile = _config.GetSection("AppConfig:Playlist").Value;
            var path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, playlistFile);
            string line;
            List<string> urls = new List<string>();
            System.IO.StreamReader file = new System.IO.StreamReader(path);
            while ((line = file.ReadLine()) != null)
                if (line.StartsWith("File"))
                    urls.Add(line.Substring(line.IndexOf("=") + 1));

            file.Close();

            return urls;
        }
    }
}