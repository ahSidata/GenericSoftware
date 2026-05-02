using EnergyAutomate.Emulator.Growatt.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Buffers.Binary;
using System.Text;

namespace EnergyAutomate.Emulator.Growatt
{
    /// <summary>
    /// Represents a Growatt Modbus message, providing functionality to parse, process, and build Modbus messages.
    /// </summary>
    public class GrowattModbusMessage
    {
        #region Constants

        private const string DumpDirectoryEnvironmentVariable = "DUMP_DIR";
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

        /// <summary>
        /// The CRC (Cyclic Redundancy Check) value of the message.
        /// </summary>
        public ushort Crc { get; set; }

        /// <summary>
        /// The raw data header of the Modbus message.
        /// </summary>
        public byte[] DataHeader { get; set; } = [];

        /// <summary>
        /// The function code of the Modbus message.
        /// </summary>
        public GrowattModbusFunction DataHeaderFunction { get; set; }

        /// <summary>
        /// The length of the message as specified in the header.
        /// </summary>
        public int DataHeaderMsgLen { get; set; }

        /// <summary>
        /// The raw data payload of the Modbus message.
        /// </summary>
        public byte[] DataRaw { get; set; } = [];

        /// <summary>
        /// The device ID associated with the Modbus message.
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// The topic associated with the Modbus message, typically used in MQTT communication.
        /// </summary>
        public string Topic { get; set; } = string.Empty;

        /// <summary>
        /// A string representation of the parsed register values.
        /// </summary>
        public string RegisterStrings { get; set; } = string.Empty;

        /// <summary>
        /// A collection of parsed Modbus register blocks.
        /// </summary>
        public List<GrowattModbusBlock> RegisterBlocks { get; set; } = new();

        private ILogger? Logger { get; set; }
        private GrowattRegisterModel GrowattRegister { get; set; } = GrowattRegisterModel.SeedDefaults();

        #endregion Properties

        #region Public Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="GrowattModbusMessage"/> class.
        /// </summary>
        /// <param name="logger">The logger instance for logging operations.</param>
        /// <param name="topic">The topic associated with the message (optional).</param>
        /// <param name="buffer">The raw data buffer of the message (optional).</param>
        public GrowattModbusMessage(ILogger logger, string? topic = null, byte[]? buffer = null)
        {
            Logger = logger;

            if (topic != null)
                Topic = topic;

            if (buffer != null)
                DataRaw = buffer;

            if (DataRaw.Length < MIN_MESSAGE_LENGTH)
            {
                Logger?.LogWarning("[GrowattModbusMessage] Buffer too short for header. Length={Length}", DataRaw.Length);
                return;
            }

            Logger?.LogTrace("[GrowattModbusMessage] Parsing message with topic={Topic}", Topic);

            DataHeaderParse();
            ProcessFunctionData();
            Dump();
        }

        #endregion Public Constructors

        #region Private Methods

        /// <summary>
        /// Dumps the parsed Modbus message to a file for debugging purposes.
        /// </summary>
        private void Dump()
        {
            try
            {
                string timestampPart = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fffffff");
                string topicPart = string.Join("_", Topic.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));

                string dumpDirectory = Path.Combine(GetDumpRootDirectory(), "Dump", topicPart, DataHeaderFunction.ToString());
                if (!Directory.Exists(dumpDirectory))
                {
                    Directory.CreateDirectory(dumpDirectory);
                    Logger?.LogInformation("[GrowattModbusMessage.Dump] Created dump directory at {DumpDirectory}", dumpDirectory);
                }

                string fileName = $"{timestampPart}_{topicPart}_{DataHeaderFunction.ToString()}_{Guid.NewGuid():N}_Messages.txt";
                string filePath = Path.Combine(dumpDirectory, fileName);

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("=== Parsed Result ===");
                sb.AppendLine($"HeaderBytes: {BitConverter.ToString(DataHeader)}");
                sb.AppendLine($"Function: {(int)DataHeaderFunction}");
                sb.AppendLine($"MsgLen: {DataHeaderMsgLen}");
                sb.AppendLine($"DeviceId: {DeviceId}");
                sb.AppendLine($"BlockCount: {RegisterBlocks.Count}");
                sb.AppendLine($"RegisterStrings: {RegisterStrings}");
                sb.AppendLine();
                sb.AppendLine("=== Raw Data ===");
                sb.AppendLine($"RawData: {BitConverter.ToString(DataRaw)}");

                File.WriteAllText(filePath, sb.ToString());

                Logger?.LogTrace("[GrowattModbusMessage.Dump] Dumped message to {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "[GrowattModbusMessage.Dump] Failed to dump message: {Message}", ex.Message);
            }
        }

