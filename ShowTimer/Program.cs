using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace ShowTimer
{
    internal sealed class Program
    {
        private static async Task Main(string[] args)
        {
            await Host.CreateDefaultBuilder(args)
                .UseContentRoot(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location))
                .ConfigureLogging(logging =>
                {
                    // Add any 3rd party loggers like NLog or Serilog
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<ConsoleHostedService>();
                })
                .RunConsoleAsync();
        }
    }

    internal class ConsoleHostedService : IHostedService
    {
        private readonly ILogger _logger;
        private readonly IHostApplicationLifetime _appLifetime;
        private System.Timers.Timer timer1;
        private Show currentShow;
        private Show _nextShow;
        private bool _recording = false;
        private List<Show> _shows;
        private bool idle = false;

        private int? _exitCode;

        public ConsoleHostedService(ILogger<ConsoleHostedService> logger, IHostApplicationLifetime appLifetime)
        {
            _logger = logger;
            _appLifetime = appLifetime;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug($"Starting with arguments: {string.Join(" ", Environment.GetCommandLineArgs())}");

            _appLifetime.ApplicationStarted.Register(() =>
            {
                Task.Run(async () =>
                {
                    try
                    {
                        _shows = GetShows();

                        timer1 = new System.Timers.Timer();
                        timer1.Interval = 5000;
                        timer1.Elapsed += Timer1_Elapsed;
                        timer1.Start();

                        currentShow = GetCurrentShow();
                        if (currentShow != null)
                            _logger.LogInformation($"Current show is {currentShow.Title}");
                        else
                            currentShow = GetNextShow(DateTime.Now.TimeOfDay);

                        _exitCode = 0;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Unhandled exception!");

                        _exitCode = 1;
                    }
                    finally
                    {
                        // Stop the application once the work is done
                        //_appLifetime.StopApplication();
                    }
                });
            });

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Called Stop Async");
            // Exit code may be null if the user cancelled via Ctrl+C/SIGTERM
            Environment.ExitCode = _exitCode.GetValueOrDefault(-1);
            return Task.CompletedTask;
        }

        private void Timer1_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            var timeOfDay = DateTime.Now.TimeOfDay;
            var firstShow = _shows.OrderBy(x => x.StartTime).FirstOrDefault();
            var lastShow = _shows.OrderByDescending(x => x.EndTime).FirstOrDefault();
            if (idle)
            {
                if (timeOfDay > firstShow.StartTime && timeOfDay < firstShow.EndTime)
                {
                    idle = false;
                    currentShow = firstShow;
                }
            }
            if (!idle)
            {
                if (currentShow == null)
                {
                    currentShow = GetNextShow(timeOfDay);
                }
                if (currentShow != null)
                {
                    if (timeOfDay > currentShow.StartTime && timeOfDay < currentShow.EndTime)
                    {
                        // show running
                        if (!_recording)
                        {
                            _logger.LogInformation($"Starting {currentShow.Title}");
                            _logger.LogInformation($"Recording {currentShow.Title}");
                            _recording = true;
                        }
                    }
                    else if (_recording && timeOfDay > currentShow.EndTime)
                    {
                        // end show
                        _logger.LogInformation($"End show {currentShow.Title}");
                        _logger.LogInformation($"Stop recording {currentShow.Title}");

                        currentShow = null;
                        _recording = false;

                        if (timeOfDay > lastShow.EndTime)
                        {
                            idle = true;
                        }
                    }
                    else
                    {
                        _logger.LogInformation($"Next show starts at {currentShow.StartTime.ToString(@"hh\:mm")}");
                        idle = true;
                    }
                }
            }
        }

        private List<Show> GetShows()
        {
            var shows = new List<Show>();
            shows.Add(new Show() { Title = "The Musers", StartTime = new TimeSpan(5, 30, 0), EndTime = new TimeSpan(10, 00, 0) });
            shows.Add(new Show() { Title = "Norm and D", StartTime = new TimeSpan(10, 00, 0), EndTime = new TimeSpan(12, 0, 0) });
            shows.Add(new Show() { Title = "The Hang Zone", StartTime = new TimeSpan(12, 0, 0), EndTime = new TimeSpan(15, 0, 0) });
            shows.Add(new Show() { Title = "The Hardline", StartTime = new TimeSpan(15, 0, 0), EndTime = new TimeSpan(19, 0, 0) });
            shows.Add(new Show() { Title = "The Ticket Top 10", StartTime = new TimeSpan(19, 0, 0), EndTime = new TimeSpan(21, 0, 0) });
            return shows;
        }

        private Show GetCurrentShow()
        {
            return _shows.Where(x => TimeBetween(DateTime.Now, x.StartTime, x.EndTime)).FirstOrDefault();
        }

        private Show GetFirstShow() => _shows.OrderBy(x => x.StartTime).FirstOrDefault();

        private Show GetNextShow(Show currentShow)
        {
            var s = _shows.SkipWhile(item => item.Title != currentShow.Title).Skip(1).FirstOrDefault();
            if (s == null) s = GetFirstShow();
            return s;
        }

        private Show GetNextShow(TimeSpan timeOfDay)
        {
            var s = _shows.Where(x => timeOfDay > x.StartTime && timeOfDay < x.EndTime).FirstOrDefault();
            if (s == null) s = GetFirstShow();
            return s;
        }

        private static bool TimeBetween(DateTime datetime, TimeSpan start, TimeSpan end)
        {
            TimeSpan now = datetime.TimeOfDay;
            if (start < end)
                return start <= now && now <= end;
            return !(end < now && now < start);
        }
    }

    internal class Show
    {
        public string Title { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
    }
}