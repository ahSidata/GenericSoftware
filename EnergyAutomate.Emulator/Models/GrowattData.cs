using System;
using System.Collections.Generic;
using System.Text;

namespace EnergyAutomate.Emulator.Models
{
    /// <summary>
    /// Represents the data type and parsing logic for a Growatt register.
    /// </summary>
    public class GrowattData
    {
        public GrowattDataType DataType { get; set; }
        public GrowattFloatOptions? FloatOptions { get; set; }
        public GrowattEnumOptions? EnumOptions { get; set; }

        /// <summary>
        /// Parses the raw Modbus data according to the register's data type and options.
        /// </summary>
        /// <param name="dataRaw">Raw bytes from Modbus register.</param>
        /// <returns>Parsed value as object, or null if parsing fails.</returns>
        public object? Parse(byte[] dataRaw)
        {
            if (dataRaw == null || dataRaw.Length == 0)
            {
                return null;
            }

            // Trace log for input
            System.Diagnostics.Trace.WriteLine($"[GrowattData.Parse] DataType={DataType}, DataRaw={BitConverter.ToString(dataRaw)}");

            string unpackType = dataRaw.Length switch
            {
                1 => "B",
                2 => "H",
                4 => "I",
                _ => null
            };

            if (unpackType == null)
            {
                System.Diagnostics.Trace.WriteLine($"[GrowattData.Parse] Unsupported data length: {dataRaw.Length}");
                return null;
            }

            // Helper for big-endian conversion
            int ToInt(byte[] bytes)
            {
                if (bytes.Length == 1) return bytes[0];
                if (bytes.Length == 2) return (bytes[0] << 8) | bytes[1];
                if (bytes.Length == 4) return (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
                throw new ArgumentException("Unsupported byte length");
            }

            switch (DataType)
            {
                case GrowattDataType.FLOAT:
                    if (FloatOptions == null)
                    {
                        System.Diagnostics.Trace.WriteLine("[GrowattData.Parse] FloatOptions missing.");
                        return null;
                    }
                    var floatValue = ToInt(dataRaw) * FloatOptions.Multiplier + FloatOptions.Delta;
                    floatValue = Math.Round(floatValue, 3);
                    System.Diagnostics.Trace.WriteLine($"[GrowattData.Parse] Parsed FLOAT: {floatValue}");
                    return floatValue;

                case GrowattDataType.TIME_HHMM:
                    {
                        var value = ToInt(dataRaw);
                        int h = value / 256;
                        int m = value % 256;
                        int hhmm = (h * 100) + m;
                        System.Diagnostics.Trace.WriteLine($"[GrowattData.Parse] Parsed TIME_HHMM: {hhmm}");
                        return hhmm;
                    }

                case GrowattDataType.INT:
                    {
                        var value = ToInt(dataRaw);
                        System.Diagnostics.Trace.WriteLine($"[GrowattData.Parse] Parsed INT: {value}");
                        return value;
                    }

                case GrowattDataType.ENUM:
                    {
                        if (EnumOptions == null)
                        {
                            System.Diagnostics.Trace.WriteLine("[GrowattData.Parse] EnumOptions missing.");
                            return null;
                        }
                        var value = ToInt(dataRaw);
                        if (EnumOptions.EnumType == "BITFIELD")
                        {
                            System.Diagnostics.Trace.WriteLine("[GrowattData.Parse] BITFIELD not implemented.");
                            return null; // TODO: implement BITFIELD
                        }
                        else if (EnumOptions.EnumType == "INT_MAP")
                        {
                            if (EnumOptions.Values != null && EnumOptions.Values.TryGetValue(value.ToString(), out var enumValue))
                            {
                                System.Diagnostics.Trace.WriteLine($"[GrowattData.Parse] Parsed ENUM INT_MAP: {enumValue} ({value})");
                                return value;
                            }
                            System.Diagnostics.Trace.WriteLine($"[GrowattData.Parse] ENUM value {value} not found in map.");
                            return null;
                        }
                        break;
                    }

                case GrowattDataType.STRING:
                    {
                        var value = Encoding.ASCII.GetString(dataRaw).Trim('\0');
                        System.Diagnostics.Trace.WriteLine($"[GrowattData.Parse] Parsed STRING: {value}");
                        return value;
                    }
            }

            System.Diagnostics.Trace.WriteLine("[GrowattData.Parse] Unknown DataType or not implemented.");
            return null;
        }
    }
}