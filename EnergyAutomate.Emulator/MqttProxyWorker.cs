using EnergyAutomate.Emulator.Growatt;
using EnergyAutomate.Emulator.Growatt.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EnergyAutomate.Emulator
{
    public class MqttProxyWorker : BackgroundService
    {
        private IServiceProvider ServiceProvider { get; set; }

        private ILogger<MqttProxyWorker> Logger => ServiceProvider.GetRequiredService<ILogger<MqttProxyWorker>>();

        private readonly PythonWrapper _pythonWrapper;
        private readonly IConfiguration _configuration;

        public MqttProxyWorker(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
            _configuration = serviceProvider.GetRequiredService<IConfiguration>();
            _pythonWrapper = serviceProvider.GetRequiredService<PythonWrapper>();

            _pythonWrapper.GrowattClientOptions = _configuration.GetSection("GrowattClient").Get<GrowattClientOptions>()
                ?? throw new InvalidOperationException("GrowattClient configuration section is missing or invalid in appsettings.");
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                Logger.LogTrace("Starting MQTT proxy worker");
                _pythonWrapper.StartPythonClient();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "MQTT proxy worker failed to start");
            }

            return Task.CompletedTask;
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                Logger.LogTrace("Stopping MQTT proxy worker");
                _pythonWrapper.StopPythonClient();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "MQTT proxy worker failed to stop");
            }

            return base.StopAsync(cancellationToken);
        }

    }
}
