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

        private MqttNetEventLogger MqttNetEventLogger { get; set; }

        private GrowattMqttServer? GrowattMqttServer { get; set; }

        public IMqttClient BrokerMqttClient { get; private set; }

        public MqttClientOptions BrokerClientOptions { get; private set; }

        private readonly ConcurrentDictionary<string, GrowattMqttClient> _remoteClients = new();

        public GrowattMqttProxy(string proxyCertPath, string proxyKeyPath, string proxyHost, int proxyPort, MqttNetEventLogger mqttNetEventLogger)
        {
            _proxyCertPath = proxyCertPath;
            _proxyKeyPath = proxyKeyPath;
            _proxyPort = proxyPort;

            MqttNetEventLogger = mqttNetEventLogger;

            //GrowattMqttServer = new GrowattMqttServer(proxyCertPath, proxyKeyPath, proxyPort, mqttNetEventLogger);
            //

            BrokerClientOptions = new MqttClientOptionsBuilder()
                .WithClientId("Proxy")
                .WithCleanSession(false)
                .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V500)
                .WithTcpServer(proxyHost, proxyPort)
                .WithKeepAlivePeriod(TimeSpan.FromSeconds(60))
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
            BrokerMqttClient = mqttFactory.CreateMqttClient();
            BrokerMqttClient.DisconnectedAsync += BrokerMqttClient_DisconnectedAsync;

            BrokerMqttClient.ApplicationMessageReceivedAsync += async (arg) =>
            {
                // Handle incoming messages from the broker
                var clientId = arg.ApplicationMessage.Topic.Split('/')[2]; // Assuming topic format is "s/33/{clientId}"

                Console.WriteLine($"Broker --> Client {clientId}, Topic: {arg.ApplicationMessage.Topic}");
                
                if (_remoteClients.TryGetValue(clientId, out var growattMqttClient))
                {
                    var msgBuilder = new MqttApplicationMessageBuilder()
                        .WithTopic(arg.ApplicationMessage.Topic)
                        .WithPayload(arg.ApplicationMessage.Payload)
                        .WithRetainFlag(arg.ApplicationMessage.Retain)
                        .WithQualityOfServiceLevel( MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);

                    var mappedMessage = msgBuilder.Build();

                    await growattMqttClient.MqttClient.PublishAsync(mappedMessage);

                    arg.AutoAcknowledge = true;
                }
                else
                {
                    Console.WriteLine($"[Proxy] Remote client {clientId} not found for message: {arg.ApplicationMessage.Topic}");
                }
            };
        }

        private async Task BrokerMqttClient_DisconnectedAsync(MqttClientDisconnectedEventArgs arg)
        {
            while (!BrokerMqttClient.IsConnected)
            {
                await Task.Delay(1000);

                await ConnectBroker("0PVPG5ZR23CT00V4");
            }
        }

        public async Task StartAsync()
        {
            //await GrowattMqttServer.StartAsync();

            await ConnectBroker("0PVPG5ZR23CT00V4");

            await AddClient("0PVPG5ZR23CT00V4");
        }

        private async Task ConnectBroker(string clientId)
        {
            await BrokerMqttClient.ConnectAsync(BrokerClientOptions);
            await BrokerMqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic($"c/33/{clientId}").Build());
        }

        public async Task StopAsync()
        {
            await Task.CompletedTask;
        }

        private async Task AddClient(string clientId)
        {
            var growattMqttClient = new GrowattMqttClient("mqtt.growatt.com", 7006, clientId, clientId, "Growatt", MqttNetEventLogger);
            growattMqttClient.BrokerMqttClient = BrokerMqttClient;

            await growattMqttClient.ConnectAsync();            

            while (!growattMqttClient.IsConnected)
            {
                Console.WriteLine($"[Proxy] Warten auf Verbindung des Remote-Clients {clientId}...");
                await Task.Delay(1000);
            }

            _remoteClients.TryAdd(clientId, growattMqttClient);
        }
    }
}
