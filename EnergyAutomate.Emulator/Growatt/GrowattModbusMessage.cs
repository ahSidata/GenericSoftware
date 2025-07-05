using EnergyAutomate.Emulator.Growatt.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace EnergyAutomate.Emulator.Growatt
{

    public class GrowattModbusMessage
    {
        public byte[] RawData { get; set; } = []; // Initialize with an empty array

        public ushort Unknown { get; set; }

        public string Topic { get; set; } = string.Empty;

        public string DeviceId { get; set; } = string.Empty;

        public GrowattMetadata? Metadata { get; set; }

        public GrowattModbusFunction Function { get; set; }

        public int MsgLenRead
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

        public int MsgLenPresent { get; set; }

        private ILogger? Logger { get; set; }

        private void ParseReadResponse()
        {
            try
            {
                int offset = 38;
                if (RawData.Length >= offset + 37)
                {
                    Metadata = GrowattMetadata.Parse(RawData.AsSpan(offset, 37).ToArray(), Logger);
                    offset += 37;
                }

                var registerBlocks = new List<GrowattModbusBlock>();
                while (RawData.Length > offset + 6) // Minimum size for a block header
                {
                    var block = GrowattModbusBlock.Parse(RawData.AsSpan(offset).ToArray(), Logger);
                    if (block == null)
                    {
                        Logger?.LogWarning("GrowattModbusMessage.ParseReadResponse: Failed to parse a register block at offset {Offset}", offset);
                        break;
                    }
                    registerBlocks.Add(block);
                    offset += block.Size();
                }
                RegisterBlocks = registerBlocks;

                Logger?.LogTrace("GrowattModbusMessage.ParseReadResponse: Parsed message for deviceId={DeviceId}, function={Function}, blocks={BlockCount}",
                    DeviceId, Function, RegisterBlocks.Count);
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Parsing GrowattModbusMessage (ReadResponse) failed: {Message}", ex.Message);
            }
        }

        #region Multiple

        public List<GrowattModbusBlock> RegisterBlocks { get; set; } = new();

        public GrowattModbusMessage(ILogger logger, string? topic = null, byte[]? buffer = null)
        {
            Logger = logger;

            Topic = topic ?? string.Empty;

            if (buffer != null)
                RawData = buffer;

            if (RawData.Length < 38)
            {
                Logger?.LogWarning("GrowattModbusMessage.Parse: Buffer too short for header.");
                return;
            }

            Unknown = (ushort)(RawData[0] << 8 | RawData[1]);
            DeviceId = Encoding.ASCII.GetString(RawData, 8, 30).Trim('\0');

            ushort constant7 = (ushort)(RawData[2] << 8 | RawData[3]);

            MsgLenPresent = (ushort)(RawData[4] << 8 | RawData[5]);

            if (MsgLenPresent != RawData.Length - 8)
            {
                Logger?.LogWarning("GrowattModbusMessage.Parse: msgLen mismatch. msgLen={MsgLen}, buffer.Length={BufferLength}", MsgLenPresent, RawData.Length);
                return;
            }

            byte constant1 = RawData[6];

            byte function = RawData[7];

            if (!Enum.IsDefined(typeof(GrowattModbusFunction), function))
            {
                Logger?.LogInformation("Unknown modbus function for {DeviceId}: {Function}", DeviceId, function);
                return;
            }

            Function = (GrowattModbusFunction)function;

            switch (Function)
            {
                case GrowattModbusFunction.READ_INPUT_REGISTER:
                case GrowattModbusFunction.READ_HOLDING_REGISTER:
                    ParseReadResponse();
                    break;

                case GrowattModbusFunction.PRESET_MULTIPLE_REGISTER:
                    ParsePresetMultipleRequest();
                    break;

                case GrowattModbusFunction.PRESET_SINGLE_REGISTER:
                    ParsePresetSingleRequest();
                    break;

                default:
                    Logger?.LogWarning("GrowattModbusMessage.Parse: No parsing logic implemented for function {Function}", Function);
                    break;
            }
        }

        private void ParsePresetMultipleRequest()
        {
            if (RawData.Length < 42)
            {
                Logger?.LogWarning("GrowattModbusMessage.ParsePresetMultipleRequest: Buffer too short for PRESET_MULTIPLE_REGISTER. Length={BufferLength}", RawData.Length);
                return;
            }
            GrowattModbusBlock growattModbusBlock = new GrowattModbusBlock();

            growattModbusBlock.Start = (ushort)(RawData[38] << 8 | RawData[39]);
            growattModbusBlock.End = (ushort)(RawData[40] << 8 | RawData[41]);

            int valuesLength = RawData.Length - 42;
            if (valuesLength > 3)
            {
                growattModbusBlock.Values = new byte[valuesLength];
                Array.Copy(RawData, 42, growattModbusBlock.Values, 0, valuesLength);
            }

            RegisterBlocks.Add(growattModbusBlock);

            Logger?.LogTrace("GrowattModbusMessage.ParsePresetMultipleRequest: Parsed PRESET_MULTIPLE_REGISTER for DeviceId={DeviceId}, Start={Start}, End={End}, ValuesLength={ValuesLength}",
                DeviceId, growattModbusBlock.Start, growattModbusBlock.End, growattModbusBlock.Values.Length);
        }

        public byte[]? GetData(GrowattRegisterPosition pos)
        {
            foreach (var block in RegisterBlocks)
            {
                if (block.Start > pos.RegisterNo || block.End < pos.RegisterNo)
                    continue;
                int blockPos = (pos.RegisterNo - block.Start) * 2 + pos.Offset;
                if (block.Values.Length < blockPos + pos.Size)
                {
                    Logger?.LogWarning("GrowattModbusMessage.GetData: Block values too short.");
                    return null;
                }
                var result = new byte[pos.Size];
                Array.Copy(block.Values, blockPos, result, 0, pos.Size);
                return result;
            }
            return null;
        }

        public byte[] BuildMultiple(GrowattModbusBlock growattModbusBlock)
        {
            byte[] deviceIdBytes = Encoding.ASCII.GetBytes(DeviceId.PadRight(30, '\0'));
            ushort msgLen = (ushort)(36 + growattModbusBlock.Values.Length);

            byte[] result = new byte[42 + growattModbusBlock.Values.Length];
            result[0] = 0; result[1] = 1; // unknown = 1
            result[2] = 0; result[3] = 7; // constant 7
            result[4] = (byte)(msgLen >> 8); result[5] = (byte)(msgLen & 0xFF);
            result[6] = 1; // constant 1
            result[7] = (byte)Function;
            Array.Copy(deviceIdBytes, 0, result, 8, 30);
            result[38] = (byte)(growattModbusBlock.Start >> 8); result[39] = (byte)(growattModbusBlock.Start & 0xFF);
            result[40] = (byte)(growattModbusBlock.End >> 8); result[41] = (byte)(growattModbusBlock.End & 0xFF);
            Array.Copy(growattModbusBlock.Values, 0, result, 42, growattModbusBlock.Values.Length);
            return result;
        }

        #endregion Multiple

        #region Single

        public ushort Register { get; set; }
        public ushort Value { get; set; }

        private void ParsePresetSingleRequest()
        {
            if (RawData.Length < 42)
            {
                Logger?.LogWarning("GrowattModbusMessage.ParsePresetSingleRequest: Buffer too short for PRESET_SINGLE_REGISTER. Length={BufferLength}", RawData.Length);
                return;
            }
            Register = (ushort)(RawData[38] << 8 | RawData[39]);
            Value = (ushort)(RawData[40] << 8 | RawData[41]);
            Logger?.LogTrace("GrowattModbusMessage.ParsePresetSingleRequest: Parsed PRESET_SINGLE_REGISTER for DeviceId={DeviceId}, Register={Register}, Value={Value}", DeviceId, Register, Value);
        }

        public byte[] BuildSingle()
        {
            byte[] deviceIdBytes = Encoding.ASCII.GetBytes(DeviceId.PadRight(30, '\0'));
            ushort msgLen = 36;

            byte[] result = new byte[42];
            result[0] = 0; result[1] = 1; // unknown = 1
            result[2] = 0; result[3] = 7; // constant 7
            result[4] = (byte)(msgLen >> 8); result[5] = (byte)(msgLen & 0xFF);
            result[6] = 1; // constant 1
            result[7] = (byte)Function;
            Array.Copy(deviceIdBytes, 0, result, 8, 30);
            result[38] = (byte)(Register >> 8); result[39] = (byte)(Register & 0xFF);
            result[40] = (byte)(Value >> 8); result[41] = (byte)(Value & 0xFF);

            return result;
        }

        #endregion Single
    }
}