using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace EnergyAutomate.Emulator.Models
{
    public class GrowattRegisterModel
    {
        public Dictionary<string, GrowattParameter> HoldingRegisters { get; set; } = new();
        public Dictionary<string, GrowattParameter> InputRegisters { get; set; } = new();

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

            model.InputRegisters.Add("bat_cnt", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 12, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 1.0 } }
                }
            });

            model.InputRegisters.Add("bat_cyclecnt", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 101, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 1.0 } }
                }
            });

            model.InputRegisters.Add("bat_sysstate", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 10, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.ENUM, EnumOptions = new GrowattEnumOptions { EnumType = "INT_MAP", Values = new Dictionary<string, string> { { "0", "Idle" }, { "1", "Charging" }, { "2", "Discharging" } } } }
                }
            });
            
            model.InputRegisters.Add("bat_1_soc_pct", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 29, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 1.0 } }
                }
            });

            model.InputRegisters.Add("bat_2_soc_pct", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 41, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 1.0 } }
                }
            });

            model.InputRegisters.Add("bat_3_soc_pct", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 53, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 1.0 } }
                }
            });

            model.InputRegisters.Add("bat_4_soc_pct", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 65, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 1.0 } }
                }
            });

            model.InputRegisters.Add("maxcvbat1", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 99, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 0.001 } }
                }
            });

            model.InputRegisters.Add("mincvbat1", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 100, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 0.001 } }
                }
            });

            model.InputRegisters.Add("tot_bat_soc_pct", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 13, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 1.0 } }
                }
            });

            model.InputRegisters.Add("Ipv1", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 93, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 0.01 } }
                }
            });

            model.InputRegisters.Add("Ipv2", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 96, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 0.01 } }
                }
            });

            model.InputRegisters.Add("Vpv1", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 92, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 0.01 } }
                }
            });

            model.InputRegisters.Add("Vpv2", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 95, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 0.01 } }
                }
            });

            model.InputRegisters.Add("out_power", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 2, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 1.0 } }
                }
            });

            model.InputRegisters.Add("out_voltage", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 109, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 0.01 } }
                }
            });

            model.InputRegisters.Add("pv_tot_power", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 7, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 1.0 } }
                }
            });

            model.InputRegisters.Add("pv_eng_today", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 72, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 0.1 } }
                }
            });

            model.InputRegisters.Add("pv_eng_month", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 74, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 0.1 } }
                }
            });

            model.InputRegisters.Add("pv_eng_year", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 76, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 0.1 } }
                }
            });

            model.InputRegisters.Add("eng_out_device", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 78, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 0.1 } }
                }
            });

            model.InputRegisters.Add("discharge_limit", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 91, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 1.0 } }
                }
            });

            model.InputRegisters.Add("charge_limit", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 90, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 1.0 } }
                }
            });

            model.InputRegisters.Add("charging_discharging", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 11, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = -30000.0, Multiplier = 1.0 } }
                }
            });

            model.InputRegisters.Add("temp1", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 94, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 0.01 } }
                }
            });

            model.InputRegisters.Add("temp2", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 97, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 0.01 } }
                }
            });

            model.InputRegisters.Add("ser_part_1", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 21, Offset = 0, Size = 4 },
                    Data = new GrowattData { DataType = GrowattDataType.STRING }
                }
            });

            model.InputRegisters.Add("ser_part_3", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 25, Offset = 0, Size = 4 },
                    Data = new GrowattData { DataType = GrowattDataType.STRING }
                }
            });

            model.InputRegisters.Add("ser_part_2", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 23, Offset = 0, Size = 4 },
                    Data = new GrowattData { DataType = GrowattDataType.STRING }
                }
            });

            model.InputRegisters.Add("ser_part_4", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 27, Offset = 0, Size = 4 },
                    Data = new GrowattData { DataType = GrowattDataType.STRING }
                }
            });

            model.InputRegisters.Add("priority_mode", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 8, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.ENUM, EnumOptions = new GrowattEnumOptions { EnumType = "INT_MAP", Values = new Dictionary<string, string> { { "0", "Load First" }, { "1", "Battery First" }, { "2", "Grid First" } } } }
                }
            });

            model.InputRegisters.Add("register_102", new GrowattParameter
            {
                Growatt = new GrowatttRegister
                {
                    Position = new GrowattRegisterPosition { RegisterNo = 102, Offset = 0, Size = 2 },
                    Data = new GrowattData { DataType = GrowattDataType.FLOAT, FloatOptions = new GrowattFloatOptions { Delta = 0.0, Multiplier = 1.0 } }
                }
            });

            #endregion

            logger?.LogTrace("GrowattRegisterModel.SeedDefaults: Seeded {HoldingCount} holding and {InputCount} input registers.",
                model.HoldingRegisters.Count, model.InputRegisters.Count);

            return model;
        }
    }
}