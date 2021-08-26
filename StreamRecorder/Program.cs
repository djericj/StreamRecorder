using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.IO;
using System.Threading.Tasks;

namespace StreamRecorder
{
    internal class Program
    {
        public static IConfigurationRoot configuration;

        private static int Main(string[] args)
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "StreamRecorder.Console.log");
            // Initialize serilog logger
            Log.Logger = new LoggerConfiguration()
                 .WriteTo.Console(Serilog.Events.LogEventLevel.Debug)
                 .WriteTo.File(path)
                 .MinimumLevel.Debug()
                 .Enrich.FromLogContext()
                 .CreateLogger();

            try
            {
                // Start!
                MainAsync(args).Wait();
                return 0;
            }
            catch (Exception e)
            {
                Log.Error(e.Message);
                Log.Error(e.StackTrace);
                return 1;
            }
        }

        private static async Task MainAsync(string[] args)
        {
            // Create service collection
            Log.Information("Creating service collection");
            ServiceCollection serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);

            // Create service provider
            Log.Information("Building service provider");
            IServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();

            // Print connection string to demonstrate configuration object is populated
            Console.WriteLine(configuration.GetConnectionString("DataConnection"));

            try
            {
                Log.Information("Starting service");
                await serviceProvider.GetService<App>().Run();
                Log.Information("Ending service");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Error running service");
                throw ex;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static void ConfigureServices(IServiceCollection serviceCollection)
        {
            // Add logging
            serviceCollection.AddSingleton(LoggerFactory.Create(builder =>
            {
                builder
                    .AddSerilog(dispose: true);
            }));

            serviceCollection.AddLogging();

            // Build configuration
            configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetParent(AppContext.BaseDirectory).FullName)
                .AddJsonFile("appsettings.json", false)
                .Build();

            // Add access to generic IConfigurationRoot
            serviceCollection.AddSingleton(configuration);

            // Add app
            serviceCollection.AddTransient<App>();
        }
    }
}