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

        private bool Subscribed { get; set; } = false; // Default to true for testing purposes

        public GrowattMqttClient(string brokerHost, int brokerPort, string clientId, string username, string password, MqttNetEventLogger mqttNetEventLogger)
        {
            MqttNetEventLogger = mqttNetEventLogger;

            _brokerHost = brokerHost;
            _brokerPort = brokerPort;

            ClientId = clientId;

            ClientOptions = new MqttClientOptionsBuilder()
                .WithClientId(ClientId)
                .WithCleanSession(false)                
                .WithCredentials(username, password)
                .WithProtocolVersion( MQTTnet.Formatter.MqttProtocolVersion.V311)                
                .WithTcpServer(_brokerHost, _brokerPort)
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
            MqttClient = mqttFactory.CreateMqttClient();
            MqttClient.InspectPacketAsync += MqttClient_InspectPacketAsync;
            MqttClient.ConnectedAsync += RemoteClient_ConnectedAsync;
            MqttClient.DisconnectedAsync += RemoteClient_DisconnectedAsync;

            MqttClient.ApplicationMessageReceivedAsync += RemoteClient_ApplicationMessageReceivedAsync;
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

        // Delegate for ApplicationMessageReceived
        public delegate Task ApplicationMessageReceivedDelegate(MqttApplicationMessage mqttApplicationMessage );

        // Event/Delegate instance for ApplicationMessageReceived
        public event ApplicationMessageReceivedDelegate? ApplicationMessageReceived;

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

            if(!Subscribed)
            {
                try
                {
                    await MqttClient.SubscribeAsync($"+/{ClientId}");
                }
                catch (Exception)
                {
                    _ = Task.Run(async () =>
                    {
                        Task.Delay(1000).Wait();
                        await ConnectAsync();
                    });

                }

                Subscribed = true;
            }
        }

        private async Task RemoteClient_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
        {            
            Console.WriteLine($"Broker --> Client {arg.ClientId}");
            Console.WriteLine($"Cloud Application Message Topic: {SubscribedTopics.First().Topic}");

            var msgBuilder = new MqttApplicationMessageBuilder()
                .WithTopic(SubscribedTopics.First().Topic)
                .WithResponseTopic(arg.ApplicationMessage.ResponseTopic)
                .WithContentType(arg.ApplicationMessage.ContentType)                
                .WithPayload(arg.ApplicationMessage.Payload)
                .WithPayloadFormatIndicator(arg.ApplicationMessage.PayloadFormatIndicator)
                .WithQualityOfServiceLevel(arg.ApplicationMessage.QualityOfServiceLevel)
                .WithRetainFlag(arg.ApplicationMessage.Retain);

            var mappedMessage = msgBuilder.Build();

            //Invoke the delegate to handle the message
            if (ApplicationMessageReceived != null)
            {
                await ApplicationMessageReceived(mappedMessage);
            }
           
            arg.ReasonCode = MqttApplicationMessageReceivedReasonCode.Success;
            var cancelationTokenSource = new System.Threading.CancellationTokenSource();

            await arg.AcknowledgeAsync(cancelationTokenSource.Token);
        }

    }
}
