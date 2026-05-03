using EnergyAutomate.Emulator.Growatt.Models;
using Microsoft.Extensions.Logging;
using System.Buffers.Binary;
using System.Text;

namespace EnergyAutomate.Emulator.Growatt
{
    /// <summary>
    /// Central codec for Growatt ModbusCodec message encoding and decoding.
    /// Handles scrambling, CRC calculation, frame construction, and message parsing.
    /// All ModbusCodec protocol logic is centralized here.
    /// </summary>
    public class GrowattModbusCodec
    {
        private const int DATA_HEADER_LENGTH = 8;
        private const int CRC_LENGTH = 2;
        private const int DEVICE_ID_LENGTH = 30;
        private const int DEVICE_ID_OFFSET = 8;
        private const int FUNCTION_CODE_OFFSET = 7;
        private const int MSG_LEN_OFFSET = 4;
        private const int MSG_LEN_LENGTH = 2;
        private const int MIN_MESSAGE_LENGTH = 38;
        private const string DumpDirectoryEnvironmentVariable = "DUMP_DIR";

        private readonly ILogger<GrowattModbusCodec> _logger;
        private readonly GrowattRegisterModel _growattRegister = GrowattRegisterModel.SeedDefaults();

        public GrowattModbusCodec(ILogger<GrowattModbusCodec> logger)
        {
            _logger = logger;
        }

        #region Block Building

        /// <summary>
        /// Builds a raw ModbusCodec frame from a GrowattModbusBlock.
        /// </summary>
        /// <param name="block">The ModbusCodec block to encode.</param>
        /// <returns>The raw ModbusCodec frame as a byte array.</returns>
        public byte[] BuildFrame(GrowattModbusBlock block)
        {
            const ushort defaultTransactionId = 0x0001;
            const ushort growattProtocolId = 0x0007;
            const byte defaultUnitId = 0x01;

            var messageLength = (ushort)(4 + block.Values.Length);

            byte[] result = new byte[12 + block.Values.Length];
            result[0] = (byte)(defaultTransactionId >> 8);
            result[1] = (byte)(defaultTransactionId & 0xFF);
            result[2] = (byte)(growattProtocolId >> 8);
            result[3] = (byte)(growattProtocolId & 0xFF);
            result[4] = (byte)(messageLength >> 8);
            result[5] = (byte)(messageLength & 0xFF);
            result[6] = defaultUnitId;
            result[7] = (byte)block.Function;
            result[8] = (byte)(block.Start >> 8);
            result[9] = (byte)(block.Start & 0xFF);
            result[10] = (byte)(block.End >> 8);
            result[11] = (byte)(block.End & 0xFF);
            Array.Copy(block.Values, 0, result, 12, block.Values.Length);

            return result;
        }

