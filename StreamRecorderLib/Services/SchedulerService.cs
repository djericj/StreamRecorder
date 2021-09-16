using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StreamRecorderLib.Domain;
using StreamRecorderLib.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StreamRecorderLib.Services
{
    public class SchedulerService : ISchedulerService
    {
        private readonly ILogger _logger;
        private readonly IOptions<AppSettings> _appSettings;
        private System.Timers.Timer _timer1;
        private List<Show> _shows;
        private Show _currentShow;
        private Show _firstShow;
        private Show _lastShow;

        private bool _idle = false;
        private bool _recording = false;
        private readonly IRecorderService _recorderService;
        private readonly IFileManagementService _fileManagementService;

        public SchedulerService(ILogger<SchedulerService> logger, IOptions<AppSettings> appSettings, IRecorderService recorderService, IFileManagementService fileManagementService)
        {
            _logger = logger;
            _appSettings = appSettings;
            _recorderService = recorderService;
            _fileManagementService = fileManagementService;
        }

        public async Task Start()
        {
            await Task.Run(() =>
            {
                _shows = GetShows();
                _firstShow = GetFirstShow();
                _lastShow = GetLastShow();

                _timer1 = new System.Timers.Timer
                {
                    Interval = 5000
                };
                _timer1.Elapsed += Timer1_Elapsed;
                _timer1.Start();

                _currentShow = GetCurrentShow();
                if (_currentShow != null)
                {
                    _logger.LogInformation($"Current show is {_currentShow.Title}");
                    if (DateTime.Now.TimeOfDay > _currentShow.StartTime)
                        _currentShow.StartTime = DateTime.Now.TimeOfDay;
                    StartShow(_currentShow);
                }
            });
        }

        public async Task Stop()
        {
            await Task.Run(() =>
            {
                if (_currentShow != null)
                {
                    if (_currentShow.EndTime > DateTime.Now.TimeOfDay)
                        _currentShow.EndTime = DateTime.Now.TimeOfDay;
                    EndShow(_currentShow);
                    _currentShow = null;
                }
            });
        }

        private void StartShow(Show show)
        {
            _logger.LogInformation($"Starting show {_currentShow.Title}");
            var path = $"{_appSettings.Value.SaveFolder}\\" +
                       $"{DateTime.Now:yyy-MM-dd}\\" +
                       $"{show.Id:00}-" +
                       $"{show.Title}-" +
                       $"{DateTime.Now:yyy-MM-dd}-" +
                       $"{show.StartTime:hhmm}-" +
                       $"{show.EndTime:hhmm}.wav";

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
            var newName = existingName[0..^4] + DateTime.Now.TimeOfDay.ToString("hhmm") + ".wav";

            _logger.LogInformation($"Ending show {show.Title}");
            _recorderService.Stop();

            Thread.Sleep(2000);

            File.Move(show.FileName, Path.Combine(Path.GetDirectoryName(show.FileName), newName));
            show.FileName = newName;
        }

        private void Timer1_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            var timeOfDay = DateTime.Now.TimeOfDay;
            if (_idle)
            {
                if (timeOfDay > _firstShow.StartTime && timeOfDay < _firstShow.EndTime)
                {
                    _shows = GetShows();
                    _idle = false;
                    _currentShow = _firstShow;
                }
            }
            if (!_idle)
            {
                if (_currentShow == null) _currentShow = GetNextShow(timeOfDay);

                if (_currentShow != null)
                {
                    if (timeOfDay > _currentShow.StartTime && timeOfDay < _currentShow.EndTime)
                    {
                        // show running
                        if (!_recording)
                        {
                            StartShow(_currentShow);
                            _logger.LogInformation($"Starting {_currentShow.Title}");
                            _logger.LogInformation($"Recording {_currentShow.Title}");
                            _recording = true;
                        }
                    }
                    else if (_recording && timeOfDay > _currentShow.EndTime)
                    {
                        // end show
                        EndShow(_currentShow);
                        _logger.LogInformation($"End show {_currentShow.Title}");
                        _logger.LogInformation($"Stop recording {_currentShow.Title}");

                        _currentShow = null;
                        _recording = false;

                        if (timeOfDay > _lastShow.EndTime) _idle = true;
                    }
                    else
                    {
                        _logger.LogInformation($"Next show starts at {_currentShow.StartTime.ToString(@"hh\:mm")}");
                        _idle = true;
                    }
                }
            }
        }

        private Show GetCurrentShow() => _shows.Where(x => TimeBetween(DateTime.Now, x.StartTime, x.EndTime)).FirstOrDefault();

        private Show GetFirstShow() => _shows.OrderBy(x => x.StartTime).FirstOrDefault();

        private Show GetLastShow() => _shows.OrderByDescending(x => x.EndTime).FirstOrDefault();

        private Show GetNextShow(Show show) => _shows.SkipWhile(item => item.Title != show.Title).Skip(1).FirstOrDefault();

        private Show GetNextShow(TimeSpan timeOfDay)
        {
            var s = _shows.Where(x => timeOfDay > x.StartTime && timeOfDay < x.EndTime).FirstOrDefault();
            if (s == null) s = GetFirstShow();
            return s;
        }

        private List<Show> GetShows() => _appSettings.Value.Schedule;

        private static bool TimeBetween(DateTime datetime, TimeSpan start, TimeSpan end)
        {
            TimeSpan now = datetime.TimeOfDay;
            if (start < end)
                return start <= now && now <= end;
            return !(end < now && now < start);
        }
    }
}