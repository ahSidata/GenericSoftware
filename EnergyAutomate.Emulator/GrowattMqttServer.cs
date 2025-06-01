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
    public class GrowattMqttServer
    {
        private readonly string _proxyCertPath;
        private readonly string _proxyKeyPath;
        private readonly int _proxyPort;

        private MqttServer MqttServer { get; set; }
        private MqttNetEventLogger MqttNetEventLogger { get; set; }

        private readonly ConcurrentDictionary<string, GrowattMqttClient> _remoteClients = new();

        public GrowattMqttServer(string proxyCertPath, string proxyKeyPath, int proxyPort, MqttNetEventLogger mqttNetEventLogger)
        {
            MqttNetEventLogger = mqttNetEventLogger;

            _proxyCertPath = proxyCertPath;
            _proxyKeyPath = proxyKeyPath;
            _proxyPort = proxyPort;

            var proxyCert = X509Certificate2.CreateFromPemFile(_proxyCertPath, _proxyKeyPath);
            var mqttServerOptions = new MqttServerOptionsBuilder()
                .WithEncryptedEndpoint()
                .WithEncryptionCertificate(proxyCert.Export(X509ContentType.Pfx))
                .WithEncryptedEndpointPort(_proxyPort)
                .Build();

            var mqttServerFactory = new MqttServerFactory(mqttNetEventLogger);
            MqttServer = mqttServerFactory.CreateMqttServer(mqttServerOptions);

            MqttServer.InterceptingInboundPacketAsync += MqttServer_InterceptingPacketAsync;
            MqttServer.InterceptingOutboundPacketAsync += MqttServer_InterceptingPacketAsync;

            MqttServer.InterceptingPublishAsync += MqttServer_InterceptingPublishAsync;
            MqttServer.ClientConnectedAsync += MqttServer_ClientConnectedAsync;
            MqttServer.InterceptingSubscriptionAsync += MqttServer_InterceptingSubscriptionAsync;
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
            var growattMqttClient = new GrowattMqttClient("mqtt.growatt.com", 7006, arg, MqttNetEventLogger);

            // Fix: Use an event handler method instead of directly assigning a lambda to the event
            growattMqttClient.ApplicationMessageReceived += async (applicationMessage) =>
            {
                await MqttServer.InjectApplicationMessage(new InjectedMqttApplicationMessage(applicationMessage));
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
            GrowattMqttClient? remoteClient;

            while (!_remoteClients.TryGetValue(arg.ClientId, out remoteClient))
            {
                await Task.Delay(1000);
            }

            var msgBuilder = new MqttApplicationMessageBuilder()
                .WithTopic(arg.ApplicationMessage.Topic)
                .WithPayload(arg.ApplicationMessage.Payload)
                .WithQualityOfServiceLevel(arg.ApplicationMessage.QualityOfServiceLevel)
                .WithRetainFlag(arg.ApplicationMessage.Retain);

            var mappedMessage = msgBuilder.Build();

            Console.WriteLine($"Client --> Broker {arg.ClientId}");
            Console.WriteLine($"Client Application Message Topic: {arg.ApplicationMessage.Topic}");
            //Console.WriteLine($"ClientApplication Message Payload: {Encoding.UTF8.GetString(arg.ApplicationMessage.Payload.ToArray())}");

            await remoteClient.MqttClient.PublishAsync(mappedMessage);

            arg.ProcessPublish = true;
            arg.Response.ReasonCode = MQTTnet.Protocol.MqttPubAckReasonCode.Success;

            await Task.CompletedTask;
        }

        private async Task MqttServer_InterceptingSubscriptionAsync(InterceptingSubscriptionEventArgs arg)
        {
            GrowattMqttClient? remoteClient;

            while (!_remoteClients.TryGetValue(arg.ClientId, out remoteClient))
            {
                await Task.Delay(1000);
            }

            var clientId = arg.ClientId;

            Console.WriteLine($"[Proxy] Subscribing at cloud for ClientId={clientId}: Topic={arg.TopicFilter}");

            var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter(f => f.WithTopic($"s/{arg.ClientId}").WithQualityOfServiceLevel(arg.TopicFilter.QualityOfServiceLevel))
                .Build();

            var result = await remoteClient.MqttClient.SubscribeAsync(subscribeOptions);

            subscribeOptions = new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter(f => f.WithTopic($"s/33/{arg.ClientId}").WithQualityOfServiceLevel(arg.TopicFilter.QualityOfServiceLevel))
                .Build();

            result = await remoteClient.MqttClient.SubscribeAsync(subscribeOptions);

            arg.ProcessSubscription = true;
            arg.Response.ReasonCode = MQTTnet.Protocol.MqttSubscribeReasonCode.GrantedQoS1;

            await Task.CompletedTask;
        }

        private readonly object _lock = new object();

        private Task MqttServer_InterceptingPacketAsync(MQTTnet.Server.InterceptingPacketEventArgs arg)
        {
            lock (_lock)
            {
                Task.Run(async () => { 

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
                }).Wait();
            }

            return Task.CompletedTask;
        }
    }
}
