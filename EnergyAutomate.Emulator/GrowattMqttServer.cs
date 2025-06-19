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
            Console.WriteLine($"[Proxy] Remote client {arg.ClientId} add subscription: {arg.TopicFilter.Topic}");

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

        }

        private async Task GrowattMqttClient_ApplicationMessageReceived(MqttApplicationMessage mqttApplicationMessage)
        {
            await MqttServer.InjectApplicationMessage(new InjectedMqttApplicationMessage(mqttApplicationMessage));
        }

        private async Task MqttServer_InterceptingPublishAsync(InterceptingPublishEventArgs arg)
        {
            var msgBuilder = new MqttApplicationMessageBuilder()
                .WithTopic(arg.ApplicationMessage.Topic)
                .WithPayload(arg.ApplicationMessage.Payload)
                .WithQualityOfServiceLevel(arg.ApplicationMessage.QualityOfServiceLevel)
                .WithRetainFlag(arg.ApplicationMessage.Retain);

            var mappedMessage = msgBuilder.Build();

            Console.WriteLine($"Client --> Broker {arg.ClientId}");
            Console.WriteLine($"Client Application Message Topic: {arg.ApplicationMessage.Topic}");
            //Console.WriteLine($"ClientApplication Message Payload: {Encoding.UTF8.GetString(arg.ApplicationMessage.Payload.ToArray())}");

            arg.ProcessPublish = true;
            arg.Response.ReasonCode = MQTTnet.Protocol.MqttPubAckReasonCode.Success;            

            await Task.CompletedTask;
        }
    }
}