        /// <summary>
        /// Parses a block from raw payload bytes.
        /// </summary>
        /// <param name="buffer">The raw payload buffer.</param>
        /// <returns>Parsed GrowattModbusBlock or null if parsing failed.</returns>
        public GrowattModbusBlock? ParseBlock(byte[] buffer)
        {
            try
            {
                if (buffer.Length < 4)
                {
                    _logger?.LogWarning("[ParseBlock] Buffer too short. Length={Length}", buffer.Length);
                    return null;
                }

                ushort start = (ushort)(buffer[0] << 8 | buffer[1]);
                ushort end = (ushort)(buffer[2] << 8 | buffer[3]);
                int numBlocks = end - start + 1;
                int valuesLength = numBlocks * 2;

                if (buffer.Length < 4 + valuesLength)
                {
                    _logger?.LogWarning("[ParseBlock] Buffer too short for values. ExpectedLength={ExpectedLength}, ActualLength={ActualLength}", 
                        4 + valuesLength, buffer.Length);
                    return null;
                }

                var values = new byte[valuesLength];
                Array.Copy(buffer, 4, values, 0, valuesLength);

                _logger?.LogTrace("[ParseBlock] Successfully parsed block. Start={Start}, End={End}, ValuesLength={ValuesLength}", 
                    start, end, values.Length);

                return new GrowattModbusBlock 
                { 
                    Start = start, 
                    End = end, 
                    Values = values 
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[ParseBlock] Parsing failed: {Message}", ex.Message);
                return null;
            }
        }

        #endregion Block Building

        #region MQTT Encoding (Scramble, CRC)

        /// <summary>
        /// Scrambles a Growatt ModbusCodec payload using the XOR mask "Growatt".
        /// </summary>
        /// <param name="payload">The unscrambled payload.</param>
        /// <returns>The scrambled payload as byte array.</returns>
        public byte[] Scramble(byte[] payload)
        {
            if (payload == null || payload.Length < 8)
            {
                _logger?.LogWarning("Scramble: Payload is null or too short.");
                return payload ?? Array.Empty<byte>();
            }

            byte[] mask = Encoding.ASCII.GetBytes("Growatt");
            int nmask = mask.Length;
            int ndecdata = payload.Length;

            // Preserve the 8-byte header
            byte[] scrambled = new byte[ndecdata];
            Array.Copy(payload, 0, scrambled, 0, 8);

            for (int i = 0; i < ndecdata - 8; i++)
            {
                scrambled[i + 8] = (byte)(payload[i + 8] ^ mask[i % nmask]);
            }

            return scrambled;
        }

        /// <summary>
        /// Unscrambles a Growatt MQTT payload using the XOR mask "Growatt".
        /// </summary>
        /// <param name="payload">The scrambled MQTT payload.</param>
        /// <returns>The unscrambled payload as byte array.</returns>
        public byte[] Unscramble(byte[] payload)
        {
            if (payload == null || payload.Length < 8)
            {
                _logger?.LogWarning("Unscramble: Payload is null or too short.");
                return payload ?? Array.Empty<byte>();
            }

            byte[] mask = Encoding.ASCII.GetBytes("Growatt");
            int nmask = mask.Length;
            int ndecdata = payload.Length;

            // Preserve the 8-byte header
            byte[] unscrambled = new byte[ndecdata];
            Array.Copy(payload, 0, unscrambled, 0, 8);

            for (int i = 0; i < ndecdata - 8; i++)
            {
                unscrambled[i + 8] = (byte)(payload[i + 8] ^ mask[i % nmask]);
            }

            return unscrambled;
        }

        /// <summary>
        /// Appends a CRC16-ModbusCodec checksum to the payload.
        /// </summary>
        /// <param name="payload">The payload to append CRC to.</param>
        /// <returns>Payload with CRC16 appended.</returns>
        public byte[] AppendCrc(byte[] payload)
        {
            ushort crc = Crc16Modbus(payload);

            byte[] result = new byte[payload.Length + 2];
            Array.Copy(payload, result, payload.Length);
            // CRC is appended big-endian (network order)
            result[result.Length - 2] = (byte)(crc >> 8 & 0xFF);
            result[result.Length - 1] = (byte)(crc & 0xFF);

            return result;
        }

        /// <summary>
        /// Builds a complete MQTT-ready payload from a GrowattModbusBlock.
        /// Includes frame construction, scrambling, and CRC.
        /// </summary>
        /// <param name="block">The ModbusCodec block to encode.</param>
        /// <returns>Scrambled and CRC-appended payload ready for MQTT publish.</returns>
        public byte[] BuildForMqtt(GrowattModbusBlock block)
        {
            var rawFrame = BuildFrame(block);
            var scrambled = Scramble(rawFrame);
            var finalPayload = AppendCrc(scrambled);
            return finalPayload;
        }

        /// <summary>
        /// Builds a Modbus command payload for setting multiple register values.
        /// </summary>
        /// <param name="startRegister">The starting Modbus register address to write.</param>
        /// <param name="values">The values to write (array of 16-bit unsigned integers).</param>
        /// <returns>Scrambled and CRC-appended payload ready for MQTT publish.</returns>
        public byte[] BuildSetMultipleRegistersCommand(ushort startRegister, ushort[] values)
        {
            _logger?.LogTrace("BuildSetMultipleRegistersCommand: startRegister={StartRegister}, values=[{Values}]", 
                startRegister, string.Join(", ", values));

            var valueBytes = new List<byte>();
            foreach (var value in values)
            {
                var bytes = BitConverter.GetBytes(value);
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(bytes);
                }
                valueBytes.AddRange(bytes);
            }

            var block = new GrowattModbusBlock
            {
                Start = startRegister,
                End = (ushort)(startRegister + values.Length - 1),
                Values = valueBytes.ToArray(),
                Function = GrowattModbusFunction.PRESET_MULTIPLE_REGISTER
            };

            var finalPayload = BuildForMqtt(block);

            _logger?.LogTrace("BuildSetMultipleRegistersCommand: finalPayload={FinalPayload}", BitConverter.ToString(finalPayload));

            return finalPayload;
        }

