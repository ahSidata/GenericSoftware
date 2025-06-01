using MQTTnet;
using MQTTnet.Server;
using System.Collections.Concurrent;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using MQTTnet.Packets;
using System.Text.Json;
using System.Buffers;
using MQTTnet.Diagnostics.Logger;

namespace EnergyAutomate.Emulator
{
    public class GrowattMqttProxy
    {
        private readonly string _proxyCertPath;
        private readonly string _proxyKeyPath;
        private readonly int _proxyPort;

        private GrowattMqttServer GrowattMqttServer { get; set; } 

        public GrowattMqttProxy(string proxyCertPath, string proxyKeyPath, int proxyPort, MqttNetEventLogger mqttNetEventLogger)
        {
            _proxyCertPath = proxyCertPath;
            _proxyKeyPath = proxyKeyPath;
            _proxyPort = proxyPort;

            GrowattMqttServer = new GrowattMqttServer(proxyCertPath, proxyKeyPath, proxyPort, mqttNetEventLogger);            
        }

        public async Task StartAsync()
        {
            await GrowattMqttServer.StartAsync();
        }

        public async Task StopAsync()
        {
            await Task.CompletedTask;
        }
    }
}
