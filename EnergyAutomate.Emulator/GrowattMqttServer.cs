using MQTTnet;
using MQTTnet.Server;
using System.Collections.Concurrent;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using MQTTnet.Packets;
using System.Text.Json;
using System.Buffers;
using MQTTnet.Diagnostics.Logger;
using System.Net.Sockets;

namespace EnergyAutomate.Emulator
{
    public class GrowattMqttServer
    {
        private readonly string _proxyCertPath;
        private readonly string _proxyKeyPath;
        private readonly int _proxyPort;

        private MqttServer MqttServer { get; set; }

        public GrowattMqttServer(string proxyCertPath, string proxyKeyPath, int proxyPort)
        {
            _proxyCertPath = proxyCertPath;
            _proxyKeyPath = proxyKeyPath;
            _proxyPort = proxyPort;

            var proxyCert = X509Certificate2.CreateFromPemFile(_proxyCertPath, _proxyKeyPath);
            var mqttServerOptions = new MqttServerOptionsBuilder()                
                .WithPersistentSessions(true)                 
                .WithEncryptedEndpoint()
                .WithEncryptionCertificate(proxyCert.Export(X509ContentType.Pfx))
                .WithEncryptedEndpointPort(_proxyPort)
                .WithMaxPendingMessagesPerClient(1000)
                .Build();

            //mqttServerOptions.TlsEndpointOptions.NoDelay = false;
            mqttServerOptions.TlsEndpointOptions.AllowPacketFragmentation = false;
            //mqttServerOptions.TlsEndpointOptions.LingerState = new LingerOption(false, 0) ;

            var mqttServerFactory = new MqttServerFactory();
            MqttServer = mqttServerFactory.CreateMqttServer(mqttServerOptions);
        }

        public async Task StartAsync()
        {
            Console.WriteLine($"Proxy läuft mit TLS (Port {_proxyPort}). IoT-Device bitte mit mqtts://localhost:{_proxyPort} verbinden.");
            if (MqttServer != null)
            {
                await MqttServer.StartAsync();
            }
        }
    }
}