        /// <summary>
        /// Builds a Modbus command payload for setting a single register value.
        /// </summary>
        /// <param name="registerAddress">The Modbus register address to write.</param>
        /// <param name="value">The value to write (16-bit unsigned).</param>
        /// <returns>Scrambled and CRC-appended payload ready for MQTT publish.</returns>
        public byte[] BuildSetSingleRegisterCommand(ushort registerAddress, ushort value)
        {
            _logger?.LogTrace("BuildSetSingleRegisterCommand: registerAddress={RegisterAddress}, value={Value}", registerAddress, value);

            var block = new GrowattModbusBlock
            {
                Start = registerAddress,
                End = registerAddress,
                Values = [(byte)(value >> 8), (byte)(value & 0xFF)],
                Function = GrowattModbusFunction.PRESET_SINGLE_REGISTER
            };

            var finalPayload = BuildForMqtt(block);

            _logger?.LogTrace("BuildSetSingleRegisterCommand: finalPayload={FinalPayload}", BitConverter.ToString(finalPayload));

            return finalPayload;
        }

        /// <summary>
        /// Parses a ModbusCodec message from a Growatt MQTT payload (unscrambled).
        /// </summary>
        /// <param name="payload">The MQTT payload (should be unscrambled and CRC removed).</param>
        /// <param name="topic">The MQTT topic.</param>
        /// <returns>Parsed ModbusMessage or null if parsing failed.</returns>
        public GrowattModbusMessage? ParseModbusMessage(byte[] payload, string topic)
        {
            try
            {
                if (payload.Length < MIN_MESSAGE_LENGTH)
                {
                    _logger?.LogWarning("[ParseModbusMessage] Buffer too short for header. Length={Length}", payload.Length);
                    return null;
                }

                var message = new GrowattModbusMessage 
                { 
                    Topic = topic,
                    DataRaw = payload
                };

                DataHeaderParse(message);
                ProcessFunctionData(message);
                Dump(message);

                return message;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[ParseModbusMessage] Exception: {Message}", ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Parses a ModbusCodec message from a Growatt MQTT payload (scrambled).
        /// </summary>
        /// <param name="payload">The MQTT payload (scrambled).</param>
        /// <param name="topic">The MQTT topic.</param>
        /// <returns>Parsed ModbusMessage or null if parsing failed.</returns>
        public GrowattModbusMessage? ParseModbusMessageFromMqtt(byte[] payload, string topic)
        {
            try
            {
                byte[] data = Unscramble(payload);
                var message = ParseModbusMessage(data, topic);

                if (message != null)
                {
                    _logger?.LogInformation(
                        "Topic={Topic}, FunctionCode={FunctionCode}, DeviceId={DeviceId}, Raw={RawData}",
                        topic,
                        message.DataHeaderFunction.ToString(),
                        message.DeviceId,
                        BitConverter.ToString(data)
                    );

                    _logger?.LogInformation(
                        "Topic={Topic}, FunctionCode={FunctionCode}, Register={Register}",
                        topic,
                        message.DataHeaderFunction.ToString(),
                        message.RegisterStrings
                    );
                }

                return message;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[ParseModbusMessageFromMqtt] Exception: {Message}", ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Calculates CRC16-ModbusCodec checksum for a byte array.
        /// </summary>
        /// <param name="data">The data to calculate CRC for.</param>
        /// <returns>CRC16-ModbusCodec checksum.</returns>
        private static ushort Crc16Modbus(byte[] data)
        {
            const ushort polynomial = 0x8005;
            ushort crc = 0xFFFF;

            for (int i = 0; i < data.Length; i++)
            {
                crc ^= (ushort)(data[i] << 8);
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 0x8000) != 0)
                        crc = (ushort)(crc << 1 ^ polynomial);
                    else
                        crc <<= 1;
                }
            }
            return crc;
        }

        #endregion MQTT Encoding (Scramble, CRC)

        #region Message Parsing

        #endregion Message Parsing

        #region Internal Parsing Helpers

        /// <summary>
        /// Parses the data header of a ModbusCodec message.
        /// </summary>
        private void DataHeaderParse(GrowattModbusMessage message)
        {
            try
            {
                var dataRaw = message.DataRaw;

                var msgLen = BinaryPrimitives.ReadUInt16BigEndian(dataRaw.AsSpan(MSG_LEN_OFFSET, MSG_LEN_LENGTH));
                if (msgLen != dataRaw.Length - DATA_HEADER_LENGTH)
                {
                    _logger?.LogWarning("[DataHeaderParse] msgLen mismatch. msgLen={MsgLen}, buffer.Length-8={ActualLength}",
                        msgLen, dataRaw.Length - DATA_HEADER_LENGTH);
                }

                byte function = dataRaw[FUNCTION_CODE_OFFSET];
                message.DataHeaderFunction = (GrowattModbusFunction)function;
                message.DeviceId = Encoding.ASCII.GetString(dataRaw, DEVICE_ID_OFFSET, DEVICE_ID_LENGTH).Trim('\0');

                _logger?.LogTrace("[DataHeaderParse] Parsed header. Function={Function}, DeviceId={DeviceId}", 
                    message.DataHeaderFunction, message.DeviceId);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[DataHeaderParse] Failed to parse header: {Message}", ex.Message);
            }
        }

        /// <summary>
        /// Processes function-specific data of the ModbusCodec message.
        /// </summary>
        private void ProcessFunctionData(GrowattModbusMessage message)
        {
            try
            {
                switch (message.DataHeaderFunction)
                {
                    case GrowattModbusFunction.READ_INPUT_REGISTER:
                    case GrowattModbusFunction.READ_HOLDING_REGISTER:
                        ParseReadResponse(message);
                        break;

                    case GrowattModbusFunction.PRESET_MULTIPLE_REGISTER:
                        ParsePresetMultipleRequest(message);
                        break;

                    case GrowattModbusFunction.PRESET_SINGLE_REGISTER:
                        ParsePresetSingleRequest(message);
                        break;

                    default:
                        _logger?.LogWarning("[ProcessFunctionData] No parsing logic implemented for function {Function}", 
                            message.DataHeaderFunction);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[ProcessFunctionData] Failed to process function data: {Message}", ex.Message);
            }
        }

        /// <summary>
        /// Parses the response for read operations.
        /// </summary>
        private void ParseReadResponse(GrowattModbusMessage message)
        {
            _logger?.LogTrace("[ParseReadResponse] Parsing read response.");

            try
            {
                if (message.DataRaw.Length <= DATA_HEADER_LENGTH + CRC_LENGTH)
                {
                    _logger?.LogWarning("[ParseReadResponse] Buffer too short for read response payload. Length={Length}", 
                        message.DataRaw.Length);
                    return;
                }

                var payloadOffset = DATA_HEADER_LENGTH;
                var payloadLength = message.DataRaw.Length - DATA_HEADER_LENGTH - CRC_LENGTH;
                if (payloadLength <= 0)
                {
                    _logger?.LogWarning("[ParseReadResponse] No payload available for parsing.");
                    return;
                }

                var payload = new byte[payloadLength];
                Array.Copy(message.DataRaw, payloadOffset, payload, 0, payloadLength);

                var registerMap = message.DataHeaderFunction == GrowattModbusFunction.READ_HOLDING_REGISTER
                    ? _growattRegister.HoldingRegisters
                    : _growattRegister.InputRegisters;

                if (registerMap.Count == 0)
                {
                    _logger?.LogWarning("[ParseReadResponse] No register definitions available for function {Function}.", 
                        message.DataHeaderFunction);
                    return;
                }

                message.RegisterBlocks.Clear();
                message.RegisterStrings = string.Empty;

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
                        _logger?.LogWarning(
                            "[ParseReadResponse] Register {RegisterNo} is out of range. StartIndex={StartIndex}, Size={Size}, PayloadLength={PayloadLength}",
                            register.Position.RegisterNo, startIndex, size, payload.Length);
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
                    message.RegisterBlocks.Add(new GrowattModbusBlock
                    {
                        Start = (ushort)register.Position.RegisterNo,
                        End = (ushort)register.Position.RegisterNo,
                        Values = raw
                    });
                }

                message.RegisterStrings = string.Join(", ", registerValues);
                _logger?.LogTrace("[ParseReadResponse] Parsed {Count} register values.", registerValues.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[ParseReadResponse] Failed to parse read response: {Message}", ex.Message);
            }
        }

        /// <summary>
        /// Parses the request for presetting multiple registers.
        /// </summary>
        private void ParsePresetMultipleRequest(GrowattModbusMessage message)
        {
            _logger?.LogTrace("[ParsePresetMultipleRequest] Parsing preset multiple request.");

            try
            {
                if (message.DataRaw.Length <= DATA_HEADER_LENGTH + CRC_LENGTH)
                {
                    _logger?.LogWarning("[ParsePresetMultipleRequest] Buffer too short for preset multiple request. Length={Length}", 
                        message.DataRaw.Length);
                    return;
                }

                var payloadLength = message.DataRaw.Length - DATA_HEADER_LENGTH - CRC_LENGTH;
                if (payloadLength < 4)
                {
                    _logger?.LogWarning("[ParsePresetMultipleRequest] Payload too short to contain register block.");
                    return;
                }

                var payload = new byte[payloadLength];
                Array.Copy(message.DataRaw, DATA_HEADER_LENGTH, payload, 0, payloadLength);

                var block = ParseBlock(payload);
                if (block == null)
                {
                    return;
                }

                message.RegisterBlocks.Clear();
                message.RegisterBlocks.Add(block);

                message.RegisterStrings = $"{block.Start}-{block.End}={BitConverter.ToString(block.Values)}";
                _logger?.LogTrace("[ParsePresetMultipleRequest] Parsed block Start={Start}, End={End}, ValuesLength={ValuesLength}", 
                    block.Start, block.End, block.Values.Length);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[ParsePresetMultipleRequest] Failed to parse preset multiple request: {Message}", ex.Message);
            }
        }

        /// <summary>
        /// Parses the request for presetting a single register.
        /// </summary>
        private void ParsePresetSingleRequest(GrowattModbusMessage message)
        {
            _logger?.LogTrace("[ParsePresetSingleRequest] Parsing preset single request.");

            try
            {
                if (message.DataRaw.Length <= DATA_HEADER_LENGTH + CRC_LENGTH)
                {
                    _logger?.LogWarning("[ParsePresetSingleRequest] Buffer too short for preset single request. Length={Length}", 
                        message.DataRaw.Length);
                    return;
                }

                var payloadLength = message.DataRaw.Length - DATA_HEADER_LENGTH - CRC_LENGTH;
                if (payloadLength < 4)
                {
                    _logger?.LogWarning("[ParsePresetSingleRequest] Payload too short to contain register data.");
                    return;
                }

                var payload = new byte[payloadLength];
                Array.Copy(message.DataRaw, DATA_HEADER_LENGTH, payload, 0, payloadLength);

                var registerNo = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(0, 2));
                var rawValue = payload.Skip(2).ToArray();

                message.RegisterBlocks.Clear();
                message.RegisterBlocks.Add(new GrowattModbusBlock
                {
                    Start = registerNo,
                    End = registerNo,
                    Values = rawValue
                });

                message.RegisterStrings = $"{registerNo}={BitConverter.ToString(rawValue)}";
                _logger?.LogTrace("[ParsePresetSingleRequest] Parsed register {RegisterNo} with value length {ValueLength}", 
                    registerNo, rawValue.Length);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[ParsePresetSingleRequest] Failed to parse preset single request: {Message}", ex.Message);
            }
        }

        /// <summary>
        /// Dumps the parsed ModbusCodec message to a file for debugging purposes.
        /// </summary>
        private void Dump(GrowattModbusMessage message)
        {
            try
            {
                string timestampPart = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fffffff");
                string topicPart = string.Join("_", message.Topic.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));

                string dumpDirectory = Path.Combine(GetDumpRootDirectory(), "Dump", topicPart, message.DataHeaderFunction.ToString());
                if (!Directory.Exists(dumpDirectory))
                {
                    Directory.CreateDirectory(dumpDirectory);
                    _logger?.LogInformation("[Dump] Created dump directory at {DumpDirectory}", dumpDirectory);
                }

                string fileName = $"{timestampPart}_{topicPart}_{message.DataHeaderFunction.ToString()}_{Guid.NewGuid():N}_Messages.txt";
                string filePath = Path.Combine(dumpDirectory, fileName);

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("=== Parsed Result ===");
                sb.AppendLine($"Function: {(int)message.DataHeaderFunction}");
                sb.AppendLine($"DeviceId: {message.DeviceId}");
                sb.AppendLine($"BlockCount: {message.RegisterBlocks.Count}");
                sb.AppendLine($"RegisterStrings: {message.RegisterStrings}");
                sb.AppendLine();
                sb.AppendLine("=== Raw Data ===");
                sb.AppendLine($"RawData: {BitConverter.ToString(message.DataRaw)}");

                File.WriteAllText(filePath, sb.ToString());

                _logger?.LogTrace("[Dump] Dumped message to {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[Dump] Failed to dump message: {Message}", ex.Message);
            }
        }

        private static string GetDumpRootDirectory()
        {
            var configuredDirectory = Environment.GetEnvironmentVariable(DumpDirectoryEnvironmentVariable);
            return string.IsNullOrWhiteSpace(configuredDirectory) ? AppContext.BaseDirectory : configuredDirectory;
        }

        #endregion Internal Parsing Helpers
    }
}