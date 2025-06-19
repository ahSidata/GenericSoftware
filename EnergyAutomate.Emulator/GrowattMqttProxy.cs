using MQTTnet;
using MQTTnet.Diagnostics.Logger;
using MQTTnet.Packets;
using MQTTnet.Server;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace EnergyAutomate.Emulator
{
    public class GrowattMqttProxy
    {
        #region Fields

        private readonly string _proxyCertPath;
        private readonly string _proxyKeyPath;
        private readonly int _proxyPort;

        #endregion Fields

        #region Public Constructors

        public GrowattMqttProxy(string proxyCertPath, string proxyKeyPath, string brokerHost, int brokerPort, string growattHost, int growattPort)
        {
            ClientId = "0PVPG5ZR23CT00V4";

            _proxyCertPath = proxyCertPath;
            _proxyKeyPath = proxyKeyPath;

            BrokerClientOptions = new MqttClientOptionsBuilder()
                .WithClientId("Proxy")
                .WithCleanSession(false)
                .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V500)
                .WithTcpServer(brokerHost, brokerPort)
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

            BrokerMqttClient.ConnectedAsync += BrokerMqttClient_ConnectedAsync;
            BrokerMqttClient.DisconnectedAsync += BrokerMqttClient_DisconnectedAsync;
            BrokerMqttClient.ApplicationMessageReceivedAsync += BrokerMqttClient_ApplicationMessageReceivedAsync;

            GrowattClientOptions = new MqttClientOptionsBuilder()
                .WithClientId(ClientId)
                .WithTcpServer(growattHost, growattPort)
                .WithKeepAlivePeriod(TimeSpan.FromSeconds(420))
                .WithTlsOptions(new MqttClientTlsOptions
                {
                    UseTls = true,
                    AllowUntrustedCertificates = true,
                    IgnoreCertificateChainErrors = true,
                    IgnoreCertificateRevocationErrors = true,
                    CertificateValidationHandler = context => true
                })
            .Build();

            GrowattMqttClient = mqttFactory.CreateMqttClient();
            GrowattMqttClient.InspectPacketAsync += GrowattMqttClient_InspectPacketAsync;
            GrowattMqttClient.ConnectedAsync += GrowattMqttClient_ConnectedAsync;
            GrowattMqttClient.DisconnectedAsync += GrowattMqttClient_DisconnectedAsync;
            GrowattMqttClient.ApplicationMessageReceivedAsync += GrowattMqttClient_ApplicationMessageReceivedAsync;
        }

        private async Task BrokerMqttClient_ConnectedAsync(MqttClientConnectedEventArgs arg)
        {
            await BrokerMqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic($"c/33/{ClientId}").Build());
        }

        #endregion Public Constructors

        #region Properties

        public MqttClientOptions BrokerClientOptions { get; private set; }

        public IMqttClient BrokerMqttClient { get; set; }

        public string ClientId { get; set; }

        public MqttClientOptions GrowattClientOptions { get; private set; }

        public IMqttClient GrowattMqttClient { get; private set; }

        private GrowattMqttServer? GrowattMqttServer { get; set; }

        #endregion Properties

        #region Public Methods

        public async Task StartAsync()
        {
            Console.WriteLine($"Starting broker connection for ClientId: {ClientId}");
            await BrokerMqttClient.ConnectAsync(BrokerClientOptions);

            Console.WriteLine($"Starting growatt connection for ClientId: {ClientId}");
            await GrowattMqttClient.ConnectAsync(GrowattClientOptions);
        }

        public async Task StopAsync()
        {
            await Task.CompletedTask;
        }

        #endregion Public Methods

        #region Private Methods

        private async Task BrokerMqttClient_DisconnectedAsync(MqttClientDisconnectedEventArgs arg)
        {
            while (!BrokerMqttClient.IsConnected)
            {
                await Task.Delay(1000);

                await BrokerMqttClient.ConnectAsync(BrokerClientOptions);
            }
        }

        private async Task GrowattMqttClient_InspectPacketAsync(MQTTnet.Diagnostics.PacketInspection.InspectMqttPacketEventArgs arg)
        {
            // Explicitly convert the buffer to a Span<byte> to resolve ambiguity
            var buffer = new byte[arg.Buffer.Length];
            arg.Buffer.AsSpan().CopyTo(buffer.AsSpan());
            Console.WriteLine($"GrowattMqttClient Packet (Dir: {arg.Direction}), Raw Buffer (Dir: {arg.Direction}): {BitConverter.ToString(buffer).Replace("-", "")}");

            await Task.CompletedTask;
        }

        private Task GrowattMqttClient_ConnectedAsync(MqttClientConnectedEventArgs arg)
        {
            Console.WriteLine($"[Proxy] Remote client connected for ClientId: {ClientId}");

            _ = Task.Run(async () => {
                await Task.Delay(1000);
                await GrowattMqttClient.SubscribeAsync($"s/33/{ClientId}", MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);
                await GrowattMqttClient.SubscribeAsync($"s/{ClientId}", MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);
                await GrowattMqttClient.SubscribeAsync($"#/{ClientId}", MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);
            });

            return Task.CompletedTask;
        }

        private Task GrowattMqttClient_DisconnectedAsync(MqttClientDisconnectedEventArgs arg)
        {
            Console.WriteLine($"[Proxy] Growatt client disconnected for ClientId: {ClientId}");
            return Task.CompletedTask;
        }

        private async Task BrokerMqttClient_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
        {
            await Task.Delay(1000);

            Console.WriteLine($"Broker --> Client {ClientId}, Topic: {arg.ApplicationMessage.Topic}");

            var msgBuilder = new MqttApplicationMessageBuilder()
                .WithTopic(arg.ApplicationMessage.Topic)
                .WithPayload(arg.ApplicationMessage.Payload)
                .WithRetainFlag(arg.ApplicationMessage.Retain)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);

            var mappedMessage = msgBuilder.Build();

            await GrowattMqttClient.PublishAsync(mappedMessage);
        }

        private async Task GrowattMqttClient_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
        {
            var topic = $"s/33/{arg.ClientId}";

            Console.WriteLine($"Client --> Broker {arg.ClientId}, Topic:{topic}");

            var msgBuilder = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(arg.ApplicationMessage.Payload)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .WithRetainFlag(arg.ApplicationMessage.Retain);

            var mappedMessage = msgBuilder.Build();

            if (BrokerMqttClient is not null)
                await BrokerMqttClient.PublishAsync(mappedMessage);
        }

        #endregion Private Methods
    }
}
