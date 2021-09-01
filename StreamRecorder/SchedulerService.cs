using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StreamRecorder.Domain;
using StreamRecorder.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StreamRecorder
{
    public class SchedulerService : ISchedulerService
    {
        private readonly ILogger _logger;
        private readonly IOptions<AppSettings> _appSettings;
        private System.Timers.Timer timer1;
        private List<Show> _shows;
        private Show currentShow;
        private bool idle = false;
        private IRecorderService _recorderService;

        public SchedulerService(ILogger<SchedulerService> logger, IOptions<AppSettings> appSettings, IRecorderService recorderService)
        {
            _logger = logger;
            _appSettings = appSettings;
            _recorderService = recorderService;
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
                    if (DateTime.Now.TimeOfDay > currentShow.StartTime)
                        currentShow.StartTime = DateTime.Now.TimeOfDay;
                    StartShow(currentShow);
                }
            });
        }

        public async Task Stop()
        {
            await Task.Run(() =>
            {
                if (currentShow != null)
                {
                    if (currentShow.EndTime > DateTime.Now.TimeOfDay)
                        currentShow.EndTime = DateTime.Now.TimeOfDay;
                    EndShow(currentShow);
                    currentShow = null;
                }
            });
        }

        private void StartShow(Show show)
        {
            _logger.LogInformation($"Starting show {currentShow.Title}");
            var path = $"{_appSettings.Value.SaveFolder}\\" +
                       $"{DateTime.Now.ToString("yyy-MM-dd")}\\" +
                       $"{show.Title}-" +
                       $"{DateTime.Now.ToString("yyy-MM-dd")}-" +
                       $"{show.StartTime.ToString("hhmm")}-" +
                       $"{show.EndTime.ToString("hhmm")}.wav";

            if (!Directory.Exists(Path.GetDirectoryName(path)))
                Directory.CreateDirectory(Path.GetDirectoryName(path));

            if (File.Exists(path))
                File.Delete(path);

            _recorderService.Record(path);

            show.FileName = path;
        }

        private void EndShow(Show show)
        {
            var existingName = Path.GetFileNameWithoutExtension(show.FileName);
            var newName = existingName.Substring(0, existingName.Length - 4) + DateTime.Now.TimeOfDay.ToString("hhmm") + ".wav";

            _logger.LogInformation($"Ending show {show.Title}");
            _recorderService.Stop();

            Thread.Sleep(2000);

            File.Move(show.FileName, Path.Combine(Path.GetDirectoryName(show.FileName), newName));
            show.FileName = newName;
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
                    }
                }
            }
            else
            {
                var firstShow = GetFirstShow();
                if (!idle) // NOT idle
                {
                    _logger.LogInformation($"Idle.  Next show starts at {firstShow.StartTime.ToString("hh\\:mm")}");
                    idle = true;
                }
                else // IS idle
                {
                    if (TimeBetween(DateTime.Now, firstShow.StartTime, firstShow.EndTime))
                    {
                        StartShow(currentShow);
                        idle = false;
                    }
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