using System.Text;
using Microsoft.Extensions.Logging;

namespace EnergyAutomate.Emulator.Growatt.Models
{
    /// <summary>
    /// Represents the data type and parsing logic for a Growatt register.
    /// </summary>
    public class GrowattData
    {
        private static ILogger? _logger;

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

            string? unpackType = dataRaw.Length switch
            {
                1 => "B",
                2 => "H",
                4 => "I",
                _ => null
            };

            if (unpackType == null)
            {
                _logger?.LogWarning("[GrowattData.Parse] Unsupported data length: {DataLength}", dataRaw.Length);
                return null;
            }

            // Helper for big-endian conversion
            int ToInt(byte[] bytes)
            {
                if (bytes.Length == 1) return bytes[0];
                if (bytes.Length == 2) return bytes[0] << 8 | bytes[1];
                if (bytes.Length == 4) return bytes[0] << 24 | bytes[1] << 16 | bytes[2] << 8 | bytes[3];
                throw new ArgumentException("Unsupported byte length");
            }

            switch (DataType)
            {
                case GrowattDataType.FLOAT:
                    // Use default FloatOptions if none provided
                    var floatOpts = FloatOptions ?? new GrowattFloatOptions { Multiplier = 1.0, Delta = 0.0 };
                    var floatValue = ToInt(dataRaw) * floatOpts.Multiplier + floatOpts.Delta;
                    floatValue = Math.Round(floatValue, 3);
                    _logger?.LogDebug("[GrowattData.Parse] Parsed FLOAT: {FloatValue}", floatValue);
                    return floatValue;

                case GrowattDataType.TIME_HHMM:
                    {
                        var value = ToInt(dataRaw);
                        int h = value / 256;
                        int m = value % 256;
                        int hhmm = h * 100 + m;
                        _logger?.LogDebug("[GrowattData.Parse] Parsed TIME_HHMM: {TimeValue}", hhmm);
                        return hhmm;
                    }

                case GrowattDataType.INT:
                    {
                        var value = ToInt(dataRaw);
                        _logger?.LogDebug("[GrowattData.Parse] Parsed INT: {IntValue}", value);
                        return value;
                    }

                case GrowattDataType.ENUM:
                    {
                        if (EnumOptions == null)
                        {
                            _logger?.LogWarning("[GrowattData.Parse] EnumOptions missing for ENUM data type");
                            return null;
                        }
                        var value = ToInt(dataRaw);
                        if (EnumOptions.EnumType == "BITFIELD")
                        {
                            _logger?.LogWarning("[GrowattData.Parse] BITFIELD not implemented");
                            return null; // TODO: implement BITFIELD
                        }
                        else if (EnumOptions.EnumType == "INT_MAP")
                        {
                            if (EnumOptions.Values != null && EnumOptions.Values.TryGetValue(value.ToString(), out var enumValue))
                            {
                                _logger?.LogDebug("[GrowattData.Parse] Parsed ENUM INT_MAP: {EnumValue} ({EnumIntValue})", enumValue, value);
                                return value;
                            }
                            _logger?.LogWarning("[GrowattData.Parse] ENUM value {EnumValue} not found in map", value);
                            return null;
                        }
                        break;
                    }

                case GrowattDataType.STRING:
                    {
                        var value = Encoding.ASCII.GetString(dataRaw).Trim('\0');
                        _logger?.LogDebug("[GrowattData.Parse] Parsed STRING: {StringValue}", value);
                        return value;
                    }
            }

            _logger?.LogWarning("[GrowattData.Parse] Unknown DataType or not implemented: {DataType}", DataType);
            return null;
        }
    }
}