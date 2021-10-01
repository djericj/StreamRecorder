using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StreamRecorder.Converter;
using StreamRecorder.Logging;
using StreamRecorderLib.Domain;
using StreamRecorderLib.Interfaces;
using StreamRecorderLib.Services;
using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace StreamRecorder
{
    internal sealed class Program
    {
        private static async Task Main(string[] args)
        {
            await Host.CreateDefaultBuilder(args)
                .UseContentRoot(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location))
                .ConfigureLogging(builder =>
                {
                    builder.ClearProviders().AddColorConsoleLogger(configuration =>
                    {
                        configuration.LogLevels.Add(LogLevel.Warning, ConsoleColor.Yellow);
                        configuration.LogLevels.Add(LogLevel.Error, ConsoleColor.Red);
                    });
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<ConsoleHostedService>();
                    services.AddSingleton<ISchedulerService, SchedulerService>();
                    services.AddSingleton<IRecorderService, StreamRecorderService>();
                    services.AddSingleton<IConverterService, ConverterService>();
                    services.AddOptions<AppSettings>().Bind(hostContext.Configuration.GetSection("AppConfig"));
                    services.AddLogging();
                })
                .RunConsoleAsync();
        }
    }

    internal sealed class ConsoleHostedService : IHostedService
    {
        private readonly ILogger _logger;
        private readonly IHostApplicationLifetime _appLifetime;
        private readonly ISchedulerService _schedulerService;
        private readonly IConverterService _converterService;

        public ConsoleHostedService(
            ILogger<ConsoleHostedService> logger,
            IHostApplicationLifetime appLifetime,
            ISchedulerService schedulerService,
            IConverterService converterService)
        {
            _logger = logger;
            _appLifetime = appLifetime;
            _schedulerService = schedulerService;
            _converterService = converterService;
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
                        _schedulerService.ShowStarted += _schedulerService_ShowStarted;
                        _schedulerService.ShowEnded += _schedulerService_ShowEnded;
                        await _schedulerService.Start();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Unhandled exception!");
                    }
                });
            });

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping...");
            _schedulerService.Stop();
            _schedulerService.ShowStarted -= _schedulerService_ShowStarted;
            _schedulerService.ShowEnded -= _schedulerService_ShowEnded;
            Thread.Sleep(5000);
            return Task.CompletedTask;
        }

        private void _schedulerService_ShowStarted(object sender, Show e)
        {
            _logger.LogInformation($"Show {e.Title} started.");
        }

        private void _schedulerService_ShowEnded(object sender, Show e)
        {
            _logger.LogInformation($"Show {e.Title} ended.");
            //var source = e.FileName;
            //var target = source.Replace(".wav", ".mp3");
            //Task.Run(async () =>
            //{
            //    await _converterService.ConvertAsync(source, target, NAudio.Lame.LAMEPreset.ABR_128, title: e.Title, artist: "", year: DateTime.Now.Year.ToString());
            //})
            //.Wait();
            //if (!File.Exists(target))
            //{
            //    _logger.LogError($"Failed to find a target file after converting at {target}");
            //}
            //else
            //File.Delete(source);
        }
    }
}