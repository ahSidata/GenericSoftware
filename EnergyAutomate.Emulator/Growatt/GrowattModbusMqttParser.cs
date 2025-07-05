using EnergyAutomate.Emulator.Growatt.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text;

namespace EnergyAutomate.Emulator.Growatt
{
    /// <summary>
    /// Parses Growatt MQTT messages for Modbus communication and builds Modbus command payloads.
    /// </summary>
    public class GrowattMqttParser
    {
        private IServiceProvider _serviceProvider;

        private ILogger<GrowattMqttParser> GrowattMqttParserLogger => _serviceProvider.GetRequiredService<ILogger<GrowattMqttParser>>();

        private ILogger<GrowattNoahParser> GrowattNoahParserLogger => _serviceProvider.GetRequiredService<ILogger<GrowattNoahParser>>();

        private GrowattNoahParser GrowattNoahModbusParser => _serviceProvider.GetRequiredService<GrowattNoahParser>();

        public GrowattMqttParser(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
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
                GrowattMqttParserLogger.LogWarning("Unscramble: Payload is null or too short.");
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
        /// Scrambles a Growatt Modbus payload using the XOR mask "Growatt".
        /// </summary>
        /// <param name="payload">The unscrambled payload.</param>
        /// <returns>The scrambled payload as byte array.</returns>
        public byte[] Scramble(byte[] payload)
        {
            if (payload == null || payload.Length < 8)
            {
                GrowattMqttParserLogger.LogWarning("Scramble: Payload is null or too short.");
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
        /// Appends a CRC16-Modbus checksum to the payload.
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
        /// Parses a Modbus message from a Growatt MQTT payload and logs details in a separate category, including the topic.
        /// </summary>
        /// <param name="payload">The MQTT payload (scrambled or unscrambled).</param>
        /// <param name="topic">The MQTT topic.</param>
        /// <param name="isScrambled">Set to true if the payload is scrambled.</param>
        /// <returns>Parsed ModbusMessage or null if parsing failed.</returns>
        public GrowattModbusMessage? ParseModbusMessage(byte[] payload, string topic)
        {
            try
            {
                string resultString = string.Empty;

                byte[] data = Unscramble(payload);

                var message = new GrowattModbusMessage(data);

                var growattNoahParser = _serviceProvider.GetRequiredService<GrowattNoahParser>();

                if (message != null)
                {
                    var growattRegister = GrowattRegisterModel.SeedDefaults();

                    if (message.Function == GrowattModbusFunction.READ_HOLDING_REGISTER)
                    {
                        var result = growattNoahParser.ParseRegisters(message, growattRegister.HoldingRegisters);
                        resultString = string.Join(", ", result.Select(kv => $"{kv.Key}={kv.Value}"));
                    }

                    if (message.Function == GrowattModbusFunction.READ_INPUT_REGISTER)
                    {
                        var result = growattNoahParser.ParseRegisters(message, growattRegister.InputRegisters);
                        resultString = string.Join(", ", result.Select(kv => $"{kv.Key}={kv.Value}"));
                    }

                    // Log including startRegister and registerCount
                    GrowattMqttParserLogger.LogInformation(
                        "Topic={Topic}, FunctionCode={FunctionCode}, DeviceId={DeviceId}Raw={RawData}",
                        topic,
                        message.Function.ToString(),
                        message.DeviceId + Environment.NewLine,
                        BitConverter.ToString(data)
                    );

                    GrowattNoahParserLogger.LogInformation(
                        "Topic={Topic}, FunctionCode={FunctionCode}, Register={Register}",
                        topic,
                        message.Function.ToString(),
                        resultString
                    );
                }

                return message;
            }
            catch (Exception ex)
            {
                GrowattMqttParserLogger.LogError(ex, "ParseModbusMessage Exception: {Message}", ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Builds a Modbus command payload for setting a register value (e.g. power).
        /// </summary>
        /// <param name="deviceId">The device ID (12 ASCII chars).</param>
        /// <param name="registerAddress">The Modbus register address to write.</param>
        /// <param name="value">The value to write (16-bit unsigned).</param>
        /// <returns>Scrambled and CRC-appended payload ready for MQTT publish.</returns>
        public byte[] BuildSetRegisterCommand(string deviceId, ushort registerAddress, ushort value)
        {
            GrowattMqttParserLogger.LogTrace("BuildSetRegisterCommand: deviceId={DeviceId}, registerAddress={RegisterAddress}, value={Value}", deviceId, registerAddress, value);

            // Build the Modbus frame according to Growatt protocol
            // Header: 8 bytes (example: 00 01 00 07 00 06 01 06)
            // 01 06 = Modbus function code 0x06 (Write Single Register)
            // Device ID: 12 bytes ASCII, padded with 0x00
            // Register address: 2 bytes big-endian
            // Value: 2 bytes big-endian

            byte[] header = new byte[] { 0x00, 0x01, 0x00, 0x07, 0x00, 0x06, 0x01, 0x06 };
            byte[] deviceIdBytes = new byte[12];
            Encoding.ASCII.GetBytes(deviceId.PadRight(12, '\0')).CopyTo(deviceIdBytes, 0);
            byte[] registerBytes = BitConverter.GetBytes(registerAddress);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(registerBytes);
            byte[] valueBytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(valueBytes);

            // Compose the packet
            byte[] packet = new byte[header.Length + deviceIdBytes.Length + registerBytes.Length + valueBytes.Length];
            int offset = 0;
            Array.Copy(header, 0, packet, offset, header.Length); offset += header.Length;
            Array.Copy(deviceIdBytes, 0, packet, offset, deviceIdBytes.Length); offset += deviceIdBytes.Length;
            Array.Copy(registerBytes, 0, packet, offset, registerBytes.Length); offset += registerBytes.Length;
            Array.Copy(valueBytes, 0, packet, offset, valueBytes.Length);

            // Scramble and append CRC
            var scrambled = Scramble(packet);
            var finalPayload = AppendCrc(scrambled);

            return finalPayload;
        }

        /// <summary>
        /// Calculates CRC16-Modbus checksum for a byte array.
        /// </summary>
        /// <param name="data">The data to calculate CRC for.</param>
        /// <returns>CRC16-Modbus checksum.</returns>
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
    }
}