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

            var mqttServerFactory = new MqttServerFactory();
            MqttServer = mqttServerFactory.CreateMqttServer(mqttServerOptions);

            MqttServer.InterceptingSubscriptionAsync += MqttServer_InterceptingSubscription;
            MqttServer.InterceptingPublishAsync += MqttServer_InterceptingPublishAsync;
            MqttServer.ClientConnectedAsync += MqttServer_ClientConnectedAsync;
        }

        private Task MqttServer_InterceptingSubscription(InterceptingSubscriptionEventArgs arg)
        {
            GrowattMqttClient? growattMqttClient;
            if(_remoteClients.TryGetValue(arg.ClientId, out growattMqttClient))
            {
                Console.WriteLine($"[Proxy] Remote client {arg.ClientId} already exists for subscription.");
            }
            else
            {
                Console.WriteLine($"[Proxy] Remote client {arg.ClientId} not found for subscription.");
            }

            Console.WriteLine($"[Proxy] Remote client {arg.ClientId} add subscription: {arg.TopicFilter.Topic}");
            growattMqttClient?.SubscribedTopics.Add(arg.TopicFilter);

            return Task.CompletedTask;
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
            var growattMqttClient = new GrowattMqttClient("mqtt.growatt.com", 7006, arg.ClientId, arg.UserName, arg.Password, MqttNetEventLogger);
            
            growattMqttClient.ApplicationMessageReceived += GrowattMqttClient_ApplicationMessageReceived;

            _remoteClients.TryAdd(arg.ClientId, growattMqttClient);

            await growattMqttClient.ConnectAsync();

            while (!growattMqttClient.IsConnected)
            {
                Console.WriteLine($"[Proxy] Warten auf Verbindung des Remote-Clients {arg.ClientId}...");
                await Task.Delay(1000);
            }
        }

        private async Task GrowattMqttClient_ApplicationMessageReceived(MqttApplicationMessage mqttApplicationMessage)
        {
            await MqttServer.InjectApplicationMessage(new InjectedMqttApplicationMessage(mqttApplicationMessage));
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
    }
}
