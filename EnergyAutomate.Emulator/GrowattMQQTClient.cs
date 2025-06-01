using MQTTnet;
using MQTTnet.Server;
using System.Buffers;
using System.Text;
using System.Text.Json;

namespace EnergyAutomate.Emulator
{
    public class GrowattMQQTClient
    {
        private readonly string _brokerHost;
        private readonly int _brokerPort;

        public IMqttClient MqttClient { get; private set; }

        public string ClientId { get; set; }

        public bool IsConnected { get; set; }

        public MqttClientOptions ClientOptions { get; private set; }

        // Delegate for ApplicationMessageReceived
        public delegate Task ApplicationMessageReceivedDelegate(MqttApplicationMessageReceivedEventArgs arg);

        // Event/Delegate instance for ApplicationMessageReceived
        public event ApplicationMessageReceivedDelegate? ApplicationMessageReceived;

        public GrowattMQQTClient(string brokerHost, int brokerPort, ClientConnectedEventArgs arg)
        {
            _brokerHost = brokerHost;
            _brokerPort = brokerPort;

            ClientId = arg.ClientId;

            Console.WriteLine($"Validating connection for ClientId: {ClientId}");

            ClientOptions = new MqttClientOptionsBuilder()
                .WithClientId(ClientId)
                .WithTcpServer(_brokerHost, _brokerPort)
                .WithCredentials(arg.UserName, arg.Password)
                .WithProtocolVersion(arg.ProtocolVersion)
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

            MqttClient.InspectPacketAsync += RemoteClient_InspectPacketAsync;
            MqttClient.ConnectedAsync += RemoteClient_ConnectedAsync;
            MqttClient.DisconnectedAsync += RemoteClient_DisconnectedAsync;

            MqttClient.ApplicationMessageReceivedAsync += RemoteClient_ApplicationMessageReceivedAsync;
        }

        public async Task ConnectAsync()
        {
            await MqttClient.ConnectAsync(ClientOptions);
        }

        private Task RemoteClient_DisconnectedAsync(MqttClientDisconnectedEventArgs arg)
        {
            IsConnected = false;
            Console.WriteLine($"[Proxy] Remote client disconnected for ClientId: {ClientId}");
            return Task.CompletedTask;
        }

        private Task RemoteClient_ConnectedAsync(MqttClientConnectedEventArgs arg)
        {
            IsConnected = true;
            Console.WriteLine($"[Proxy] Remote client connected for ClientId: {ClientId}");
            return Task.CompletedTask;
        }

        private async Task RemoteClient_InspectPacketAsync(MQTTnet.Diagnostics.PacketInspection.InspectMqttPacketEventArgs arg)
        {
            var timestamp = DateTime.UtcNow.ToString("o");
            var direction = arg.Direction.ToString();

            // Payload als Hex und als Base64 (wahlweise)
            string hex = BitConverter.ToString(arg.Buffer).Replace("-", " ");
            string base64 = Convert.ToBase64String(arg.Buffer);

            var logObj = new
            {
                Timestamp = timestamp,
                Direction = direction,
                BufferHex = hex,
                BufferBase64 = base64,
                BufferLength = arg.Buffer.Length
            };

            var logDir = Path.Combine("Logs", "mqtt_packets");
            Directory.CreateDirectory(logDir);
            var logFile = Path.Combine(logDir, $"remote_{DateTime.UtcNow:yyyyMMdd}.json");
            await File.AppendAllTextAsync(logFile, JsonSerializer.Serialize(logObj) + Environment.NewLine);
        }

        private static string MapCloudToNoahTopic(string topic)
        {
            if (topic != null && topic.StartsWith("s/33/"))
            {
                var parts = topic.Split('/');
                if (parts.Length == 3)
                {
                    return $"s/{parts[2]}";
                }
            }
            return topic ?? "";
        }

        private async Task RemoteClient_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
        {
            var clientId = arg.ClientId;

            var mappedTopic = MapCloudToNoahTopic(arg.ApplicationMessage.Topic);
            arg.ApplicationMessage.Topic = mappedTopic;
            arg.ApplicationMessage.Retain = false;

            Console.WriteLine($"Broker --> Client {clientId}");
            Console.WriteLine($"Application Message Topic: {arg.ApplicationMessage.Topic}");
            Console.WriteLine($"Application Message Payload: {Encoding.UTF8.GetString(arg.ApplicationMessage.Payload.ToArray())}");

            //Invoke the delegate to handle the message
            if (ApplicationMessageReceived != null)
            {
                await ApplicationMessageReceived(arg);
            }
        }
    }
}
