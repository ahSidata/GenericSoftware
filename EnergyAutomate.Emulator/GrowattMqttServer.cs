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
    public class GrowattMqttServer
    {
        private readonly string _proxyCertPath;
        private readonly string _proxyKeyPath;
        private readonly int _proxyPort;

        private MqttServer MqttServer { get; set; }

        private readonly ConcurrentDictionary<string, GrowattMQQTClient> _remoteClients = new();

        public GrowattMqttServer(string proxyCertPath, string proxyKeyPath, int proxyPort)
        {
            _proxyCertPath = proxyCertPath;
            _proxyKeyPath = proxyKeyPath;
            _proxyPort = proxyPort;

            var proxyCert = X509Certificate2.CreateFromPemFile(_proxyCertPath, _proxyKeyPath);
            var mqttServerOptions = new MqttServerOptionsBuilder()
                .WithEncryptedEndpoint()
                .WithEncryptionCertificate(proxyCert.Export(X509ContentType.Pfx))
                .WithEncryptedEndpointPort(_proxyPort)
                .Build();

            var mqttServerFactory = new MqttServerFactory();
            MqttServer = mqttServerFactory.CreateMqttServer(mqttServerOptions);

            MqttServer.InterceptingInboundPacketAsync += MqttServer_InterceptingPacketAsync;
            MqttServer.InterceptingOutboundPacketAsync += MqttServer_InterceptingPacketAsync;

            MqttServer.InterceptingPublishAsync += MqttServer_InterceptingPublishAsync;
            MqttServer.ClientConnectedAsync += MqttServer_ClientConnectedAsync;
            MqttServer.ClientSubscribedTopicAsync += MqttServer_ClientSubscribedTopicAsync;
        }

        public async Task StartAsync()
        {
            Console.WriteLine($"Proxy läuft mit TLS (Port {_proxyPort}). IoT-Device bitte mit mqtts://localhost:{_proxyPort} verbinden.");
            if (MqttServer != null)
            {
                await MqttServer.StartAsync();
            }
        }

        private async Task MqttServer_ClientConnectedAsync(ClientConnectedEventArgs arg)
        {
            var growattMqttClient = new GrowattMQQTClient("mqtt.growatt.com", 7006, arg);

            // Fix: Use an event handler method instead of directly assigning a lambda to the event
            growattMqttClient.ApplicationMessageReceived += async (arg) =>
            {
                await MqttServer.InjectApplicationMessage(new InjectedMqttApplicationMessage(arg.ApplicationMessage)
                {
                    SenderClientId = arg.ClientId,                   
                });

                var cancelatioonToken = new CancellationTokenSource(TimeSpan.FromSeconds(10));

                await arg.AcknowledgeAsync(cancelatioonToken.Token);
            };

            await growattMqttClient.ConnectAsync();

            while (!growattMqttClient.IsConnected)
            {
                Console.WriteLine($"[Proxy] Warten auf Verbindung des Remote-Clients {arg.ClientId}...");
                await Task.Delay(1000);
            }

            _remoteClients.TryAdd(arg.ClientId, growattMqttClient);
        }

        private async Task MqttServer_InterceptingPublishAsync(InterceptingPublishEventArgs arg)
        {
            GrowattMQQTClient? remoteClient;

            while (!_remoteClients.TryGetValue(arg.ClientId, out remoteClient))
            {
                await Task.Delay(1000);
            }

            var mappedTopic = MapNoahToCloudTopic(arg.ApplicationMessage.Topic);
            var msgBuilder = new MqttApplicationMessageBuilder()
                .WithTopic(mappedTopic)
                .WithPayload(arg.ApplicationMessage.Payload)
                .WithQualityOfServiceLevel(arg.ApplicationMessage.QualityOfServiceLevel)
                .WithRetainFlag(arg.ApplicationMessage.Retain);

            var mappedMessage = msgBuilder.Build();

            Console.WriteLine($"Client {arg.ClientId} --> Broker: {mappedTopic}");

            await remoteClient.MqttClient.PublishAsync(mappedMessage);

            await Task.CompletedTask;
        }

        private async Task MqttServer_ClientSubscribedTopicAsync(ClientSubscribedTopicEventArgs arg)
        {
            GrowattMQQTClient? remoteClient;

            while (!_remoteClients.TryGetValue(arg.ClientId, out remoteClient))
            {
                await Task.Delay(1000);
            }

            var clientId = arg.ClientId;

            Console.WriteLine($"[Proxy] Subscribing at cloud for ClientId={clientId}: Topic={arg.TopicFilter}");

            var mappedTopic = MapNoahToCloudTopic(arg.TopicFilter.Topic);

            var options = new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter(f => f.WithTopic(mappedTopic)
                                       .WithQualityOfServiceLevel(arg.TopicFilter.QualityOfServiceLevel))
                .Build();

            await remoteClient.MqttClient.SubscribeAsync(options);

            await Task.CompletedTask;
        }

        private async Task MqttServer_InterceptingPacketAsync(MQTTnet.Server.InterceptingPacketEventArgs arg)
        {
            var packetType = arg.Packet?.GetType().Name ?? "null";
            var clientId = arg.ClientId ?? "";
            var endpoint = arg.Endpoint ?? "";
            var timestamp = DateTime.UtcNow.ToString("o");

            string topic = null, payload = null, extra = null;
            if (arg.Packet is MQTTnet.Packets.MqttPublishPacket pub)
            {
                topic = pub.Topic;
                try
                {
                    if (!pub.Payload.IsEmpty)
                    {
                        payload = Encoding.UTF8.GetString(pub.Payload.ToArray());
                    }
                    else
                    {
                        payload = null;
                    }
                }
                catch
                {
                    payload = "<Payload not decodable>";
                }
                extra = $"QoS: {pub.QualityOfServiceLevel}, Retain: {pub.Retain}";
            }
            else if (arg.Packet is MQTTnet.Packets.MqttSubscribePacket sub)
            {
                extra = "Topics: " + string.Join(", ", sub.TopicFilters.Select(tf => tf.Topic));
            }
            else if (arg.Packet is MQTTnet.Packets.MqttConnectPacket connect)
            {
                extra = $"Username: {connect.Username}, CleanSession: {connect.CleanSession}";
            }
            else
            {
                extra = arg.Packet?.ToString();
            }

            var logObj = new
            {
                Timestamp = timestamp,
                ClientId = clientId,
                Endpoint = endpoint,
                PacketType = packetType,
                Topic = topic,
                Payload = payload,
                Extra = extra
            };

            var logDir = Path.Combine("Logs", "mqtt_packets");
            Directory.CreateDirectory(logDir);
            var logFile = Path.Combine(logDir, $"server_{DateTime.UtcNow:yyyyMMdd}.json");
            await File.AppendAllTextAsync(logFile, JsonSerializer.Serialize(logObj) + Environment.NewLine);
        }

        private static string MapNoahToCloudTopic(string topic)
        {
            if (topic != null && topic.StartsWith("s/") && !topic.StartsWith("s/33/"))
            {
                var parts = topic.Split('/');
                if (parts.Length == 2)
                {
                    return $"s/33/{parts[1]}";
                }
            }
            return topic ?? "";
        }
    }
}
