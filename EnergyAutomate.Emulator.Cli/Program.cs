using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EnergyAutomate.Emulator.Cli
{
    internal class Program
    {



        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                services.AddHostedService<MqttProxyWorker>();
            });

        public static void Main(string[] args)
        {
            AppContext.SetSwitch("System.Net.EnableSslKeyLogging", true);

            Console.WriteLine("[TRACE] Program start");
            Console.WriteLine("[TRACE] .NET Version: " + Environment.Version);
            string keylogPath = Environment.GetEnvironmentVariable("SSLKEYLOGFILE");
            Console.WriteLine("[TRACE] SSLKEYLOGFILE: " + (keylogPath ?? "(not set)"));

            if (!string.IsNullOrWhiteSpace(keylogPath))
            {
                try
                {
                    File.AppendAllText(keylogPath, "[TRACE] Test write: " + DateTime.Now + Environment.NewLine);
                    Console.WriteLine("[TRACE] Test line written to SSLKEYLOGFILE");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[TRACE] Could not write to SSLKEYLOGFILE: " + ex);
                }
            }

            Console.WriteLine("[TRACE] Starting as async service/daemon");
            CreateHostBuilder(args).Build().Run();
        }
    }
}