        private static string GetDumpRootDirectory()
        {
            var configuredDirectory = Environment.GetEnvironmentVariable(DumpDirectoryEnvironmentVariable);
            return string.IsNullOrWhiteSpace(configuredDirectory) ? AppContext.BaseDirectory : configuredDirectory;
        }

        private static string SanitizeFolderName(string value)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new string(value.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
            return string.IsNullOrWhiteSpace(sanitized) ? "unknown" : sanitized;
        }

        /// <summary>
        /// Parses the data header of the Modbus message.
        /// </summary>
        private void DataHeaderParse()
        {
            try
            {
                DataHeader = new byte[DATA_HEADER_LENGTH];
                Array.Copy(DataRaw, 0, DataHeader, 0, DATA_HEADER_LENGTH);

                DataHeaderMsgLen = BinaryPrimitives.ReadUInt16BigEndian(DataRaw.AsSpan(MSG_LEN_OFFSET, MSG_LEN_LENGTH));

                if (DataHeaderMsgLen != DataRaw.Length - DATA_HEADER_LENGTH)
                {
                    Logger?.LogWarning("[GrowattModbusMessage.DataHeaderParse] msgLen mismatch. msgLen={MsgLen}, buffer.Length-8={ActualLength}",
                        DataHeaderMsgLen, DataRaw.Length - DATA_HEADER_LENGTH);
                }

                byte function = DataRaw[FUNCTION_CODE_OFFSET];
                DataHeaderFunction = (GrowattModbusFunction)function;
                DeviceId = Encoding.ASCII.GetString(DataRaw, DEVICE_ID_OFFSET, DEVICE_ID_LENGTH).Trim('\0');

                Logger?.LogTrace("[GrowattModbusMessage.DataHeaderParse] Parsed header. Function={Function}, DeviceId={DeviceId}", DataHeaderFunction, DeviceId);
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "[GrowattModbusMessage.DataHeaderParse] Failed to parse header: {Message}", ex.Message);
            }
        }

