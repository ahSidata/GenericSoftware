using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;

namespace EnergyAutomate.Emulator
{
    /// <summary>
    /// Parses Growatt MQTT messages for Modbus communication and builds Modbus command payloads.
    /// </summary>
    public class GrowattModbusMqttParser
    {
        private readonly ILogger<GrowattModbusMqttParser> _logger;

        public GrowattModbusMqttParser(ILogger<GrowattModbusMqttParser> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Unscrambles a Growatt MQTT payload using the XOR mask "Growatt".
        /// </summary>
        /// <param name="payload">The scrambled MQTT payload.</param>
        /// <returns>The unscrambled payload as byte array.</returns>
        public byte[] Unscramble(byte[] payload)
        {
            _logger.LogTrace("Unscramble: Start unscrambling payload of length {Length}", payload?.Length ?? 0);

            if (payload == null || payload.Length < 8)
            {
                _logger.LogWarning("Unscramble: Payload is null or too short.");
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

            _logger.LogTrace("Unscramble: Finished unscrambling. Result: {Hex}", BitConverter.ToString(unscrambled));
            return unscrambled;
        }

        /// <summary>
        /// Scrambles a Growatt Modbus payload using the XOR mask "Growatt".
        /// </summary>
        /// <param name="payload">The unscrambled payload.</param>
        /// <returns>The scrambled payload as byte array.</returns>
        public byte[] Scramble(byte[] payload)
        {
            _logger.LogTrace("Scramble: Start scrambling payload of length {Length}", payload?.Length ?? 0);

            if (payload == null || payload.Length < 8)
            {
                _logger.LogWarning("Scramble: Payload is null or too short.");
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

            _logger.LogTrace("Scramble: Finished scrambling. Result: {Hex}", BitConverter.ToString(scrambled));
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
            _logger.LogTrace("AppendCrc: Calculated CRC16={Crc:X4} for payload length {Length}", crc, payload.Length);

            byte[] result = new byte[payload.Length + 2];
            Array.Copy(payload, result, payload.Length);
            // CRC is appended big-endian (network order)
            result[result.Length - 2] = (byte)((crc >> 8) & 0xFF);
            result[result.Length - 1] = (byte)(crc & 0xFF);

            _logger.LogTrace("AppendCrc: Payload with CRC: {Hex}", BitConverter.ToString(result));
            return result;
        }

        /// <summary>
        /// Parses a Modbus message from a Growatt MQTT payload.
        /// </summary>
        /// <param name="payload">The MQTT payload (scrambled or unscrambled).</param>
        /// <param name="isScrambled">Set to true if the payload is scrambled.</param>
        /// <returns>Parsed ModbusMessage or null if parsing failed.</returns>
        public ModbusMessage? ParseModbusMessage(byte[] payload, bool isScrambled = true)
        {
            _logger.LogTrace("ParseModbusMessage: Start parsing payload. Scrambled: {IsScrambled}", isScrambled);

            byte[] data = isScrambled ? Unscramble(payload) : payload;

            if (data.Length < 10)
            {
                _logger.LogWarning("ParseModbusMessage: Payload too short for Modbus message.");
                return null;
            }

            try
            {
                // Example: Extract function code and device id (positions may vary by protocol)
                int functionCode = data[7];
                string deviceId = Encoding.ASCII.GetString(data, 8, 12).Trim('\0');

                _logger.LogTrace("ParseModbusMessage: FunctionCode={FunctionCode}, DeviceId={DeviceId}", functionCode, deviceId);

                // Example: Extract data section (positions/protocol may need adjustment)
                int dataStart = 20;
                int dataLength = data.Length - dataStart;
                byte[] modbusData = new byte[dataLength];
                Array.Copy(data, dataStart, modbusData, 0, dataLength);

                var message = new ModbusMessage
                {
                    FunctionCode = functionCode,
                    DeviceId = deviceId,
                    Data = modbusData,
                    Raw = data
                };

                _logger.LogTrace("ParseModbusMessage: Parsed ModbusMessage: {Message}", message);
                return message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ParseModbusMessage: Exception while parsing Modbus message.");
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
            _logger.LogTrace("BuildSetRegisterCommand: deviceId={DeviceId}, registerAddress={RegisterAddress}, value={Value}", deviceId, registerAddress, value);

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

            _logger.LogTrace("BuildSetRegisterCommand: Raw packet before scramble/CRC: {Hex}", BitConverter.ToString(packet));

            // Scramble and append CRC
            var scrambled = Scramble(packet);
            var finalPayload = AppendCrc(scrambled);

            _logger.LogTrace("BuildSetRegisterCommand: Final payload (scrambled+CRC): {Hex}", BitConverter.ToString(finalPayload));
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
                        crc = (ushort)((crc << 1) ^ polynomial);
                    else
                        crc <<= 1;
                }
            }
            return crc;
        }

    }

    public class NoahModbusParser
    {

        public Dictionary<string, object> Parse(ModbusMessage message)
        {
            var payload = message.Data;
            var result = new Dictionary<string, object>();

            result = ParseLastData(payload);
            // Alternativ: result = ParseInfoData(payload);

            return result;
        }

        public Dictionary<string, object> ParseLastData(byte[] payload)
        {
            var result = new Dictionary<string, object>();

            void AddFloat(string key, int byteOffset, float multiplier = 1.0f, float delta = 0.0f)
            {
                if (byteOffset + 4 <= payload.Length)
                {
                    float raw = BitConverter.ToSingle(payload, byteOffset);
                    result[key] = raw * multiplier + delta;
                }
            }

            void AddUInt16(string key, int byteOffset)
            {
                if (byteOffset + 2 <= payload.Length)
                {
                    ushort val = BitConverter.ToUInt16(payload, byteOffset);
                    result[key] = val;
                }
            }

            void AddString(string key, int byteOffset, int length)
            {
                if (byteOffset + length <= payload.Length)
                {
                    string str = Encoding.ASCII.GetString(payload, byteOffset, length).TrimEnd('\0');
                    result[key] = str;
                }
            }

            // Register-Mapping gemäß JSON für LastData
            AddFloat("out_power", 4);
            AddFloat("pv_tot_power", 14);
            AddUInt16("priority_mode", 16);
            AddUInt16("bat_sysstate", 20);
            AddFloat("charging_discharging", 22, 1.0f, -30000.0f);
            AddFloat("pv1_voltage", 26);
            AddFloat("pv1_current", 30);
            AddFloat("pv1_temp", 34);
            AddFloat("pv2_voltage", 38);
            AddFloat("pv2_current", 42);
            AddFloat("pv2_temp", 46);
            AddFloat("pv3_voltage", 50);
            AddFloat("pv3_current", 54);
            AddFloat("pv3_temp", 58);
            AddFloat("pv4_voltage", 62);
            AddFloat("pv4_current", 66);
            AddFloat("pv4_temp", 70);
            AddFloat("system_temp", 74);
            AddFloat("ambient_temp", 78);
            AddUInt16("total_bat_charge_power", 82);
            AddUInt16("total_bat_discharge_power", 84);
            AddFloat("total_house_load", 86);
            AddUInt16("bat1_soc_pct", 90);
            AddUInt16("bat2_soc_pct", 92);
            AddUInt16("bat3_soc_pct", 94);
            AddUInt16("bat4_soc_pct", 96);
            AddUInt16("bat1_warn_status", 98);
            AddUInt16("bat1_protect_status", 100);
            AddUInt16("bat2_warn_status", 102);
            AddUInt16("bat2_protect_status", 104);
            AddUInt16("bat3_warn_status", 106);
            AddUInt16("bat3_protect_status", 108);
            AddUInt16("bat4_warn_status", 110);
            AddUInt16("bat4_protect_status", 112);
            AddUInt16("bat_cycles", 114);
            AddUInt16("bat_pack_count", 116);
            AddUInt16("bat_soh", 118);
            AddUInt16("charge_soc_limit", 120);
            AddUInt16("discharge_soc_limit", 122);
            AddFloat("eac_today", 124);
            AddFloat("eac_month", 128);
            AddFloat("eac_year", 132);
            AddFloat("eac_total", 136);
            AddUInt16("mppt_protect_status", 140);
            AddUInt16("ac_couple_warn_status", 142);
            AddUInt16("ac_couple_protect_status", 144);
            AddUInt16("ct_flag", 146);
            AddFloat("ct_self_power", 148);
            AddUInt16("on_off_grid", 152);
            AddUInt16("work_mode", 154);
            AddUInt16("pd_warn_status", 156);
            AddUInt16("fault_status", 158);
            AddUInt16("heating_status", 160);

            return result;
        }

        public Dictionary<string, object> ParseInfoData(byte[] payload)
        {
            var result = new Dictionary<string, object>();
            // Placeholder für spätere Implementierung falls Info-Register verfügbar werden
            // Aktuell nicht in JSON-Mapping enthalten
            return result;
        }
    }

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