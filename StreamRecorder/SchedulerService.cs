using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace StreamRecorder
{
    public interface ISchedulerService
    {
        Task Start();

        Task Stop();
    }

    public class SchedulerService : ISchedulerService
    {
        private readonly ILogger _logger;
        private readonly IOptions<AppSettings> _appSettings;
        private System.Timers.Timer timer1;
        private List<Show> _shows;
        private Show currentShow;
        private bool idle = false;
        private StreamRecorder _streamRecorder;

        public SchedulerService(ILogger<SchedulerService> logger, IOptions<AppSettings> appSettings)
        {
            _logger = logger;
            _appSettings = appSettings;
        }

        public async Task Start()
        {
            await Task.Run(() =>
            {
                _shows = _appSettings.Value.Schedule;

                timer1 = new System.Timers.Timer();
                timer1.Interval = 5000;
                timer1.Elapsed += Timer1_Elapsed;
                timer1.Start();

                currentShow = GetCurrentShow();
                if (currentShow != null)
                {
                    _logger.LogInformation($"Current show is {currentShow.Title}");
                    StartShow(currentShow);
                }
            });
        }

        public async Task Stop()
        {
            await Task.Run(() =>
            {
                if (currentShow != null)
                    EndShow(currentShow);
            });
        }

        private void StartShow(Show show)
        {
            var basePath = _appSettings.Value.SaveFolder;
            var today = DateTime.Now.ToString("yyy-MM-dd");
            var fileName = $"{show.Title}-{today}-{show.StartTime.ToString("hhmm")}-{show.EndTime.ToString("hhmm")}";
            var filePath = $@"{basePath}\{today}";
            var savePath = $@"{filePath}\{fileName}.wav";
            if (!Directory.Exists(filePath)) Directory.CreateDirectory(filePath);
            if (File.Exists(savePath)) File.Delete(savePath);
            _streamRecorder = new StreamRecorder(_appSettings, savePath);
            _streamRecorder.StreamEventMessage += StreamRecorder_StreamEventMessage;
            _streamRecorder.BufferProgressEvent += StreamRecorder_BufferProgressEvent;
            _streamRecorder.Play();
        }

        private void EndShow(Show show)
        {
            _logger.LogInformation($"Ending show {show.Title}");
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

        private void StreamRecorder_BufferProgressEvent(object sender, BufferProgressEvent e) => _logger.LogInformation(e.TotalSeconds.ToString());

        private void StreamRecorder_StreamEventMessage(object sender, StreamEventMessage e) => _logger.LogInformation(e.Message);

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