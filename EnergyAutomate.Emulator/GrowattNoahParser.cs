using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EnergyAutomate.Emulator
{
    public class GrowattNoahParser
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<GrowattNoahParser> _logger;

        // Register-Konstanten für eine bessere Wartbarkeit
        private static class Registers
        {
            // System-Register
            public const int OutputPower = 2;
            public const int PvTotalPower = 7;
            public const int PriorityMode = 8;
            public const int BatterySystemState = 10;
            public const int ChargingDischarging = 11;
            public const int BatteryCount = 12;
            public const int TotalBatterySoc = 13;

            // Seriennummern und Identifikation
            public const int SerialPart1 = 21;
            public const int SerialPart2 = 23;
            public const int SerialPart3 = 25;
            public const int SerialPart4 = 27;

            // Batterie-Register
            public const int Battery1Soc = 29;
            public const int Battery2Soc = 41;
            public const int Battery3Soc = 53;
            public const int Battery4Soc = 65;

            // Energie-Register
            public const int PvEnergyToday = 72;
            public const int PvEnergyMonth = 74;
            public const int PvEnergyYear = 76;
            public const int EnergyOutDevice = 78;

            // Lade-/Entlade-Limits
            public const int ChargeLimit = 90;
            public const int DischargeLimit = 91;

            // PV-Register und Temperaturen
            public const int Pv1Voltage = 92;
            public const int Pv1Current = 93;
            public const int Temperature1 = 94;
            public const int Pv2Voltage = 95;
            public const int Pv2Current = 96;
            public const int Temperature2 = 97;

            // Zellen-Register
            public const int MaxCellVoltageBat1 = 99;
            public const int MinCellVoltageBat1 = 100;
            public const int BatteryCycleCount = 101;

            // Weitere Register
            public const int Register102 = 102;
            public const int OutputVoltage = 109;
        }

        // Offsets für Seriennummern
        private static class SerialNumOffsets
        {
            public const int Battery1 = 18;
            public const int Battery2 = 50;
            public const int Battery3 = 82;
            public const int Battery4 = 114;
        }

        private static readonly string ModbusDataFile = Path.Combine(AppContext.BaseDirectory, "dump", "modbus_data.txt");

        public GrowattNoahParser(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _logger = serviceProvider.GetRequiredService<ILogger<GrowattNoahParser>>();
        }

        public Dictionary<string, object> Parse(ModbusMessage message)
        {
            _logger.LogTrace("Parse: Verarbeite ModbusMessage");

            if (message == null)
            {
                _logger.LogWarning("Parse: ModbusMessage ist null");
                return new Dictionary<string, object>();
            }

            return ParseLastData(message.Data);
        }

        public Dictionary<string, object> ParseLastData(byte[] payload)
        {
            var result = new Dictionary<string, object>();

            _logger.LogTrace("ParseLastData: Starte Parsing von Payload mit Länge {Length}", payload?.Length ?? 0);

            const int minPayloadLength = 162;

            if (payload == null)
            {
                _logger.LogWarning("ParseLastData: Payload ist null");
                return result;
            }

            if (payload.Length < minPayloadLength)
            {
                _logger.LogWarning("ParseLastData: Payload zu kurz für vollständiges Parsing (Länge {Length}, benötigt {Required}). Nur verfügbare Felder werden geparst.",
                    payload.Length, minPayloadLength);
            }

            try
            {
                // Hilfs-Funktionen für das Parsing
                ushort GetUInt16(int register) => ReadBigEndianUInt16(payload, register * 2);
                float GetFloat(int register, float multiplier = 1.0f, float delta = 0.0f) => GetUInt16(register) * multiplier + delta;
                string GetString(int offset, int length) => ReadAsciiString(payload, offset, length);
                void Add(string key, object value) => result[key] = value;

                // System-Informationen
                Add("outputPower", GetUInt16(Registers.OutputPower));
                Add("pvTotalPower", GetUInt16(Registers.PvTotalPower));
                Add("priorityMode", GetUInt16(Registers.PriorityMode));
                Add("batterySystemState", GetUInt16(Registers.BatterySystemState));
                Add("chargingPower", GetUInt16(Registers.ChargingDischarging) - 30000);
                Add("batteryCount", GetUInt16(Registers.BatteryCount));
                Add("totalBatterySoc", GetUInt16(Registers.TotalBatterySoc));

                // Seriennummern
                Add("battery1SerialNum", GetString(SerialNumOffsets.Battery1, 16));
                Add("battery2SerialNum", GetString(SerialNumOffsets.Battery2, 16));
                Add("battery3SerialNum", GetString(SerialNumOffsets.Battery3, 16));
                Add("battery4SerialNum", GetString(SerialNumOffsets.Battery4, 16));

                // Batterie-SOC Werte
                Add("battery1Soc", GetUInt16(Registers.Battery1Soc));
                Add("battery2Soc", GetUInt16(Registers.Battery2Soc));
                Add("battery3Soc", GetUInt16(Registers.Battery3Soc));
                Add("battery4Soc", GetUInt16(Registers.Battery4Soc));

                // PV-Werte
                Add("pv1Voltage", GetFloat(Registers.Pv1Voltage, 0.01f));
                Add("pv1Current", GetFloat(Registers.Pv1Current, 0.01f));
                Add("pv2Voltage", GetFloat(Registers.Pv2Voltage, 0.01f));
                Add("pv2Current", GetFloat(Registers.Pv2Current, 0.01f));

                // Energie-Werte
                Add("pvEnergyToday", GetFloat(Registers.PvEnergyToday, 0.1f));
                Add("pvEnergyMonth", GetFloat(Registers.PvEnergyMonth, 0.1f));
                Add("pvEnergyYear", GetFloat(Registers.PvEnergyYear, 0.1f));
                Add("energyOutDevice", GetFloat(Registers.EnergyOutDevice, 0.1f));

                // Ausgangswerte
                Add("outputVoltage", GetFloat(Registers.OutputVoltage, 0.01f));

                // Lade-/Entlade-Limits
                Add("chargeLimit", GetUInt16(Registers.ChargeLimit));
                Add("dischargeLimit", GetUInt16(Registers.DischargeLimit));

                // Temperaturen
                Add("temperature1", GetFloat(Registers.Temperature1, 0.01f));
                Add("temperature2", GetFloat(Registers.Temperature2, 0.01f));

                // Zellenwerte
                Add("maxCellVoltageBat1", GetFloat(Registers.MaxCellVoltageBat1, 0.001f));
                Add("minCellVoltageBat1", GetFloat(Registers.MinCellVoltageBat1, 0.001f));
                Add("batteryCycleCount", GetUInt16(Registers.BatteryCycleCount));

                // Seriennummer-Teile
                Add("serialPart1", GetString(Registers.SerialPart1 * 2, 8));
                Add("serialPart2", GetString(Registers.SerialPart2 * 2, 8));
                Add("serialPart3", GetString(Registers.SerialPart3 * 2, 8));
                Add("serialPart4", GetString(Registers.SerialPart4 * 2, 8));

                // Sonstige Werte
                Add("register102", GetUInt16(Registers.Register102));

                // Modi und Zustände als Text interpretieren
                Add("priorityModeText", GetPriorityModeText(GetUInt16(Registers.PriorityMode)));
                Add("batteryStateText", GetBatteryStateText(GetUInt16(Registers.BatterySystemState)));

                _logger.LogTrace("ParseLastData: Parsing abgeschlossen. Geparste Schlüssel: {Keys}", string.Join(", ", result.Keys));

                // Speichern der Daten in die Datei
                SaveDataToFile(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Parsen der Modbus-Daten");
            }

            return result;
        }

        private void SaveDataToFile(Dictionary<string, object> result)
        {
            try
            {
                var directory = Path.GetDirectoryName(ModbusDataFile);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (!File.Exists(ModbusDataFile))
                {
                    File.AppendAllText(ModbusDataFile, string.Join("\t", result.Keys) + Environment.NewLine);
                }

                File.AppendAllText(ModbusDataFile, string.Join("\t", result.Values) + Environment.NewLine);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Speichern der Modbus-Daten in die Datei {File}", ModbusDataFile);
            }
        }

        private static string GetPriorityModeText(ushort mode)
        {
            return mode switch
            {
                0 => "Laden-Zuerst",
                1 => "Batterie-Zuerst",
                2 => "Netz-Zuerst",
                3 => "Spitzenabdeckung",
                _ => $"Unbekannter Modus ({mode})"
            };
        }

        private static string GetBatteryStateText(ushort state)
        {
            return state switch
            {
                0 => "Standby",
                1 => "Laden",
                2 => "Entladen",
                3 => "Fehler",
                4 => "Vollständig geladen",
                _ => $"Unbekannter Zustand ({state})"
            };
        }

        private static float ReadBigEndianFloat(byte[] data, int offset)
        {
            if (offset + 4 > data.Length) return 0;
            var bytes = data.Skip(offset).Take(4).ToArray();
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            return BitConverter.ToSingle(bytes, 0);
        }

        private static ushort ReadBigEndianUInt16(byte[] data, int offset)
        {
            if (offset + 2 > data.Length)
            {
                return 0;
            }

            // Optimierte Version, die direkt die Bytes aus dem Array liest
            var highByte = data[offset];
            var lowByte = data[offset + 1];
            return (ushort)((highByte << 8) | lowByte);
        }

        private static string ReadAsciiString(byte[] data, int offset, int length)
        {
            if (offset + length > data.Length) return string.Empty;
            var bytes = data.Skip(offset).Take(length).ToArray();
            return Encoding.ASCII.GetString(bytes).Trim('\0');
        }
    }
}