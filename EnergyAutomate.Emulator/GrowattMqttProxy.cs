using MQTTnet;
using MQTTnet.Server;
using System.Collections.Concurrent;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using MQTTnet.Packets;
using System.Text.Json;
using System.Buffers;

namespace EnergyAutomate.Emulator
{
    public class GrowattMqttProxy
    {
        private readonly string _proxyCertPath;
        private readonly string _proxyKeyPath;
        private readonly int _proxyPort;

        private GrowattMqttServer GrowattMqttServer { get; set; } 

        public GrowattMqttProxy(string proxyCertPath, string proxyKeyPath, int proxyPort)
        {
            _proxyCertPath = proxyCertPath;
            _proxyKeyPath = proxyKeyPath;
            _proxyPort = proxyPort;

            GrowattMqttServer = new GrowattMqttServer(proxyCertPath, proxyKeyPath, proxyPort);            
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