        /// <summary>
        /// Processes the function-specific data of the Modbus message.
        /// </summary>
        private void ProcessFunctionData()
        {
            try
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
                        Logger?.LogWarning("[GrowattModbusMessage.ProcessFunctionData] No parsing logic implemented for function {Function}", DataHeaderFunction);
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "[GrowattModbusMessage.ProcessFunctionData] Failed to process function data: {Message}", ex.Message);
            }
        }

        /// <summary>
        /// Parses the response for read operations.
        /// </summary>
        private void ParseReadResponse()
        {
            Logger?.LogTrace("[GrowattModbusMessage.ParseReadResponse] Parsing read response.");

            try
            {
                if (DataRaw.Length <= DATA_HEADER_LENGTH + CRC_LENGTH)
                {
                    Logger?.LogWarning("[GrowattModbusMessage.ParseReadResponse] Buffer too short for read response payload. Length={Length}", DataRaw.Length);
                    return;
                }

                var payloadOffset = DATA_HEADER_LENGTH;
                var payloadLength = DataRaw.Length - DATA_HEADER_LENGTH - CRC_LENGTH;
                if (payloadLength <= 0)
                {
                    Logger?.LogWarning("[GrowattModbusMessage.ParseReadResponse] No payload available for parsing.");
                    return;
                }

                var payload = new byte[payloadLength];
                Array.Copy(DataRaw, payloadOffset, payload, 0, payloadLength);

                var registerMap = DataHeaderFunction == GrowattModbusFunction.READ_HOLDING_REGISTER
                    ? GrowattRegister.HoldingRegisters
                    : GrowattRegister.InputRegisters;

                if (registerMap.Count == 0)
                {
                    Logger?.LogWarning("[GrowattModbusMessage.ParseReadResponse] No register definitions available for function {Function}.", DataHeaderFunction);
                    return;
                }

                RegisterBlocks.Clear();
                RegisterStrings = string.Empty;

                var orderedRegisters = registerMap
                    .Select(kvp => kvp.Value.Growatt)
                    .OrderBy(r => r.Position.RegisterNo)
                    .ToList();

                var registerValues = new List<string>();
                foreach (var register in orderedRegisters)
                {
                    var startIndex = (register.Position.RegisterNo - 1) * 2 + register.Position.Offset;
                    var size = register.Position.Size <= 0 ? 2 : register.Position.Size;

                    if (startIndex < 0 || startIndex + size > payload.Length)
                    {
                        Logger?.LogWarning(
                            "[GrowattModbusMessage.ParseReadResponse] Register {RegisterNo} is out of range. StartIndex={StartIndex}, Size={Size}, PayloadLength={PayloadLength}",
                            register.Position.RegisterNo,
                            startIndex,
                            size,
                            payload.Length);
                        continue;
                    }

                    var raw = new byte[size];
                    Array.Copy(payload, startIndex, raw, 0, size);

                    var parsedValue = register.Data.Parse(raw);
                    if (parsedValue == null)
                    {
                        continue;
                    }

                    registerValues.Add($"{register.Position.RegisterNo}={parsedValue}");
                    RegisterBlocks.Add(new GrowattModbusBlock
                    {
                        Start = (ushort)register.Position.RegisterNo,
                        End = (ushort)register.Position.RegisterNo,
                        Values = raw
                    });
                }

                RegisterStrings = string.Join(", ", registerValues);
                Logger?.LogTrace("[GrowattModbusMessage.ParseReadResponse] Parsed {Count} register values.", registerValues.Count);
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "[GrowattModbusMessage.ParseReadResponse] Failed to parse read response: {Message}", ex.Message);
            }
        }

        /// <summary>
        /// Parses the request for presetting multiple registers.
        /// </summary>
        private void ParsePresetMultipleRequest()
        {
            Logger?.LogTrace("[GrowattModbusMessage.ParsePresetMultipleRequest] Parsing preset multiple request.");

            try
            {
                if (DataRaw.Length <= DATA_HEADER_LENGTH + CRC_LENGTH)
                {
                    Logger?.LogWarning("[GrowattModbusMessage.ParsePresetMultipleRequest] Buffer too short for preset multiple request. Length={Length}", DataRaw.Length);
                    return;
                }

                var payloadLength = DataRaw.Length - DATA_HEADER_LENGTH - CRC_LENGTH;
                if (payloadLength < 4)
                {
                    Logger?.LogWarning("[GrowattModbusMessage.ParsePresetMultipleRequest] Payload too short to contain register block.");
                    return;
                }

                var payload = new byte[payloadLength];
                Array.Copy(DataRaw, DATA_HEADER_LENGTH, payload, 0, payloadLength);

                var block = GrowattModbusBlock.Parse(payload, Logger);
                if (block == null)
                {
                    return;
                }

                RegisterBlocks.Clear();
                RegisterBlocks.Add(block);

                RegisterStrings = $"{block.Start}-{block.End}={BitConverter.ToString(block.Values)}";
                Logger?.LogTrace("[GrowattModbusMessage.ParsePresetMultipleRequest] Parsed block Start={Start}, End={End}, ValuesLength={ValuesLength}", block.Start, block.End, block.Values.Length);
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "[GrowattModbusMessage.ParsePresetMultipleRequest] Failed to parse preset multiple request: {Message}", ex.Message);
            }
        }

        /// <summary>
        /// Parses the request for presetting a single register.
        /// </summary>
        private void ParsePresetSingleRequest()
        {
            Logger?.LogTrace("[GrowattModbusMessage.ParsePresetSingleRequest] Parsing preset single request.");

            try
            {
                if (DataRaw.Length <= DATA_HEADER_LENGTH + CRC_LENGTH)
                {
                    Logger?.LogWarning("[GrowattModbusMessage.ParsePresetSingleRequest] Buffer too short for preset single request. Length={Length}", DataRaw.Length);
                    return;
                }

                var payloadLength = DataRaw.Length - DATA_HEADER_LENGTH - CRC_LENGTH;
                if (payloadLength < 4)
                {
                    Logger?.LogWarning("[GrowattModbusMessage.ParsePresetSingleRequest] Payload too short to contain register data.");
                    return;
                }

                var payload = new byte[payloadLength];
                Array.Copy(DataRaw, DATA_HEADER_LENGTH, payload, 0, payloadLength);

                var registerNo = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(0, 2));
                var rawValue = payload.Skip(2).ToArray();

                RegisterBlocks.Clear();
                RegisterBlocks.Add(new GrowattModbusBlock
                {
                    Start = registerNo,
                    End = registerNo,
                    Values = rawValue
                });

                RegisterStrings = $"{registerNo}={BitConverter.ToString(rawValue)}";
                Logger?.LogTrace("[GrowattModbusMessage.ParsePresetSingleRequest] Parsed register {RegisterNo} with value length {ValueLength}", registerNo, rawValue.Length);
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "[GrowattModbusMessage.ParsePresetSingleRequest] Failed to parse preset single request: {Message}", ex.Message);
            }
        }

        /// <summary>
        /// Builds a Modbus message for writing multiple registers.
        /// </summary>
        /// <param name="growattModbusBlock">The Modbus block containing the register data.</param>
        /// <returns>The constructed Modbus message as a byte array.</returns>
        public byte[] BuildMultiple(GrowattModbusBlock growattModbusBlock)
        {
            try
            {
                Logger?.LogTrace("[GrowattModbusMessage.BuildMultiple] Building Modbus message for multiple registers.");

                byte[] deviceIdBytes = Encoding.ASCII.GetBytes(DeviceId.PadRight(DEVICE_ID_LENGTH, '\0'));
                ushort msgLen = (ushort)(DEVICE_ID_LENGTH + 4 + growattModbusBlock.Values.Length);

                byte[] result = new byte[42 + growattModbusBlock.Values.Length];
                result[0] = 0;
                result[1] = 1; // unknown = 1
                result[2] = 0;
                result[3] = 7; // constant 7
                result[4] = (byte)(msgLen >> 8);
                result[5] = (byte)(msgLen & 0xFF);
                result[6] = 1; // constant 1
                result[7] = (byte)DataHeaderFunction;
                Array.Copy(deviceIdBytes, 0, result, 8, DEVICE_ID_LENGTH);
                result[38] = (byte)(growattModbusBlock.Start >> 8);
                result[39] = (byte)(growattModbusBlock.Start & 0xFF);
                result[40] = (byte)(growattModbusBlock.End >> 8);
                result[41] = (byte)(growattModbusBlock.End & 0xFF);
                Array.Copy(growattModbusBlock.Values, 0, result, 42, growattModbusBlock.Values.Length);

                Logger?.LogTrace("[GrowattModbusMessage.BuildMultiple] Built Modbus message successfully.");
                return result;
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "[GrowattModbusMessage.BuildMultiple] Failed to build Modbus message: {Message}", ex.Message);
                throw;
            }
        }

        #endregion Private Methods
    }
}
