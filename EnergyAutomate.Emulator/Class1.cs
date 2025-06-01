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
    public class MqttProxy1
    {
        private readonly string _proxyCertPath;
        private readonly string _proxyKeyPath;
        private readonly string _brokerHost;
        private readonly int _proxyPort;
        private readonly int _brokerPort;

        private MqttServer? _mqttServer;
        private readonly ConcurrentDictionary<string, IMqttClient> _remoteClients = new();
        private readonly ConcurrentDictionary<string, Queue<MqttClientSubscribeOptions>> _remoteClientMqttClientSubscribeOptions = new();
        private readonly ConcurrentDictionary<string, Queue<MqttApplicationMessage>> _remoteClientMqttApplicationMessages = new();

        public MqttProxy1(string proxyCertPath, string proxyKeyPath, int proxyPort, string brokerHost, int brokerPort)
        {
            _proxyCertPath = proxyCertPath;
            _proxyKeyPath = proxyKeyPath;
            _proxyPort = proxyPort;
            _brokerHost = brokerHost;
            _brokerPort = brokerPort;
        }

        public async Task StartAsync()
        {
            var proxyCert = X509Certificate2.CreateFromPemFile(_proxyCertPath, _proxyKeyPath);
            var mqttServerOptions = new MqttServerOptionsBuilder()
                .WithEncryptedEndpoint()
                .WithEncryptionCertificate(proxyCert.Export(X509ContentType.Pfx))
                .WithEncryptedEndpointPort(_proxyPort)
                .Build();

            var mqttServerFactory = new MqttServerFactory();
            _mqttServer = mqttServerFactory.CreateMqttServer(mqttServerOptions);

            _mqttServer.InterceptingInboundPacketAsync += _mqttServer_InterceptingPacketAsync;
            _mqttServer.InterceptingOutboundPacketAsync += _mqttServer_InterceptingPacketAsync;

            _mqttServer.ValidatingConnectionAsync += async context =>
            {
                string clientId = context.ClientId;
                _remoteClientMqttClientSubscribeOptions[context.ClientId] = new Queue<MqttClientSubscribeOptions>();
                _remoteClientMqttApplicationMessages[context.ClientId] = new Queue<MqttApplicationMessage>();

                Console.WriteLine($"Validating connection for ClientId: {clientId}");

                var clientOptions = new MqttClientOptionsBuilder()
                    .WithClientId(clientId)
                    .WithTcpServer(_brokerHost, _brokerPort)
                    .WithCredentials(context.UserName, context.Password)
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
                var remoteClient = mqttFactory.CreateMqttClient();
                await remoteClient.ConnectAsync(clientOptions);

                remoteClient.InspectPacketAsync += RemoteClient_InspectPacketAsync;
                remoteClient.ConnectedAsync += RemoteClient_ConnectedAsync;
                remoteClient.DisconnectedAsync += RemoteClient_DisconnectedAsync;
                remoteClient.ApplicationMessageReceivedAsync += RemoteClient_ApplicationMessageReceivedAsync;

                _remoteClients[clientId] = remoteClient;
            };

            _mqttServer.InterceptingPublishAsync += async e =>
            {
                if (!_remoteClients.TryGetValue(e.ClientId, out var remoteClient))
                    return;

                string mappedTopic = MapNoahToCloudTopic(e.ApplicationMessage.Topic);

                var msgBuilder = new MqttApplicationMessageBuilder()
                    .WithTopic(mappedTopic)
                    .WithPayload(e.ApplicationMessage.Payload)
                    .WithQualityOfServiceLevel(e.ApplicationMessage.QualityOfServiceLevel)
                    .WithRetainFlag(e.ApplicationMessage.Retain);

                // Loopschutz (optional, falls du es willst)
                msgBuilder.WithUserProperty("forwarded-for", "device");

                var mappedMessage = msgBuilder.Build();

                Console.WriteLine($"Client {e.ClientId} --> Broker: {mappedTopic}");

                _remoteClientMqttApplicationMessages[e.ClientId].Enqueue(mappedMessage);

                await Task.CompletedTask;
            };

            _mqttServer.InterceptingSubscriptionAsync += async e =>
            {
                if (_remoteClients.TryGetValue(e.ClientId, out var remoteClient))
                {
                    Console.WriteLine($"SUBSCRIBE: ClientId={e.ClientId}, Topic={e.TopicFilter.Topic}");

                    var options = new MqttClientSubscribeOptionsBuilder()
                        .WithTopicFilter(f => f.WithTopic(MapNoahToCloudTopic(e.TopicFilter.Topic))
                                               .WithQualityOfServiceLevel(e.TopicFilter.QualityOfServiceLevel))
                        .Build();

                    _remoteClientMqttClientSubscribeOptions[e.ClientId].Enqueue(options);
                }
                await Task.CompletedTask;
            };

            await _mqttServer.StartAsync();
            Console.WriteLine($"Proxy läuft mit TLS (Port {_proxyPort}). IoT-Device bitte mit mqtts://localhost:{_proxyPort} verbinden.");
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

        // *** Cloud --> NOAH: Topic-Mapping s/33/{id} -> s/{id} ***
        private async Task RemoteClient_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
        {
            var clientId = arg.ClientId;
            var payloadBytes = arg.ApplicationMessage.Payload.ToArray();
            var topic = arg.ApplicationMessage.Topic;

            string mappedTopic = MapCloudToNoahTopic(topic);

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(mappedTopic)
                .WithPayload(payloadBytes)
                .WithUserProperty("forwarded-for", "cloud")
                .WithQualityOfServiceLevel(arg.ApplicationMessage.QualityOfServiceLevel)
                .WithRetainFlag(arg.ApplicationMessage.Retain)
                .Build();

            Console.WriteLine($"Broker --> Client {clientId}: {mappedTopic} | Payload: {Encoding.UTF8.GetString(payloadBytes)}");

            if (_mqttServer != null)
            {
                await _mqttServer.InjectApplicationMessage(new InjectedMqttApplicationMessage(message)
                {
                    SenderClientId = clientId
                });
            }
        }

        private static string MapNoahToCloudTopic(string topic)
        {
            // Wenn Topic s/{id} → s/33/{id}, sonst unverändert
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

        private static string MapCloudToNoahTopic(string topic)
        {
            // Wenn Topic s/33/{id} → s/{id}, sonst unverändert
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

        private Task RemoteClient_DisconnectedAsync(MqttClientDisconnectedEventArgs arg)
        {
            var clientId = arg.ConnectResult.AssignedClientIdentifier;
            Console.WriteLine($"[Proxy] Remote client disconnected for ClientId: {clientId}");
            return Task.CompletedTask;
        }

        private async Task RemoteClient_ConnectedAsync(MqttClientConnectedEventArgs arg)
        {
            var clientId = arg.ConnectResult.AssignedClientIdentifier;
            Console.WriteLine($"[Proxy] Remote client connected for ClientId: {clientId}");

            Console.WriteLine($"[Proxy] Subscribe for ClientId: {clientId}");

            while (_remoteClientMqttClientSubscribeOptions[clientId].Count > 0)
            {
                var option = _remoteClientMqttClientSubscribeOptions[clientId].Dequeue();
                if (option != null)
                {
                    Console.WriteLine($"[Proxy] Forwarding subscrition to remote client {clientId}: {option.TopicFilters}");
                    await _remoteClients[clientId].SubscribeAsync(option);
                }
            }

            while (_remoteClientMqttApplicationMessages[clientId].Count > 0)
            {
                var message = _remoteClientMqttApplicationMessages[clientId].Dequeue();
                if (message != null)
                {
                    Console.WriteLine($"[Proxy] Forwarding message to remote client {clientId}: {message.Topic}");
                    await _remoteClients[clientId].PublishAsync(message);
                }
            }

        }

        private async Task _mqttServer_InterceptingPacketAsync(MQTTnet.Server.InterceptingPacketEventArgs arg)
        {
            // Logging wie gehabt ...
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
            await File.AppendAllTextAsync(logFile, System.Text.Json.JsonSerializer.Serialize(logObj) + Environment.NewLine);
        }

        public async Task StopAsync()
        {
            foreach (var remoteClient in _remoteClients.Values)
                await remoteClient.DisconnectAsync();
            if (_mqttServer != null)
                await _mqttServer.StopAsync();
        }
    }
}