using Microsoft.Extensions.Logging;

namespace EnergyAutomate.Emulator.Growatt.Models
{
    public class GrowattRegisterModel
    {
        public Dictionary<string, GrowattParameter> HoldingRegisters { get; set; } = new();

        public Dictionary<string, GrowattParameter> InputRegisters { get; set; } = new();

        public Dictionary<string, GrowattParameter> PresentRegisters { get; set; } = new();

        public static GrowattRegisterModel SeedDefaults(ILogger logger = null)
        {
            var model = new GrowattRegisterModel();

            #region Hardcoded HoldingRegisters

            model.HoldingRegisters.Add("charge_limit", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 250 },
                    Data = new GrowattData { DataType = GrowattDataType.INT }
                }
            });

            model.HoldingRegisters.Add("default_power", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 252 },
                    Data = new GrowattData { DataType = GrowattDataType.INT }
                }
            });

            model.HoldingRegisters.Add("discharge_limit", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 251 },
                    Data = new GrowattData { DataType = GrowattDataType.INT }
                }
            });

            model.HoldingRegisters.Add("slot1_start_time", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 254, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.TIME_HHMM }
                }
            });

            model.HoldingRegisters.Add("slot1_end_time", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 255, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.TIME_HHMM }
                }
            });

            model.HoldingRegisters.Add("slot1_mode", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 256, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.INT }
                }
            });

            model.HoldingRegisters.Add("slot1_power", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 257, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.INT }
                }
            });

            model.HoldingRegisters.Add("slot1_enabled", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 258, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.INT }
                }
            });

            model.HoldingRegisters.Add("slot2_start_time", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 259, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.TIME_HHMM }
                }
            });

            model.HoldingRegisters.Add("slot2_end_time", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 260, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.TIME_HHMM }
                }
            });

            model.HoldingRegisters.Add("slot2_mode", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 261, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.INT }
                }
            });

            model.HoldingRegisters.Add("slot2_power", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 262, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.INT }
                }
            });

            model.HoldingRegisters.Add("slot2_enabled", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 263, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.INT }
                }
            });

            model.HoldingRegisters.Add("slot3_start_time", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 264, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.TIME_HHMM }
                }
            });

            model.HoldingRegisters.Add("slot3_end_time", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 265, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.TIME_HHMM }
                }
            });

            model.HoldingRegisters.Add("slot3_mode", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 266, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.INT }
                }
            });

            model.HoldingRegisters.Add("slot3_power", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 267, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.INT }
                }
            });

            model.HoldingRegisters.Add("slot3_enabled", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 268, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.INT }
                }
            });

            model.HoldingRegisters.Add("slot4_start_time", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 269, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.TIME_HHMM }
                }
            });

            model.HoldingRegisters.Add("slot4_end_time", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 270, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.TIME_HHMM }
                }
            });

            model.HoldingRegisters.Add("slot4_mode", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 271, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.INT }
                }
            });

            model.HoldingRegisters.Add("slot4_power", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 272, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.INT }
                }
            });

            model.HoldingRegisters.Add("slot4_enabled", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 273, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.INT }
                }
            });

            model.HoldingRegisters.Add("slot5_start_time", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 274, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.TIME_HHMM }
                }
            });

            model.HoldingRegisters.Add("slot5_end_time", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 275, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.TIME_HHMM }
                }
            });

            model.HoldingRegisters.Add("slot5_mode", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 276, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.INT }
                }
            });

            model.HoldingRegisters.Add("slot5_power", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 277, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.INT }
                }
            });

            model.HoldingRegisters.Add("slot5_enabled", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 278, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.INT }
                }
            });

            model.HoldingRegisters.Add("slot6_start_time", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 279, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.TIME_HHMM }
                }
            });

            model.HoldingRegisters.Add("slot6_end_time", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 280, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.TIME_HHMM }
                }
            });

            model.HoldingRegisters.Add("slot6_mode", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 281, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.INT }
                }
            });

            model.HoldingRegisters.Add("slot6_power", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 282, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.INT }
                }
            });

            model.HoldingRegisters.Add("slot6_enabled", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 283, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.INT }
                }
            });

            model.HoldingRegisters.Add("slot7_start_time", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 284, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.TIME_HHMM }
                }
            });

            model.HoldingRegisters.Add("slot7_end_time", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 285, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.TIME_HHMM }
                }
            });

            model.HoldingRegisters.Add("slot7_mode", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 286, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.INT }
                }
            });

            model.HoldingRegisters.Add("slot7_power", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 287, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.INT }
                }
            });

            model.HoldingRegisters.Add("slot7_enabled", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 288, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.INT }
                }
            });

            model.HoldingRegisters.Add("slot8_start_time", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 289, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.TIME_HHMM }
                }
            });

            model.HoldingRegisters.Add("slot8_end_time", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 290, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.TIME_HHMM }
                }
            });

            model.HoldingRegisters.Add("slot8_mode", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 291, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.INT }
                }
            });

            model.HoldingRegisters.Add("slot8_power", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 292, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.INT }
                }
            });

            model.HoldingRegisters.Add("slot8_enabled", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 293, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.INT }
                }
            });

            model.HoldingRegisters.Add("slot9_start_time", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 294, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.TIME_HHMM }
                }
            });

            model.HoldingRegisters.Add("slot9_end_time", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 295, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.TIME_HHMM }
                }
            });

            model.HoldingRegisters.Add("slot9_mode", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 296, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.INT }
                }
            });

            model.HoldingRegisters.Add("slot9_power", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 297, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.INT }
                }
            });

            model.HoldingRegisters.Add("slot9_enabled", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 298, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.INT }
                }
            });

            #endregion

            #region Hardcoded InputRegisters

            // RegisterNo 1
            model.InputRegisters.Add(nameof(DeviceNoahLastData.status), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 1, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.INT }
                }
            });

            // RegisterNo 2
            model.InputRegisters.Add(nameof(DeviceNoahLastData.pac), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 2, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 1.0 } }
                }
            });

            // RegisterNo 3
            model.InputRegisters.Add(nameof(DeviceNoahLastData.isAgain), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 3, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.INT }
                }
            });

            // RegisterNo 4
            model.InputRegisters.Add(nameof(DeviceNoahLastData.pdWarnStatus), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 4, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.INT }
                }
            });

            // RegisterNo 5
            model.InputRegisters.Add(nameof(DeviceNoahLastData.mpptProtectStatus), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 5, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.INT }
                }
            });

            // RegisterNo 6
            model.InputRegisters.Add(nameof(DeviceNoahLastData.time), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 6, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.INT }
                }
            });

            // RegisterNo 7
            model.InputRegisters.Add(nameof(DeviceNoahLastData.ppv), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 7, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 1.0 } }
                }
            });

            // RegisterNo 8
            model.InputRegisters.Add(nameof(DeviceNoahLastData.workMode), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 8, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.ENUM, EnumOptions = new GrowattEnumOptions { EnumType = "INT_MAP", Values = new Dictionary<string, string> { { "0", "Load First" }, { "1", "Battery First" }, { "2", "Grid First" } } } }
                }
            });

            // RegisterNo 9
            model.InputRegisters.Add(nameof(DeviceNoahLastData.batteryPackageQuantity), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 9, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.INT }
                }
            });

            // RegisterNo 10
            model.InputRegisters.Add(nameof(DeviceNoahLastData.batterySoh), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 10, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.ENUM, EnumOptions = new GrowattEnumOptions { EnumType = "INT_MAP", Values = new Dictionary<string, string> { { "0", "Idle" }, { "1", "Charging" }, { "2", "Discharging" } } } }
                }
            });

            // RegisterNo 11
            model.InputRegisters.Add(nameof(DeviceNoahLastData.totalBatteryPackChargingPower), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 11, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = -30000.0, Multiplier = 1.0 } }
                }
            });

            // RegisterNo 12
            model.InputRegisters.Add(nameof(DeviceNoahLastData.batteryCycles), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 12, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 1.0 } }
                }
            });

            // RegisterNo 13
            model.InputRegisters.Add(nameof(DeviceNoahLastData.totalBatteryPackSoc), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 13, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 1.0 } }
                }
            });

            // RegisterNo 14
            model.InputRegisters.Add(nameof(DeviceNoahLastData.workMode), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 14, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.ENUM }
                }
            });

            // RegisterNo 15
            model.InputRegisters.Add(nameof(DeviceNoahLastData.totalBatteryPackChargingStatus), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 15, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.ENUM }
                }
            });

            // RegisterNo 16
            model.InputRegisters.Add(nameof(DeviceNoahLastData.totalBatteryPackChargingPower), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 16, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 1.0 } }
                }
            });

            // RegisterNo 17
            model.InputRegisters.Add(nameof(DeviceNoahLastData.heatingStatus), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 17, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.ENUM }
                }
            });

            // RegisterNo 18
            model.InputRegisters.Add(nameof(DeviceNoahLastData.faultStatus), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 18, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.ENUM }
                }
            });

            // RegisterNo 19
            model.InputRegisters.Add(nameof(DeviceNoahLastData.timeStr), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 19, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.INT }
                }
            });

            // RegisterNo 20
            model.InputRegisters.Add(nameof(DeviceNoahLastData.deviceSn), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 20, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.INT }
                }
            });

            // RegisterNo 21-28 (Zusammengesetzt für battery1SerialNum)
            model.InputRegisters.Add("battery1SerialNum_part1", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 21, Offset = 0, Size = 4 },
                    Data = new GrowattData { DataType = GrowattDataType.STRING }
                }
            });

            // RegisterNo 23
            model.InputRegisters.Add("battery1SerialNum_part2", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 23, Offset = 0, Size = 4 },
                    Data = new GrowattData { DataType = GrowattDataType.STRING }
                }
            });

            // RegisterNo 25
            model.InputRegisters.Add("battery1SerialNum_part3", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 25, Offset = 0, Size = 4 },
                    Data = new GrowattData { DataType = GrowattDataType.STRING }
                }
            });

            // RegisterNo 27
            model.InputRegisters.Add("battery1SerialNum_part4", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 27, Offset = 0, Size = 4 },
                    Data = new GrowattData { DataType = GrowattDataType.STRING }
                }
            });

            // RegisterNo 29
            model.InputRegisters.Add(nameof(DeviceNoahLastData.battery1Soc), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 29, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 1.0 } }
                }
            });

            // RegisterNo 30
            model.InputRegisters.Add(nameof(DeviceNoahLastData.battery1Temp), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 30, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT }
                }
            });

            // RegisterNo 31
            model.InputRegisters.Add(nameof(DeviceNoahLastData.battery1WarnStatus), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 31, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.ENUM }
                }
            });

            // RegisterNo 32
            model.InputRegisters.Add(nameof(DeviceNoahLastData.battery1ProtectStatus), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 32, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.ENUM }
                }
            });

            // RegisterNo 33-40 (Zusammengesetzt für battery2SerialNum)
            model.InputRegisters.Add("battery2SerialNum_part1", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 33, Offset = 0, Size = 4 },
                    Data = new GrowattData { DataType = GrowattDataType.STRING }
                }
            });

            // RegisterNo 35
            model.InputRegisters.Add("battery2SerialNum_part2", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 35, Offset = 0, Size = 4 },
                    Data = new GrowattData { DataType = GrowattDataType.STRING }
                }
            });

            // RegisterNo 37
            model.InputRegisters.Add("battery2SerialNum_part3", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 37, Offset = 0, Size = 4 },
                    Data = new GrowattData { DataType = GrowattDataType.STRING }
                }
            });

            // RegisterNo 39
            model.InputRegisters.Add("battery2SerialNum_part4", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 39, Offset = 0, Size = 4 },
                    Data = new GrowattData { DataType = GrowattDataType.STRING }
                }
            });

            // RegisterNo 41
            model.InputRegisters.Add(nameof(DeviceNoahLastData.battery2Soc), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 41, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 1.0 } }
                }
            });

            // RegisterNo 42
            model.InputRegisters.Add(nameof(DeviceNoahLastData.battery2Temp), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 42, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT }
                }
            });

            // RegisterNo 43
            model.InputRegisters.Add(nameof(DeviceNoahLastData.battery2WarnStatus), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 43, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.ENUM }
                }
            });

            // RegisterNo 44
            model.InputRegisters.Add(nameof(DeviceNoahLastData.battery2ProtectStatus), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 44, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.ENUM }
                }
            });

            // RegisterNo 45-52 (Zusammengesetzt für battery3SerialNum)
            model.InputRegisters.Add("battery3SerialNum_part1", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 45, Offset = 0, Size = 4 },
                    Data = new GrowattData { DataType = GrowattDataType.STRING }
                }
            });

            // RegisterNo 47
            model.InputRegisters.Add("battery3SerialNum_part2", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 47, Offset = 0, Size = 4 },
                    Data = new GrowattData { DataType = GrowattDataType.STRING }
                }
            });

            // RegisterNo 49
            model.InputRegisters.Add("battery3SerialNum_part3", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 49, Offset = 0, Size = 4 },
                    Data = new GrowattData { DataType = GrowattDataType.STRING }
                }
            });

            // RegisterNo 51
            model.InputRegisters.Add("battery3SerialNum_part4", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 51, Offset = 0, Size = 4 },
                    Data = new GrowattData { DataType = GrowattDataType.STRING }
                }
            });

            // RegisterNo 53
            model.InputRegisters.Add(nameof(DeviceNoahLastData.battery3Soc), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 53, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 1.0 } }
                }
            });

            // RegisterNo 54
            model.InputRegisters.Add(nameof(DeviceNoahLastData.battery3Temp), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 54, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT }
                }
            });

            // RegisterNo 55
            model.InputRegisters.Add(nameof(DeviceNoahLastData.battery3WarnStatus), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 55, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.ENUM }
                }
            });

            // RegisterNo 56
            model.InputRegisters.Add(nameof(DeviceNoahLastData.battery3ProtectStatus), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 56, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.ENUM }
                }
            });

            // RegisterNo 57-64 (Zusammengesetzt für battery4SerialNum)
            model.InputRegisters.Add("battery4SerialNum_part1", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 57, Offset = 0, Size = 4 },
                    Data = new GrowattData { DataType = GrowattDataType.STRING }
                }
            });

            // RegisterNo 59
            model.InputRegisters.Add("battery4SerialNum_part2", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 59, Offset = 0, Size = 4 },
                    Data = new GrowattData { DataType = GrowattDataType.STRING }
                }
            });

            // RegisterNo 61
            model.InputRegisters.Add("battery4SerialNum_part3", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 61, Offset = 0, Size = 4 },
                    Data = new GrowattData { DataType = GrowattDataType.STRING }
                }
            });

            // RegisterNo 63
            model.InputRegisters.Add("battery4SerialNum_part4", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 63, Offset = 0, Size = 4 },
                    Data = new GrowattData { DataType = GrowattDataType.STRING }
                }
            });

            // RegisterNo 65
            model.InputRegisters.Add(nameof(DeviceNoahLastData.battery4Soc), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 65, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 1.0 } }
                }
            });

            // RegisterNo 66
            model.InputRegisters.Add(nameof(DeviceNoahLastData.battery4Temp), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 66, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT }
                }
            });

            // RegisterNo 67
            model.InputRegisters.Add(nameof(DeviceNoahLastData.battery4WarnStatus), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 67, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.ENUM }
                }
            });

            // RegisterNo 68
            model.InputRegisters.Add(nameof(DeviceNoahLastData.battery4ProtectStatus), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 68, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.ENUM }
                }
            });

            // RegisterNo 69
            model.InputRegisters.Add(nameof(DeviceNoahLastData.settableTimePeriod), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 69, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.ENUM }
                }
            });

            // RegisterNo 70
            model.InputRegisters.Add(nameof(DeviceNoahLastData.acCoupleWarnStatus), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 70, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.ENUM }
                }
            });

            // RegisterNo 71
            model.InputRegisters.Add(nameof(DeviceNoahLastData.acCoupleProtectStatus), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 71, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.ENUM }
                }
            });

            // RegisterNo 72
            model.InputRegisters.Add(nameof(DeviceNoahLastData.eacToday), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 72, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 0.1 } }
                }
            });

            // RegisterNo 73
            model.InputRegisters.Add(nameof(DeviceNoahLastData.eacTotal), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 73, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 0.1 } }
                }
            });

            // RegisterNo 74
            model.InputRegisters.Add(nameof(DeviceNoahLastData.eacMonth), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 74, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 0.1 } }
                }
            });

            // RegisterNo 75
            model.InputRegisters.Add(nameof(DeviceNoahLastData.datalogSn), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 75, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 0.1 } }
                }
            });

            // RegisterNo 76
            model.InputRegisters.Add(nameof(DeviceNoahLastData.eacYear), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 76, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 0.1 } }
                }
            });

            // RegisterNo 77-78 haben keine direkte Entsprechung
            model.InputRegisters.Add("RegisterNo77", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 77, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 0.1 } }
                }
            });

            // RegisterNo 78
            model.InputRegisters.Add("eng_out_device", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 78, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 0.1 } }
                }
            });

            // RegisterNo 79
            model.InputRegisters.Add(nameof(DeviceNoahLastData.ctFlag), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 79, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.ENUM }
                }
            });

            // RegisterNo 80
            model.InputRegisters.Add(nameof(DeviceNoahLastData.totalHouseholdLoad), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 80, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT }
                }
            });

            // RegisterNo 81
            model.InputRegisters.Add(nameof(DeviceNoahLastData.householdLoadApartFromGroplug), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 81, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT }
                }
            });

            // RegisterNo 82
            model.InputRegisters.Add(nameof(DeviceNoahLastData.onOffGrid), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 82, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.ENUM }
                }
            });

            // RegisterNo 83
            model.InputRegisters.Add(nameof(DeviceNoahLastData.ctSelfPower), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 83, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 1.0 } }
                }
            });

            // RegisterNo 84-89 haben keine direkte Entsprechung
            model.InputRegisters.Add("RegisterNo84", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 84, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 1.0 } }
                }
            });

            // RegisterNo 85-89 überspringen wir zur Kürze

            // RegisterNo 90
            model.InputRegisters.Add(nameof(DeviceNoahLastData.chargeSocLimit), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 90, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 1.0 } }
                }
            });

            // RegisterNo 91
            model.InputRegisters.Add(nameof(DeviceNoahLastData.dischargeSocLimit), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 91, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 1.0 } }
                }
            });

            // RegisterNo 92
            model.InputRegisters.Add(nameof(DeviceNoahLastData.pv1Voltage), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 92, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 0.01 } }
                }
            });

            // RegisterNo 93
            model.InputRegisters.Add(nameof(DeviceNoahLastData.pv1Current), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 93, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 0.01 } }
                }
            });

            // RegisterNo 94
            model.InputRegisters.Add(nameof(DeviceNoahLastData.pv1Temp), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 94, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 0.01 } }
                }
            });

            // RegisterNo 95
            model.InputRegisters.Add(nameof(DeviceNoahLastData.pv2Voltage), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 95, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 0.01 } }
                }
            });

            // RegisterNo 96
            model.InputRegisters.Add(nameof(DeviceNoahLastData.pv2Current), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 96, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 0.01 } }
                }
            });

            // RegisterNo 97
            model.InputRegisters.Add(nameof(DeviceNoahLastData.pv2Temp), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 97, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 0.01 } }
                }
            });

            // RegisterNo 98
            model.InputRegisters.Add(nameof(DeviceNoahLastData.batterySoh), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 98, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT }
                }
            });

            // RegisterNo 99
            model.InputRegisters.Add(nameof(DeviceNoahLastData.maxCellVoltage), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 99, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 0.001 } }
                }
            });

            // RegisterNo 100
            model.InputRegisters.Add(nameof(DeviceNoahLastData.minCellVoltage), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 100, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 0.001 } }
                }
            });

            // RegisterNo 101
            model.InputRegisters.Add(nameof(DeviceNoahLastData.batteryCycles), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 101, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 1.0 } }
                }
            });

            // Restliche Register
            // RegisterNo 102
            model.InputRegisters.Add("register_102", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 102, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 1.0 } }
                }
            });

            // RegisterNo 103
            model.InputRegisters.Add(nameof(DeviceNoahLastData.pv3Voltage), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 103, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 0.01 } }
                }
            });

            // RegisterNo 104
            model.InputRegisters.Add(nameof(DeviceNoahLastData.pv3Current), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 104, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 0.01 } }
                }
            });

            // RegisterNo 105
            model.InputRegisters.Add(nameof(DeviceNoahLastData.pv3Temp), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 105, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 0.01 } }
                }
            });

            // RegisterNo 106
            model.InputRegisters.Add(nameof(DeviceNoahLastData.pv4Voltage), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 106, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 0.01 } }
                }
            });

            // RegisterNo 107
            model.InputRegisters.Add(nameof(DeviceNoahLastData.pv4Current), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 107, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 0.01 } }
                }
            });

            // RegisterNo 108
            model.InputRegisters.Add(nameof(DeviceNoahLastData.pv4Temp), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 108, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 0.01 } }
                }
            });

            // RegisterNo 109 (keine direkte Entsprechung)
            model.InputRegisters.Add("out_voltage", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 109, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 0.01 } }
                }
            });

            // RegisterNo 110
            model.InputRegisters.Add(nameof(DeviceNoahLastData.systemTemp), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 110, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT }
                }
            });

            // RegisterNo 112
            model.InputRegisters.Add(nameof(DeviceNoahLastData.maxCellVoltage), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 112, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT }
                }
            });

            // RegisterNo 114
            model.InputRegisters.Add(nameof(DeviceNoahLastData.minCellVoltage), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 114, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT }
                }
            });

            // Temperaturen in Fahrenheit
            // RegisterNo 120
            model.InputRegisters.Add(nameof(DeviceNoahLastData.battery1TempF), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 120, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT }
                }
            });

            // RegisterNo 122
            model.InputRegisters.Add(nameof(DeviceNoahLastData.battery2TempF), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 122, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT }
                }
            });

            // RegisterNo 124
            model.InputRegisters.Add(nameof(DeviceNoahLastData.battery3TempF), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 124, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT }
                }
            });

            // RegisterNo 126
            model.InputRegisters.Add(nameof(DeviceNoahLastData.battery4TempF), new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 126, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT }
                }
            });

            #endregion

            #region PresentRegisters

            model.PresentRegisters.Add("smart_watt", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 311, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.INT }
                }
            });

            model.PresentRegisters.Add("default_watt", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 252, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.INT }
                }
            });

            #endregion

            logger?.LogTrace("GrowattRegisterModel.SeedDefaults: Seeded {HoldingCount} holding and {InputCount} input registers.",
                model.HoldingRegisters.Count, model.InputRegisters.Count);

            return model;
        }
    }
}