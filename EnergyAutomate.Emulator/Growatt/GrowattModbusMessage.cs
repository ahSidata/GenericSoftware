using EnergyAutomate.Emulator.Growatt.Models;
using Microsoft.Extensions.Logging;
using System.Text;

namespace EnergyAutomate.Emulator.Growatt
{

    public class GrowattModbusMessage
    {
        public ushort Unknown { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public GrowattMetadata? Metadata { get; set; }
        public GrowattModbusFunction Function { get; set; }
        public List<GrowattModbusBlock> RegisterBlocks { get; set; } = new();

        public int MsgLen
        {
            get
            {
                int result = 32; // 2 byte unknown + 30 byte device id
                if (Metadata != null)
                    result += Metadata.Size();
                foreach (var block in RegisterBlocks)
                    result += block.Size();
                return result;
            }
        }

        public byte[]? GetData(GrowattRegisterPosition pos, ILogger logger = null)
        {
            foreach (var block in RegisterBlocks)
            {
                if (block.Start > pos.RegisterNo || block.End < pos.RegisterNo)
                    continue;
                int blockPos = (pos.RegisterNo - block.Start) * 2 + pos.Offset;
                if (block.Values.Length < blockPos + pos.Size)
                {
                    logger?.LogWarning("GrowattModbusMessage.GetData: Block values too short.");
                    return null;
                }
                var result = new byte[pos.Size];
                Array.Copy(block.Values, blockPos, result, 0, pos.Size);
                logger?.LogTrace("GrowattModbusMessage.GetData: Found data at register {RegisterNo}, offset {Offset}, size {Size}", pos.RegisterNo, pos.Offset, pos.Size);
                return result;
            }
            logger?.LogTrace("GrowattModbusMessage.GetData: Register not found.");
            return null;
        }

        public static GrowattModbusMessage? Parse(byte[] buffer, ILogger logger = null)
        {
            try
            {
                if (buffer.Length < 38)
                {
                    logger?.LogWarning("GrowattModbusMessage.Parse: Buffer too short for header.");
                    return null;
                }
                ushort unknown = (ushort)(buffer[0] << 8 | buffer[1]);
                ushort constant7 = (ushort)(buffer[2] << 8 | buffer[3]);
                ushort msgLen = (ushort)(buffer[4] << 8 | buffer[5]);
                byte constant1 = buffer[6];
                byte function = buffer[7];
                string deviceId = Encoding.ASCII.GetString(buffer, 8, 30).Trim('\0');
                if (msgLen != buffer.Length - 8)
                {
                    logger?.LogWarning("GrowattModbusMessage.Parse: msgLen mismatch. msgLen={MsgLen}, buffer.Length={BufferLength}", msgLen, buffer.Length);
                    return null;
                }
                if (!Enum.IsDefined(typeof(GrowattModbusFunction), function))
                {
                    logger?.LogInformation("Unknown modbus function for {DeviceId}: {Function}", deviceId, function);
                    return null;
                }
                var registerBlocks = new List<GrowattModbusBlock>();
                int offset = 38;
                GrowattMetadata? metadata = null;
                if (function == (byte)GrowattModbusFunction.READ_INPUT_REGISTER || function == (byte)GrowattModbusFunction.READ_HOLDING_REGISTER)
                {
                    metadata = GrowattMetadata.Parse(buffer.AsSpan(offset, 37).ToArray(), logger);
                    offset += 37;
                }
                while (buffer.Length > offset + 6)
                {
                    var block = GrowattModbusBlock.Parse(buffer.AsSpan(offset).ToArray(), logger);
                    if (block == null)
                        break;
                    registerBlocks.Add(block);
                    offset += block.Size();
                }
                logger?.LogTrace("GrowattModbusMessage.Parse: Parsed message for deviceId={DeviceId}, function={Function}, blocks={BlockCount}", deviceId, function, registerBlocks.Count);
                return new GrowattModbusMessage
                {
                    Unknown = unknown,
                    DeviceId = deviceId,
                    Metadata = metadata,
                    Function = (GrowattModbusFunction)function,
                    RegisterBlocks = registerBlocks
                };
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Parsing GrowattModbusMessage failed: {Message}", ex.Message);
                return null;
            }
        }

        public byte[] Build()
        {
            var deviceIdBytes = Encoding.ASCII.GetBytes(DeviceId.PadRight(30, '\0'));
            var result = new List<byte>();
            result.Add((byte)(Unknown >> 8));
            result.Add((byte)(Unknown & 0xFF));
            result.Add(0); // constant 7 high byte
            result.Add(7); // constant 7 low byte
            result.Add((byte)(MsgLen >> 8));
            result.Add((byte)(MsgLen & 0xFF));
            result.Add(1); // constant 1
            result.Add((byte)Function);
            result.AddRange(deviceIdBytes);
            if (Metadata != null)
                result.AddRange(Metadata.Build());
            foreach (var block in RegisterBlocks)
                result.AddRange(block.Build());
            return result.ToArray();
        }
    }
}