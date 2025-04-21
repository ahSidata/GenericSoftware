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
        private IMqttClient? _remoteClient;

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

            // Event-Handler für neue Verbindungen
            _mqttServer.ClientConnectedAsync += async e =>
            {
                Console.WriteLine($"Neuer Client verbunden: {e.ClientId}");

                // Erstelle eine neue Broker-Session für diesen Client
                var clientOptions = new MqttClientOptionsBuilder()
                    .WithTcpServer(_brokerHost, _brokerPort)
                    .WithTlsOptions(new MqttClientTlsOptions
                    {
                        UseTls = true,
                        AllowUntrustedCertificates = true,
                        IgnoreCertificateChainErrors = true,
                        IgnoreCertificateRevocationErrors = true,
                        CertificateValidationHandler = context => true
                    })
                    .Build();

                var mqttFactory = new MqttClientFactory();
                var brokerClient = mqttFactory.CreateMqttClient();

                await brokerClient.ConnectAsync(clientOptions);

                // Nachrichten vom Client an den Broker weiterleiten
                _mqttServer.InterceptingPublishAsync += async publishEvent =>
                {
                    if (publishEvent.ClientId == e.ClientId)
                    {
                        Console.WriteLine($"Client {e.ClientId} --> Broker: {publishEvent.ApplicationMessage.Topic}");
                        await brokerClient.PublishAsync(publishEvent.ApplicationMessage);
                    }
                };

                // Nachrichten vom Broker an den Client weiterleiten
                brokerClient.ApplicationMessageReceivedAsync += async brokerMessage =>
                {
                    Console.WriteLine($"Broker --> Client {e.ClientId}: {brokerMessage.ApplicationMessage.Topic}");
                    await _mqttServer.InjectApplicationMessage(new InjectedMqttApplicationMessage(brokerMessage.ApplicationMessage)
                    {
                        SenderClientId = e.ClientId
                    });
                };
            };

            await _mqttServer.StartAsync();
            Console.WriteLine("Proxy läuft mit TLS (Port 8883). IoT-Device bitte mit mqtts://localhost:8883 verbinden.");
        }

        public async Task StopAsync()
        {
            if (_remoteClient != null)
                await _remoteClient.DisconnectAsync();
            if (_mqttServer != null)
                await _mqttServer.StopAsync();
        }
    }
}