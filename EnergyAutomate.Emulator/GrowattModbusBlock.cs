using Microsoft.Extensions.Logging;

namespace EnergyAutomate.Emulator
{
    public class GrowattModbusBlock
    {
        public ushort Start { get; set; }
        public ushort End { get; set; }
        public byte[] Values { get; set; } = Array.Empty<byte>();

        public static GrowattModbusBlock? Parse(byte[] buffer, ILogger logger = null)
        {
            try
            {
                if (buffer.Length < 4)
                {
                    logger?.LogWarning("GrowattModbusBlock.Parse: Buffer too short.");
                    return null;
                }
                ushort start = (ushort)((buffer[0] << 8) | buffer[1]);
                ushort end = (ushort)((buffer[2] << 8) | buffer[3]);
                int numBlocks = end - start + 1;
                int valuesLength = numBlocks * 2;
                if (buffer.Length < 4 + valuesLength)
                {
                    logger?.LogWarning("GrowattModbusBlock.Parse: Buffer too short for values.");
                    return null;
                }
                var values = new byte[valuesLength];
                Array.Copy(buffer, 4, values, 0, valuesLength);
                if (values.Length != valuesLength)
                {
                    logger?.LogWarning("GrowattModbusBlock.Parse: Values length mismatch.");
                    return null;
                }
                logger?.LogTrace("GrowattModbusBlock.Parse: start={Start}, end={End}, valuesLength={ValuesLength}", start, end, valuesLength);
                return new GrowattModbusBlock { Start = start, End = end, Values = values };
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Parsing GrowattModbusBlock failed: {Message}", ex.Message);
                return null;
            }
        }

        public byte[] Build()
        {
            var result = new byte[4 + Values.Length];
            result[0] = (byte)(Start >> 8);
            result[1] = (byte)(Start & 0xFF);
            result[2] = (byte)(End >> 8);
            result[3] = (byte)(End & 0xFF);
            Array.Copy(Values, 0, result, 4, Values.Length);
            return result;
        }

        public int Size() => 4 + Values.Length;
    }
}