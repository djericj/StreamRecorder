using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;
using System;

namespace StreamRecorder.Logging
{
    public static class ColorConsoleLoggerExtensions
    {
        public static ILoggingBuilder AddColorConsoleLogger(
            this ILoggingBuilder builder)
        {
            builder.AddConfiguration();

            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<ILoggerProvider, ColorConsoleLoggerProvider>());

            LoggerProviderOptions.RegisterProviderOptions
                <ColorConsoleLoggerConfiguration, ColorConsoleLoggerProvider>(builder.Services);

            return builder;
        }

        public static ILoggingBuilder AddColorConsoleLogger(
            this ILoggingBuilder builder,
            Action<ColorConsoleLoggerConfiguration> configure)
        {
            builder.AddColorConsoleLogger();
            builder.Services.Configure(configure);

            return builder;
        }

        public static string Abbreviate(this LogLevel logLevel)
        {
            if (logLevel == LogLevel.Debug) return "dbug";
            if (logLevel == LogLevel.Warning) return "warn";
            if (logLevel == LogLevel.Information) return "info";
            if (logLevel == LogLevel.Trace) return "trac";
            if (logLevel == LogLevel.Critical) return "crit";
            if (logLevel == LogLevel.Error) return "fail";
            return string.Empty;
        }
    }
}