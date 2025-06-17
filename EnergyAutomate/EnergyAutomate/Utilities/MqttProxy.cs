using MQTTnet;
using MQTTnet.Server;
using System.Security.Cryptography.X509Certificates;

namespace EnergyAutomate.Utilities
{
    public class MqttProxy
    {
        private readonly string _proxyCertPath;
        private readonly string _proxyKeyPath;
        private readonly string _brokerHost;
        private readonly int _brokerPort;

        private MqttServer? _mqttServer;

        public MqttProxy(string proxyCertPath, string proxyKeyPath, string brokerHost, int brokerPort)
        {
            _proxyCertPath = proxyCertPath;
            _proxyKeyPath = proxyKeyPath;
            _brokerHost = brokerHost;
            _brokerPort = brokerPort;
        }

        public async Task StartAsync()
        {
            // Proxy TLS-Zertifikat laden
            var proxyCert = X509Certificate2.CreateFromPemFile(_proxyCertPath, _proxyKeyPath);
            var mqttServerOptions = new MqttServerOptionsBuilder()
                .WithEncryptedEndpoint()
                .WithEncryptionCertificate(proxyCert.Export(X509ContentType.Pfx))
                .WithEncryptedEndpointPort(7006)
                .Build();

            var mqttServerFactory = new MqttServerFactory();
            _mqttServer = mqttServerFactory.CreateMqttServer(mqttServerOptions);

            await _mqttServer.StartAsync();
            Console.WriteLine("Proxy läuft mit TLS (Port 8883). IoT-Device bitte mit mqtts://localhost:8883 verbinden.");
        }

        public async Task StopAsync()
        {
            if (_mqttServer != null)
                await _mqttServer.StopAsync();
        }
    }
}