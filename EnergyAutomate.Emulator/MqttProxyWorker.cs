using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet.Diagnostics.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnergyAutomate.Emulator
{
    public class MqttProxyWorker : BackgroundService
    {
        private PythonWrapper _pythonWrapper;
        private GrowattMqttServer _growattMqttServer;

        public MqttProxyWorker()
        {
            _growattMqttServer = new GrowattMqttServer(
                proxyCertPath: "certs/server.crt",
                proxyKeyPath: "certs/server.key",
                7006);

            _pythonWrapper = new PythonWrapper();
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
