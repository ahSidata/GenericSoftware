using EnergyAutomate.Emulator.Growatt.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Buffers.Binary;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace EnergyAutomate.Emulator.Growatt
{
    public class GrowattModbusMessage
    {
        #region Constants

        private const int CRC_LENGTH = 2;
        private const int DATA_HEADER_LENGTH = 8;
        private const int DEVICE_ID_LENGTH = 30;
        private const int DEVICE_ID_OFFSET = 8;
        private const int FUNCTION_CODE_OFFSET = 7;
        private const int MIN_MESSAGE_LENGTH = 38; // Header + DeviceID
        private const int MSG_LEN_OFFSET = 4;
        private const int MSG_LEN_LENGTH = 2;

        #endregion Constants

        #region Properties

        public ushort Crc { get; set; }
        public byte[] DataHeader { get; set; } = [];
        public GrowattModbusFunction DataHeaderFunction { get; set; }
        public int DataHeaderMsgLen { get; set; }
        public byte[] DataRaw { get; set; } = [];
        public string DeviceId { get; set; } = string.Empty;
        public string Topic { get; set; } = string.Empty;
        public string RegisterStrings { get; set; } = string.Empty;
        private ILogger? Logger { get; set; }
        private GrowattRegisterModel GrowattRegister { get; set; } = GrowattRegisterModel.SeedDefaults();

        #endregion Properties

        #region Public Constructors

        public GrowattModbusMessage(ILogger logger, string? topic = null, byte[]? buffer = null)
        {
            Logger = logger;

            if (topic != null)
                Topic = topic;

            if (buffer != null)
                DataRaw = buffer;

            if (DataRaw.Length < MIN_MESSAGE_LENGTH)
            {
                Logger?.LogWarning("GrowattModbusMessage.Parse: Buffer too short for header. Length={Length}", DataRaw.Length);
                return;
            }

            DataHeaderParse();
            ProcessFunctionData();
            Dump();
        }

        #endregion Public Constructors

        #region Private Methods

        private void Dump()
        {
            string dumpDirectory = Path.Combine(AppContext.BaseDirectory, "dump");
            if (!Directory.Exists(dumpDirectory))
            {
                Directory.CreateDirectory(dumpDirectory);
                Logger?.LogInformation("[TRACE] Created dump directory at {DumpDirectory}", dumpDirectory);
            }

            string datePart = DateTime.UtcNow.ToString("yyyyMMdd");
            string topicPart = string.Join("_", Topic.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
            string fileName = $"{datePart}_{topicPart}_{(int)DataHeaderFunction}_Messages.txt";
            string filePath = Path.Combine(dumpDirectory, fileName);

            string fileNameDataRaw = $"{datePart}_DataRaw.txt";
            string filePathDataRaw = Path.Combine(dumpDirectory, fileName);

            StringBuilder sb = new StringBuilder();
            sb.Append($"HeaderBytes: {BitConverter.ToString(DataHeader)} ");
            sb.Append($"Function: {(int)DataHeaderFunction} ");
            sb.Append($"MsgLen: {DataHeaderMsgLen} ");
            sb.Append($"DeviceId: {DeviceId} ");
            sb.Append($"BlockCount: {RegisterBlocks.Count} ");
            sb.AppendLine($"RegisterStrings: {RegisterStrings} ");

            File.AppendAllText(filePath, sb.ToString());

            StringBuilder sbDataRaw = new StringBuilder();
            sbDataRaw.AppendLine($"Topic: {Topic}, DataRaw: {BitConverter.ToString(DataRaw)} ");

            File.AppendAllText(filePathDataRaw, sbDataRaw.ToString());
        }

        public ushort Register { get; set; }

        public List<GrowattModbusBlock> RegisterBlocks { get; set; } = new();

        public ushort Value { get; set; }

        public static ushort CalculateCrc(byte[] data, int offset, int length)
        {
            ushort crc = 0xFFFF;

            for (int i = offset; i < offset + length; i++)
            {
                crc ^= data[i];
                for (int j = 0; j < 8; j++)
                {
                    bool lsb = (crc & 0x0001) != 0;
                    crc >>= 1;
                    if (lsb)
                    {
                        crc ^= 0xA001; // Modbus CRC polynomial
                    }
                }
            }

            return crc;
        }

        public byte[] BuildMultiple(GrowattModbusBlock growattModbusBlock)
        {
            byte[] deviceIdBytes = Encoding.ASCII.GetBytes(DeviceId.PadRight(30, '\0'));
            ushort msgLen = (ushort)(36 + growattModbusBlock.Values.Length);

            byte[] result = new byte[42 + growattModbusBlock.Values.Length];
            result[0] = 0;
            result[1] = 1; // unknown = 1
            result[2] = 0;
            result[3] = 7; // constant 7
            result[4] = (byte)(msgLen >> 8); result[5] = (byte)(msgLen & 0xFF);
            result[6] = 1; // constant 1
            result[7] = (byte)DataHeaderFunction;
            Array.Copy(deviceIdBytes, 0, result, 8, 30);
            result[38] = (byte)(growattModbusBlock.Start >> 8); result[39] = (byte)(growattModbusBlock.Start & 0xFF);
            result[40] = (byte)(growattModbusBlock.End >> 8); result[41] = (byte)(growattModbusBlock.End & 0xFF);
            Array.Copy(growattModbusBlock.Values, 0, result, 42, growattModbusBlock.Values.Length);

            // Add CRC
            ushort crc = CalculateCrc(result, 0, result.Length);
            byte[] withCrc = new byte[result.Length + 2];
            Array.Copy(result, withCrc, result.Length);
            withCrc[result.Length] = (byte)(crc >> 8);
            withCrc[result.Length + 1] = (byte)(crc & 0xFF);

            return withCrc;
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
            result[7] = (byte)DataHeaderFunction;
            Array.Copy(deviceIdBytes, 0, result, 8, 30);
            result[38] = (byte)(Register >> 8); result[39] = (byte)(Register & 0xFF);
            result[40] = (byte)(Value >> 8); result[41] = (byte)(Value & 0xFF);

            // Add CRC
            ushort crc = CalculateCrc(result, 0, result.Length);
            byte[] withCrc = new byte[result.Length + 2];
            Array.Copy(result, withCrc, result.Length);
            withCrc[result.Length] = (byte)(crc >> 8);
            withCrc[result.Length + 1] = (byte)(crc & 0xFF);

            return withCrc;
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

        private void DataHeaderParse()
        {
            DataHeader = new byte[DATA_HEADER_LENGTH];
            Array.Copy(DataRaw, 0, DataHeader, 0, DATA_HEADER_LENGTH);

            // Diese Methode ist speziell für Big-Endian-Werte gedacht
            DataHeaderMsgLen = BinaryPrimitives.ReadUInt16BigEndian(DataRaw.AsSpan(MSG_LEN_OFFSET, MSG_LEN_LENGTH));

            // Verify message length
            if (DataHeaderMsgLen != DataRaw.Length - DATA_HEADER_LENGTH)
            {
                Logger?.LogWarning("GrowattModbusMessage.ParseHeader: msgLen mismatch. msgLen={MsgLen}, buffer.Length-8={ActualLength}",
                    DataHeaderMsgLen, DataRaw.Length - DATA_HEADER_LENGTH);
            }

            byte function = DataRaw[FUNCTION_CODE_OFFSET];

            DataHeaderFunction = (GrowattModbusFunction)function;
            DeviceId = Encoding.ASCII.GetString(DataRaw, DEVICE_ID_OFFSET, DEVICE_ID_LENGTH).Trim('\0');
        }

        private void ParsePresetMultipleRequest()
        {
            if (DataRaw.Length < 42)
            {
                Logger?.LogWarning("GrowattModbusMessage.ParsePresetMultipleRequest: Buffer too short for PRESET_MULTIPLE_REGISTER. Length={BufferLength}", DataRaw.Length);
                return;
            }
            GrowattModbusBlock growattModbusBlock = new GrowattModbusBlock();

            growattModbusBlock.Start = (ushort)(DataRaw[38] << 8 | DataRaw[39]);
            growattModbusBlock.End = (ushort)(DataRaw[40] << 8 | DataRaw[41]);

            int valuesLength = DataRaw.Length - 42;
            if (valuesLength > 3)
            {
                growattModbusBlock.Values = new byte[valuesLength];
                Array.Copy(DataRaw, 42, growattModbusBlock.Values, 0, valuesLength);
            }

            RegisterBlocks.Add(growattModbusBlock);

            var result = ParseRegisters(GrowattRegister.PresentRegisters);

            if(result.Any())
                RegisterStrings = $"ValueCount={result.Count}" + string.Join(", ", result.Select(kv => $"{kv.Key}={kv.Value}"));
            else
                RegisterStrings = string.Join(", ", RegisterBlocks.Select(x => $"Start:{x.Start},End:{x.End},Values:{BitConverter.ToString(x.Values)}"));
        }

        private void ParsePresetSingleRequest()
        {
            if (DataRaw.Length < 42)
            {
                Logger?.LogWarning("GrowattModbusMessage.ParsePresetSingleRequest: Buffer too short for PRESET_SINGLE_REGISTER. Length={BufferLength}", DataRaw.Length);
                return;
            }
            Register = (ushort)(DataRaw[38] << 8 | DataRaw[39]);
            Value = (ushort)(DataRaw[40] << 8 | DataRaw[41]);

            RegisterStrings = $"{Register}={Value}";
        }

        private void ParseReadResponse()
        {
            try
            {
                GrowattMetadata? readMetadata = null;

                int offset = MIN_MESSAGE_LENGTH;
                if (DataRaw.Length >= offset + 37)
                {
                    readMetadata = GrowattMetadata.Parse(DataRaw.AsSpan(offset, 37).ToArray(), Logger);
                    offset += 37;
                }

                int msgLen = 32; // 2 byte unknown + 30 byte device id
                if (readMetadata != null)
                    msgLen += readMetadata.Size();
                foreach (var block in RegisterBlocks)
                    msgLen += block.Size();

                var registerBlocks = new List<GrowattModbusBlock>();
                while (DataRaw.Length > offset + 6) // Minimum size for a block header
                {
                    var block = GrowattModbusBlock.Parse(DataRaw.AsSpan(offset).ToArray(), Logger);
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
                    DeviceId, DataHeaderFunction, RegisterBlocks.Count);

                if (DataHeaderFunction == GrowattModbusFunction.READ_HOLDING_REGISTER)
                {
                    var result = ParseRegisters(GrowattRegister.HoldingRegisters);
                    RegisterStrings = $"ValueCount={result.Count}" + string.Join(", ", result.Select(kv => $"{kv.Key}={kv.Value}"));
                }

                if (DataHeaderFunction == GrowattModbusFunction.READ_INPUT_REGISTER)
                {
                    var result = ParseRegisters(GrowattRegister.InputRegisters);
                    RegisterStrings = $"ValueCount={result.Count}" + string.Join(", ", result.Select(kv => $"{kv.Key}={kv.Value}"));
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Parsing GrowattModbusMessage (ReadResponse) failed: {Message}", ex.Message);
            }
        }

        private void ProcessFunctionData()
        {
            switch (DataHeaderFunction)
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
                    Logger?.LogWarning("GrowattModbusMessage.ProcessFunctionData: No parsing logic implemented for function {Function}", DataHeaderFunction);
                    break;
            }
        }

        private Dictionary<string, object> ParseRegisters(Dictionary<string, GrowattParameter> keyValuePairs)
        {
            var result = new Dictionary<string, object>();

            foreach (var kvp in keyValuePairs)
            {
                string name = kvp.Key;
                var register = kvp.Value;

                // Assuming GrowattRegisterModel has a property or method to get the position
                var position = register.Growatt.Position; // Replace with actual method/property

                // Get raw data for the register
                var dataRaw = GetData(position);

                if (dataRaw == null)
                {
                    continue;
                }

                // Assuming GrowattRegisterModel has a method to parse data
                var value = register.Growatt.Data.Parse(dataRaw); // Replace with actual method

                if (value == null)
                {
                    continue;
                }

                result[name] = value;
            }

            return result;
        }

        #endregion Private Methods
    }
}
