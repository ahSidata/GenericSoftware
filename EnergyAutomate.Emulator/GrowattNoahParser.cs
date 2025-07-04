using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace EnergyAutomate.Emulator
{

    //| Function Code | Typical Fields / Payload Types                                      |
    //| ------------- | ------------------------------------------------------------------- |
    //| **3**         | Serial number, register block(schedules, config, many fields)       |
    //| **4**         | Serial number, register block(live status/measurements)             |
    //| **6**         | Serial number, single register address & value                      |
    //| **16**        | Serial number, start/end register, sequence of values               |
    //| **25**        | Serial number, ASCII (device info, config strings, plain text data) |

    //READ_HOLDING_REGISTER = 3
    //READ_INPUT_REGISTER = 4
    //READ_SINGLE_REGISTER = 5
    //PRESET_SINGLE_REGISTER = 6
    //PRESET_MULTIPLE_REGISTER = 16


    public class GrowattNoahParser
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<GrowattNoahParser> _logger;

        public GrowattNoahParser(IServiceProvider serviceProvider, ILogger<GrowattNoahParser> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public static readonly Dictionary<int, string> RegisterToFieldName = new Dictionary<int, string>
        {
            { 92, "pv1Voltage" },
            { 93, "pv1Current" },
            { 95, "pv2Voltage" },
            { 96, "pv2Current" },
            { 131, "totalBatteryPackSoc" },
            { 132, "battery1Soc" },
            { 133, "battery2Soc" },
            { 134, "battery3Soc" },
            { 135, "battery4Soc" },
            { 141, "totalBatteryPackChargingPower" },
            { 142, "totalBatteryPackDischargingPower" },
            { 143, "batteryChargingPower" },
            { 144, "batteryDischargingPower" },
            { 145, "batteryChargingCurrent" },
            { 146, "batteryDischargingCurrent" },
            { 147, "batteryBusVoltage" },
            { 148, "batteryBusCurrent" },
            { 149, "batteryVoltage" },
            { 150, "battery1Temp" },
            { 151, "battery2Temp" },
            { 152, "battery3Temp" },
            { 153, "battery4Temp" },
            { 160, "batteryCycles" },
            { 161, "batterySoh" },
            { 162, "battery1CellVoltage" },
            { 163, "battery2CellVoltage" },
            { 164, "battery3CellVoltage" },
            { 165, "battery4CellVoltage" },
            { 170, "battery1Alarm" },
            { 171, "battery2Alarm" },
            { 172, "battery3Alarm" },
            { 173, "battery4Alarm" },
            { 200, "pac" },
            { 201, "ppv" },
            { 202, "gridVoltage" },
            { 203, "gridFrequency" },
            { 204, "outputCurrent" },
            { 210, "maxCellVoltage" },
            { 211, "minCellVoltage" },
            { 212, "batteryMaxTemp" },
            { 213, "batteryMinTemp" },
            { 220, "batteryCommStatus" },
            { 221, "bmsStatus" },
            { 230, "systemMode" },
            { 231, "inverterStatus" },
            { 240, "workTimeTotal" },
            { 241, "workTimeThisYear" },
            { 242, "workTimeThisMonth" },
            { 243, "workTimeToday" },
            { 250, "energyTotal" },
            { 251, "energyThisYear" },
            { 252, "energyThisMonth" },
            { 253, "energyToday" },

            // Alle Zeitfenster/Timer-Slots
            { 254, "slot1_start_time" },
            { 255, "slot1_end_time" },
            { 256, "slot1_mode" },
            { 257, "slot1_power" },
            { 258, "slot1_enabled" },
            { 259, "slot2_start_time" },
            { 260, "slot2_end_time" },
            { 261, "slot2_mode" },
            { 262, "slot2_power" },
            { 263, "slot2_enabled" },
            { 264, "slot3_start_time" },
            { 265, "slot3_end_time" },
            { 266, "slot3_mode" },
            { 267, "slot3_power" },
            { 268, "slot3_enabled" },
            { 269, "slot4_start_time" },
            { 270, "slot4_end_time" },
            { 271, "slot4_mode" },
            { 272, "slot4_power" },
            { 273, "slot4_enabled" },
            { 274, "slot5_start_time" },
            { 275, "slot5_end_time" },
            { 276, "slot5_mode" },
            { 277, "slot5_power" },
            { 278, "slot5_enabled" },
            { 279, "slot6_start_time" },
            { 280, "slot6_end_time" },
            { 281, "slot6_mode" },
            { 282, "slot6_power" },
            { 283, "slot6_enabled" },
            { 284, "slot7_start_time" },
            { 285, "slot7_end_time" },
            { 286, "slot7_mode" },
            { 287, "slot7_power" },
            { 288, "slot7_enabled" },
            { 289, "slot8_start_time" },
            { 290, "slot8_end_time" },
            { 291, "slot8_mode" },
            { 292, "slot8_power" },
            { 293, "slot8_enabled" },
            { 294, "slot9_start_time" },
            { 295, "slot9_end_time" },
            { 296, "slot9_mode" },
            { 297, "slot9_power" },
            { 298, "slot9_enabled" }
        };

        public Dictionary<string, object> Parse(ModbusMessage message)
        {
            var result = new Dictionary<string, object>();

            switch (message.FunctionCode)
            {
                case 3: // READ_HOLDING_REGISTER

                    break;

                case 4: // READ_INPUT_REGISTER

                    int offset = 38;
                    while (offset + 4 <= message.Data.Length)
                    {
                        ushort startReg = BitConverter.ToUInt16(message.Data.Skip(offset).Take(2).Reverse().ToArray(), 0);
                        ushort endReg = BitConverter.ToUInt16(message.Data.Skip(offset + 2).Take(2).Reverse().ToArray(), 0);
                        int regCount = endReg - startReg + 1;
                        offset += 4;
                        for (int i = 0; i < regCount; i++)
                        {
                            if (offset + 2 > message.Data.Length) break;
                            ushort value = BitConverter.ToUInt16(message.Data.Skip(offset).Take(2).Reverse().ToArray(), 0);

                            string key = RegisterToFieldName.TryGetValue(startReg + i, out var name)
                                ? name
                                : $"register_{startReg + i}";

                            result[key] = value;
                            offset += 2;
                        }
                    }
                    break;

                case 6: // WRITE_SINGLE_REGISTER

                    int w6offset = 38;
                    if (message.Data.Length >= w6offset + 4)
                    {
                        ushort reg = BitConverter.ToUInt16(message.Data.Skip(w6offset).Take(2).Reverse().ToArray(), 0);
                        ushort val = BitConverter.ToUInt16(message.Data.Skip(w6offset + 2).Take(2).Reverse().ToArray(), 0);

                        string key = RegisterToFieldName.TryGetValue(reg, out var name)
                            ? name
                            : $"register_{reg}";

                        result[key] = val;
                    }
                    break;

                case 16: // WRITE_MULTIPLE_REGISTERS
                    int w16offset = 38;
                    if (message.Data.Length >= w16offset + 4)
                    {
                        ushort startReg = BitConverter.ToUInt16(message.Data.Skip(w16offset).Take(2).Reverse().ToArray(), 0);
                        ushort endReg = BitConverter.ToUInt16(message.Data.Skip(w16offset + 2).Take(2).Reverse().ToArray(), 0);
                        int regCount = endReg - startReg + 1;
                        int vOffset = w16offset + 4;
                        for (int i = 0; i < regCount; i++)
                        {
                            if (vOffset + 2 > message.Data.Length) break;
                            ushort value = BitConverter.ToUInt16(message.Data.Skip(vOffset).Take(2).Reverse().ToArray(), 0);

                            string key = RegisterToFieldName.TryGetValue(startReg + i, out var name)
                                ? name
                                : $"register_{startReg + i}";

                            result[key] = value;
                            vOffset += 2;
                        }
                    }
                    break;

                case 25: // CUSTOM/ASCII/Manufacturer
                    result["ascii_payload"] = System.Text.Encoding.ASCII.GetString(message.Data);
                    break;

                default:
                    // Fallback: Gib Raw aus
                    result["raw_data"] = BitConverter.ToString(message.Data);
                    break;
            }

            return result;
        }
    }
}