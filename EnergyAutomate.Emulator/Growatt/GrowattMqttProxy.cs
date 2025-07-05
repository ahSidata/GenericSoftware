using Microsoft.Extensions.Logging;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

namespace EnergyAutomate.Emulator.Growatt
{
    public class GrowattMqttProxy
    {
        private ILogger<GrowattMqttProxy> _logger;

        #region Public Constructors

        public GrowattMqttProxy(string clientId, string brokerHost, int brokerPort, string growattHost, int growattPort, ILogger<GrowattMqttProxy> logger)
        {
            _logger = logger;

            ClientId = clientId;

            BrokerMqttClient = new MqttClient(
                brokerHostName: brokerHost,
                brokerPort: brokerPort,
                secure: true,
                sslProtocol: MqttSslProtocols.TLSv1_2,
                userCertificateValidationCallback: (sender, certificate, chain, sslPolicyErrors) =>
                {
                    // Allow any certificate for testing purposes
                    return true;
                }
            );

            BrokerMqttClient.ProtocolVersion = MqttProtocolVersion.Version_3_1_1;

            BrokerMqttClient.MqttMsgPublishReceived += BrokerMqttClient_MqttMsgPublishReceived;
            BrokerMqttClient.ConnectionClosed += BrokerMqttClient_ConnectionClosed;

            GrowattMqttClient = new MqttClient(
                brokerHostName: growattHost,
                brokerPort: growattPort,
                secure: true,
                sslProtocol: MqttSslProtocols.TLSv1_2,
                userCertificateValidationCallback: (sender, certificate, chain, sslPolicyErrors) =>
                {
                    // Allow any certificate for testing purposes
                    return true;
                }
            );

            GrowattMqttClient.ProtocolVersion = MqttProtocolVersion.Version_3_1_1;


            GrowattMqttClient.MqttMsgPublishReceived += GrowattMqttClient_MqttMsgPublishReceived;
            GrowattMqttClient.ConnectionClosed += GrowattMqttClient_ConnectionClosed;
        }

        private void GrowattMqttClient_ConnectionClosed(object sender, EventArgs e)
        {
            Task.Delay(1000);
            StartGrowatt();
        }

        private void BrokerMqttClient_ConnectionClosed(object sender, EventArgs e)
        {
            Task.Delay(1000);
            StartBroker();
        }

        #endregion Public Constructors

        #region Properties

        public MqttClient BrokerMqttClient { get; set; }

        public MqttClient GrowattMqttClient { get; set; }

        public string ClientId { get; set; }

        #endregion Properties

        #region Public Methods

        public void Start()
        {
            StartBroker();
            StartGrowatt();
        }

        public void StartBroker()
        {
            _logger.LogInformation($"Starting broker connection for ClientId: {ClientId}");
            BrokerMqttClient.Connect($"relay.{ClientId}", ClientId, "Growatt", false, 60);

            while (!BrokerMqttClient.IsConnected)
            {
                Task.Delay(100);
            }

            var topic = $"c/#";
            // QoS 1 = at least once
            byte qosLevel = MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE;
            _logger.LogInformation($"[TRACE] Subscribing to topic '{topic}' with QoS {qosLevel} (at least once)");
            BrokerMqttClient.Subscribe(new string[] { topic }, new byte[] { qosLevel });
        }

        public void StartGrowatt()
        {
            _logger.LogInformation($"Starting growatt connection for ClientId: {ClientId}");
            GrowattMqttClient.Connect(ClientId, ClientId, "Growatt", false, 420);

            while (!GrowattMqttClient.IsConnected)
            {
                Task.Delay(100);
            }

            //var topic = $"s/33/{ClientId}";
            //// QoS 1 = at least once
            //byte qosLevel = MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE;
            //_logger.LogInformation($"[TRACE] Subscribing to topic '{topic}' with QoS {qosLevel} (at least once)");
            //GrowattMqttClient.Subscribe(new string[] { topic }, new byte[] { qosLevel });
        }

        public void Stop()
        {
            _logger.LogInformation($"Stoping broker connection for ClientId: {ClientId}");
            BrokerMqttClient.Disconnect();
            _logger.LogInformation($"Stoping growatt connection for ClientId: {ClientId}");
            GrowattMqttClient.Disconnect();
        }

        #endregion Public Methods

        #region Private Methods

        private void BrokerMqttClient_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
        {
            _logger.LogInformation($"Broker --> Growatt {ClientId}, Topic: {e.Topic}");

            GrowattMqttClient?.Publish(e.Topic, e.Message, e.QosLevel, e.Retain);
        }

        private void GrowattMqttClient_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
        {
            var topic = $"s/33/{ClientId}";

            _logger.LogInformation($"Growatt --> Broker {ClientId}, Topic: {e.Topic}, New Topic: {topic}");

            BrokerMqttClient?.Publish(topic, e.Message, e.QosLevel, e.Retain);
        }

        #endregion Private Methods
    }
}
