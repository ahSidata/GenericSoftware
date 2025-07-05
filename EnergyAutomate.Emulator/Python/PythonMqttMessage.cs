namespace EnergyAutomate.Emulator.Python
{
    /// <summary>
    /// This class describes an incoming MQTT message. It is intended to be passed to the OnMessage callback as the message parameter.
    /// </summary>
    public class PythonMqttMessage
    {
        public double Timestamp { get; set; }
        public int State { get; set; }
        public int Dup { get; set; }
        public int Mid { get; set; }
        public string? Topic { get; set; }
        public byte[]? Payload { get; set; }
        public int Qos { get; set; }
        public int Retain { get; set; }

    }
}