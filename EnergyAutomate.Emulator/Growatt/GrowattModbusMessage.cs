using EnergyAutomate.Emulator.Growatt.Models;

namespace EnergyAutomate.Emulator.Growatt
{
    /// <summary>
    /// Data Transfer Object (DTO) representing a parsed Growatt ModbusCodec message.
    /// Contains only properties; all parsing logic is in GrowattModbusCodec.
    /// </summary>
    public class GrowattModbusMessage
    {
        /// <summary>
        /// The function code of the ModbusCodec message.
        /// </summary>
        public GrowattModbusFunction DataHeaderFunction { get; set; }

        /// <summary>
        /// The raw data payload of the ModbusCodec message.
        /// </summary>
        public byte[] DataRaw { get; set; } = [];

        /// <summary>
        /// The device ID associated with the ModbusCodec message.
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// The topic associated with the ModbusCodec message, typically used in MQTT communication.
        /// </summary>
        public string Topic { get; set; } = string.Empty;

        /// <summary>
        /// A string representation of the parsed register values.
        /// </summary>
        public string RegisterStrings { get; set; } = string.Empty;

        /// <summary>
        /// A collection of parsed ModbusCodec register blocks.
        /// </summary>
        public List<GrowattModbusBlock> RegisterBlocks { get; set; } = new();
    }
}
