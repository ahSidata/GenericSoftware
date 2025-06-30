namespace EnergyAutomate.Emulator
{
    /// <summary>
    /// Represents a parsed Modbus message from Growatt MQTT.
    /// </summary>
    public class ModbusMessage
    {
        public int FunctionCode { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public byte[] Raw { get; set; } = Array.Empty<byte>();

        public override string ToString()
        {
            return $"FunctionCode={FunctionCode}, DeviceId={DeviceId}, Data={BitConverter.ToString(Data)}";
        }
    }
}