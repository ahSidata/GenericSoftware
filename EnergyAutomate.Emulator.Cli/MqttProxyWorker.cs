using Microsoft.Extensions.Hosting;
using MQTTnet.Diagnostics.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnergyAutomate.Emulator.Cli
{
    public class MqttProxyWorker : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var mqttEventLogger = new MqttNetEventLogger("MeinProxyLogger");

            var proxy = new GrowattMqttProxy(
                proxyCertPath: "certs/server.crt",
                proxyKeyPath: "certs/server.key",
                brokerHost: "ah.azure.sidata.com",
                brokerPort: 7006,
                growattHost: "mqtt.growatt.com",
                growattPort: 7006);

            await proxy.StartAsync();
        }
    }
}
