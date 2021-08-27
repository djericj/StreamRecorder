using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static StreamRecorder.StreamRecorder;

namespace StreamRecorder
{
    public class App
    {
        private System.Timers.Timer timer1;
        private readonly IConfigurationRoot _config;
        private readonly ILogger<App> _logger;
        private static bool idle = false;
        private Show currentShow;
        private List<Show> _shows;
        private IEnumerable<string> _playlist;
        private StreamRecorder _streamRecorder;

        public App(IConfigurationRoot config, ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<App>();
            _config = config;
        }

        public async Task Run()
        {
            _shows = _config.GetSection("AppConfig:Schedule").Get<List<Show>>();
            _playlist = ReadPlaylist();

            timer1 = new System.Timers.Timer();
            timer1.Interval = 5000;
            timer1.Elapsed += Timer1_Elapsed; ;
            timer1.Start();

            currentShow = GetCurrentShow();
            if (currentShow != null)
            {
                _logger.LogInformation($"Current show is {currentShow.Title}");
                StartShow(currentShow);
            }

            if (_streamRecorder != null)
            {
                if (Console.ReadKey().Key == ConsoleKey.P)
                {
                    if (_streamRecorder.PlaybackState == StreamingPlaybackState.Playing) _streamRecorder.Pause();
                    else _streamRecorder.UnPause();
                }
                else if (Console.ReadKey().Key == ConsoleKey.S)
                {
                    _streamRecorder.Stop();
                }
                else if (Console.ReadKey().Key == ConsoleKey.Q)
                {
                    _streamRecorder.Stop();
                }
            }
        }

        private void StartShow(Show show)
        {
            var basePath = _config.GetSection("AppConfig:SaveFolder").Value;
            var today = DateTime.Now.ToString("yyy-MM-dd");
            var fileName = $"{show.Title}-{today}-{show.StartTime.ToString("hhmm")}-{show.EndTime.ToString("hhmm")}";
            var filePath = $@"{basePath}\{today}";
            var savePath = $@"{filePath}\{fileName}.wav";
            if (!Directory.Exists(filePath)) Directory.CreateDirectory(filePath);
            if (File.Exists(savePath)) File.Delete(savePath);
            _streamRecorder = new StreamRecorder(_config, savePath);
            _streamRecorder.StreamEventMessage += StreamRecorder_StreamEventMessage;
            _streamRecorder.BufferProgressEvent += StreamRecorder_BufferProgressEvent;
            _streamRecorder.Play();
        }

        private void EndShow(Show show)
        {
            _streamRecorder.Stop();
        }

        private void Timer1_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (currentShow != null)
            {
                if (!TimeBetween(DateTime.Now, currentShow.StartTime, currentShow.EndTime))
                {
                    EndShow(currentShow);

                    currentShow = GetCurrentShow();

                    if (currentShow != null)
                    {
                        StartShow(currentShow);
                        _logger.LogInformation($"Change Show: Starting {currentShow.Title}");
                    }
                }
            }
            else
            {
                if (!idle)
                {
                    _logger.LogInformation($"Idle.  Next show starts at {GetFirstShow().StartTime.ToString("hh\\:mm")}");
                    idle = true;
                }
            }
        }

        private void StreamRecorder_BufferProgressEvent(object sender, BufferProgressEvent e)
        {
            _logger.LogInformation(e.TotalSeconds.ToString());
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

        private Show GetCurrentShow() => _shows.Where(x => TimeBetween(DateTime.Now, x.StartTime, x.EndTime)).FirstOrDefault();

        private Show GetFirstShow() => _shows.OrderBy(x => x.StartTime).FirstOrDefault();

        private Show GetNextShow(Show show) => _shows.SkipWhile(item => item.Title != show.Title).Skip(1).FirstOrDefault();

        private static bool TimeBetween(DateTime datetime, TimeSpan start, TimeSpan end)
        {
            TimeSpan now = datetime.TimeOfDay;
            if (start < end)
                return start <= now && now <= end;
            return !(end < now && now < start);
        }
    }
}