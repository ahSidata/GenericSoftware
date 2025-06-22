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
        private GrowattMqttServer _growattMqttServer;

        public MqttProxyWorker(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;

            _growattMqttServer = new GrowattMqttServer(
                proxyCertPath: "certs/server.crt",
                proxyKeyPath: "certs/server.key",
                7006);

            _pythonWrapper = new PythonWrapper(ServiceProvider);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await StartBrokerAsync();
            await StartProxyAsync();
        }

        private async Task StartBrokerAsync()
        {
            await _growattMqttServer.StartAsync();
        }

        private async Task StartProxyAsync()
        {
            _pythonWrapper.StartPythonClient();
            await Task.CompletedTask;

            //var proxy = new GrowattMqttProxy(
            //    brokerHost: "localhost",
            //    brokerPort: 7006,
            //    growattHost: "mqtt.growatt.com",
            //    growattPort: 7006);

            //await proxy.StartAsync();
        }
    }
}
