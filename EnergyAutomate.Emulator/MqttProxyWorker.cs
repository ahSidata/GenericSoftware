using EnergyAutomate.Emulator.Growatt;
using EnergyAutomate.Emulator.Growatt.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EnergyAutomate.Emulator
{
    public class MqttProxyWorker : BackgroundService
    {
        private IServiceProvider ServiceProvider { get; set; }

        private ILogger<MqttProxyWorker> Logger => ServiceProvider.GetRequiredService<ILogger<MqttProxyWorker>>();

        private PythonWrapper _pythonWrapper;

        public MqttProxyWorker(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
            _pythonWrapper = new PythonWrapper(ServiceProvider);

            _pythonWrapper.GrowattClientOptions = new GrowattClientOptions()
            {
                ClientId = "0PVP50ZR16ST00CB",
                BrokerHost = "ah.azure.sidata.com",
                BrokerPort = 7006,
                GrowattHost = "mqtt.growatt.com",
                GrowattPort = 7006
            };
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _pythonWrapper.StartPythonClient();

            return Task.CompletedTask;
        }

    }
}
