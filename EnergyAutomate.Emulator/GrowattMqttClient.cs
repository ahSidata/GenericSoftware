using MQTTnet;
using MQTTnet.Diagnostics.Logger;
using MQTTnet.Packets;
using MQTTnet.Server;
using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;

namespace EnergyAutomate.Emulator
{
    public class GrowattMqttClient
    {
        private readonly string _brokerHost;
        private readonly int _brokerPort;

        private int count = 0;

        public IMqttClient? BrokerMqttClient { get; set; }

        public GrowattMqttClient(string brokerHost, int brokerPort, string clientId, string username, string password, MqttNetEventLogger mqttNetEventLogger)
        {
            MqttNetEventLogger = mqttNetEventLogger;

            _brokerHost = brokerHost;
            _brokerPort = brokerPort;

            ClientId = clientId;

            ClientOptions = new MqttClientOptionsBuilder()
                .WithClientId(ClientId)
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
            MqttClient = mqttFactory.CreateMqttClient();
            MqttClient.InspectPacketAsync += MqttClient_InspectPacketAsync;
            MqttClient.ConnectedAsync += RemoteClient_ConnectedAsync;
            MqttClient.DisconnectedAsync += RemoteClient_DisconnectedAsync;
            MqttClient.ApplicationMessageReceivedAsync += MqttClient_ApplicationMessageReceivedAsync;
        }

        private async Task MqttClient_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
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

            arg.AutoAcknowledge = true;
        }

        private Task MqttClient_InspectPacketAsync(MQTTnet.Diagnostics.PacketInspection.InspectMqttPacketEventArgs arg)
        {
            Console.WriteLine($"Packet (Dir: {arg.Direction})");
            // Explicitly convert the buffer to a Span<byte> to resolve ambiguity
            var buffer = new byte[arg.Buffer.Length];
            arg.Buffer.AsSpan().CopyTo(buffer.AsSpan());
            Console.WriteLine($" Raw Buffer (Dir: {arg.Direction}): {BitConverter.ToString(buffer).Replace("-", "")}");

            return Task.CompletedTask;
        }

        public IMqttClient MqttClient { get; private set; }

        private MqttNetEventLogger MqttNetEventLogger { get; set; }

        public string ClientId { get; set; }

        public bool IsConnected { get; set; }

        public MqttClientOptions ClientOptions { get; private set; }

        public ObservableCollection<MqttTopicFilter> SubscribedTopics { get; } = [];

        public async Task ConnectAsync()
        {
            Console.WriteLine($"Starting connection for ClientId: {ClientId}");
            await MqttClient.ConnectAsync(ClientOptions);
        }

        private Task RemoteClient_DisconnectedAsync(MqttClientDisconnectedEventArgs arg)
        {
            IsConnected = false;
            Console.WriteLine($"[Proxy] Remote client disconnected for ClientId: {ClientId}");
            return Task.CompletedTask;
        }

        private async Task RemoteClient_ConnectedAsync(MqttClientConnectedEventArgs arg)
        {
            IsConnected = true;
            Console.WriteLine($"[Proxy] Remote client connected for ClientId: {ClientId}");
            await MqttClient.SubscribeAsync($"s/33/{ClientId}", MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);
        }

    }
}
