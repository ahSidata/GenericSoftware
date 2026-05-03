using EnergyAutomate.Emulator.Growatt.Models;

namespace EnergyAutomate.Emulator.Growatt
{
    /// <summary>
    /// Data Transfer Object (DTO) representing a Growatt ModbusCodec register block.
    /// Contains frame header and payload information; all encoding/decoding logic is in GrowattModbusCodec.
    /// </summary>
    public class GrowattModbusBlock
    {
        /// <summary>
        /// The ModbusCodec TCP transaction identifier.
        /// </summary>
        public ushort TransactionId { get; set; } = 0x0001;

        /// <summary>
        /// The Growatt-specific protocol identifier (standard ModbusCodec TCP uses 0x0000).
        /// </summary>
        public ushort ProtocolId { get; set; } = 0x0007;

        /// <summary>
        /// The ModbusCodec unit identifier.
        /// </summary>
        public byte UnitId { get; set; } = 0x01;

        /// <summary>
        /// The ModbusCodec function code.
        /// </summary>
        public GrowattModbusFunction Function { get; set; }

        /// <summary>
        /// The starting register address.
        /// </summary>
        public ushort Start { get; set; }

        /// <summary>
        /// The ending register address.
        /// </summary>
        public ushort End { get; set; }

        /// <summary>
        /// The register values as raw bytes.
        /// </summary>
        public byte[] Values { get; set; } = Array.Empty<byte>();
    }
}