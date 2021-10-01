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

        public event EventHandler<Show> ShowStarted;

        public event EventHandler<Show> ShowEnded;

        public SchedulerService(ILogger<SchedulerService> logger,
                                IOptions<AppSettings> appSettings,
                                IRecorderService recorderService)
        {
            _logger = logger;
            _appSettings = appSettings;
            _recorderService = recorderService;
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
            if (show.Status != Domain.Types.ShowStatusTypes.ShowStatusType.Started)
            {
                var now = DateTime.Now;
                show.StartTime = new TimeSpan(now.Hour, now.Minute, now.Second);
                _logger.LogInformation($"Starting show {_currentShow.Title}");
                var path = @$"{_appSettings.Value.SaveFolder}\" +
                           @$"{_appSettings.Value.Station.CallSign}\" +
                           @$"{now:yyy-MM-dd}\" +
                           $"{show.Id:00}-" +
                           $"{show.Title}-" +
                           $"{now:yyy-MM-dd}-" +
                           $"{now:HHmm}-" +
                           $"{show.EndTime:hhmm}.wav";

                if (!Directory.Exists(Path.GetDirectoryName(path)))
                    Directory.CreateDirectory(Path.GetDirectoryName(path));

                if (File.Exists(path))
                    File.Delete(path);

                _recorderService.RecorderEvent += _recorderService_RecorderEvent;
                _recorderService.RecorderException += _recorderService_RecorderException;

                _recorderService.Record(path);

                show.Status = Domain.Types.ShowStatusTypes.ShowStatusType.Started;
                show.FileName = path;
                ShowStarted?.Invoke(this, show);
            }
        }

        private void EndShow(Show show)
        {
            if (show.Status != Domain.Types.ShowStatusTypes.ShowStatusType.Ended)
            {
                var existingName = Path.GetFileNameWithoutExtension(show.FileName);
                var newName = existingName[0..^4] + DateTime.Now.TimeOfDay.ToString("hhmm") + ".wav";

                _logger.LogInformation($"Ending show {show.Title}");
                _recorderService.Stop();
                _recorderService.RecorderEvent -= _recorderService_RecorderEvent;
                _recorderService.RecorderException -= _recorderService_RecorderException;

                Thread.Sleep(2000);

                File.Move(show.FileName, Path.Combine(Path.GetDirectoryName(show.FileName), newName));

                show.Status = Domain.Types.ShowStatusTypes.ShowStatusType.Ended;
                show.FileName = newName;
                ShowEnded?.Invoke(this, show);
            }
        }

        private void Timer1_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            var timeOfDay = DateTime.Now.TimeOfDay;
            var firstShow = _shows.OrderBy(x => x.StartTime).FirstOrDefault();
            var lastShow = _shows.OrderByDescending(x => x.EndTime).FirstOrDefault();
            if (_idle)
            {
                if (timeOfDay > firstShow.StartTime && timeOfDay < firstShow.EndTime)
                {
                    _idle = false;
                    _currentShow = firstShow;
                }
            }
            if (!_idle)
            {
                if (_currentShow == null)
                {
                    _currentShow = GetNextShow(timeOfDay);
                }
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

                        _recording = false;
                        _currentShow = GetNextShow(_currentShow);

                        if (timeOfDay > lastShow.EndTime)
                        {
                            _idle = true;
                        }
                    }
                    else
                    {
                        _logger.LogInformation($"Next show starts at {_currentShow.StartTime.ToString(@"hh\:mm")}");
                        _idle = true;
                    }
                }
            }
        }

        private void _recorderService_RecorderException(object sender, Events.RecorderExceptionArgs e)
        {
            if (e.Exception.Message.Contains("NoDriver calling waveOutWrite")) // handles a restart of the Windows Audio Service
            {
                EndShow(_currentShow);
                if (_appSettings.Value.AutoRestart)
                {
                    _logger.LogWarning("Attempting restart...");
                    StartShow(_currentShow);
                }
            }
        }

        private void _recorderService_RecorderEvent(object sender, Events.RecorderEventArgs e) => _logger.Log(e.LogLevel, e.Message);

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

        private List<Show> GetShows() => _appSettings.Value.Station.Shows.OrderBy(x => x.StartTime).ToList();

        private static bool TimeBetween(DateTime datetime, TimeSpan start, TimeSpan end)
        {
            TimeSpan now = datetime.TimeOfDay;
            if (start < end)
                return start <= now && now <= end;
            return !(end < now && now < start);
        }
    }
}