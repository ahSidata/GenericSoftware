using MQTTnet;
using MQTTnet.Server;
using System.Collections.Concurrent;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Buffers;
using MQTTnet.Packets;
using System.Text.Json;

namespace EnergyAutomate.Emulator
{
    public class MqttProxy
    {
        private readonly string _proxyCertPath;
        private readonly string _proxyKeyPath;
        private readonly string _brokerHost;
        private readonly int _proxyPort;
        private readonly int _brokerPort;

        private MqttServer? _mqttServer;

        private readonly ConcurrentDictionary<string, IMqttClient> _remoteClients = new();

        public MqttProxy(string proxyCertPath, string proxyKeyPath, int proxyPort, string brokerHost, int brokerPort)
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

            _mqttServer.InterceptingInboundPacketAsync += _mqttServer_InterceptingInboundPacketAsync;

            // Initialisiere Remote-Client für jeden NOAH-Client beim Connect
            _mqttServer.ValidatingConnectionAsync += async context =>
            {
                string clientId = context.ClientId;
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

                remoteClient.ConnectedAsync += RemoteClient_ConnectedAsync;
                remoteClient.DisconnectedAsync += RemoteClient_DisconnectedAsync;
                remoteClient.ApplicationMessageReceivedAsync += RemoteClient_ApplicationMessageReceivedAsync;

                _remoteClients[clientId] = remoteClient;
            };

            _mqttServer.InterceptingPublishAsync += async e =>
            {
                if (!_remoteClients.TryGetValue(e.ClientId, out var remoteClient))
                    return;

                Console.WriteLine($"Client {e.ClientId} --> Broker: {e.ApplicationMessage.Topic}");
                await remoteClient.PublishAsync(e.ApplicationMessage);
            };

            _mqttServer.InterceptingSubscriptionAsync += async e =>
            {
                if (_remoteClients.TryGetValue(e.ClientId, out var remoteClient))
                {
                    Console.WriteLine($"SUBSCRIBE: ClientId={e.ClientId}, Topic={e.TopicFilter.Topic}");
                    await remoteClient.SubscribeAsync(e.TopicFilter);
                }
            };

            _mqttServer.InterceptingUnsubscriptionAsync += async e =>
            {
                if (_remoteClients.TryGetValue(e.ClientId, out var remoteClient))
                {
                    Console.WriteLine($"UNSUBSCRIBE: ClientId={e.ClientId}, Topic={e.Topic}");
                    await remoteClient.UnsubscribeAsync(e.Topic);
                }
            };

            await _mqttServer.StartAsync();
            Console.WriteLine($"Proxy läuft mit TLS (Port {_proxyPort}). IoT-Device bitte mit mqtts://localhost:{_proxyPort} verbinden.");
        }

        private async Task RemoteClient_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
        {
            var clientId = arg.ClientId;
            var payloadBytes = arg.ApplicationMessage.Payload.ToArray();
            var topic = arg.ApplicationMessage.Topic;

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payloadBytes)
                .WithUserProperty("forwarded-for", "cloud")
                .Build();

            Console.WriteLine($"Broker --> Client {clientId}: {topic} | Payload: {Encoding.UTF8.GetString(payloadBytes)}");

            if (_mqttServer != null)
            {
                await _mqttServer.InjectApplicationMessage(new InjectedMqttApplicationMessage(message)
                {
                    SenderClientId = clientId
                });
            }
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

            Console.WriteLine($"[Proxy] Subscripe for ClientId: {clientId}");
            await _remoteClients[clientId].SubscribeAsync(new MqttTopicFilterBuilder().WithTopic($"+/{clientId}").Build());
        }

        private async Task _mqttServer_InterceptingInboundPacketAsync(MQTTnet.Server.InterceptingPacketEventArgs arg)
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
                // Für alle anderen Typen ggf. ToString() serialisieren
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
            var logFile = Path.Combine(logDir, $"inbound_{DateTime.UtcNow:yyyyMMdd}.json");
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
