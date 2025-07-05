using System.Text;
using Microsoft.Extensions.Logging;

namespace EnergyAutomate.Emulator
{
    public class GrowattModbusFunctionMultiple
    {
        public string DeviceId { get; set; } = string.Empty;
        public GrowattModbusFunction Function { get; set; }
        public ushort Start { get; set; }
        public ushort End { get; set; }
        public byte[] Values { get; set; } = Array.Empty<byte>();

        public static GrowattModbusFunctionMultiple? Parse(byte[] buffer, ILogger logger = null)
        {
            if (buffer == null || buffer.Length < 42)
            {
                logger?.LogWarning("GrowattModbusFunctionMultiple.Parse: Buffer too short.");
                return null;
            }

            ushort unknown = (ushort)((buffer[0] << 8) | buffer[1]);
            ushort constant7 = (ushort)((buffer[2] << 8) | buffer[3]);
            ushort msgLen = (ushort)((buffer[4] << 8) | buffer[5]);
            byte constant1 = buffer[6];
            byte function = buffer[7];
            string deviceId = Encoding.ASCII.GetString(buffer, 8, 30).Trim('\0');
            ushort start = (ushort)((buffer[38] << 8) | buffer[39]);
            ushort end = (ushort)((buffer[40] << 8) | buffer[41]);
            byte[] values = new byte[buffer.Length - 42];
            Array.Copy(buffer, 42, values, 0, values.Length);

            logger?.LogTrace("GrowattModbusFunctionMultiple.Parse: deviceId={DeviceId}, function={Function}, start={Start}, end={End}, valuesLength={ValuesLength}", deviceId, function, start, end, values.Length);

            return new GrowattModbusFunctionMultiple
            {
                DeviceId = deviceId,
                Function = (GrowattModbusFunction)function,
                Start = start,
                End = end,
                Values = values
            };
        }

        public byte[] Build(ILogger logger = null)
        {
            byte[] deviceIdBytes = Encoding.ASCII.GetBytes(DeviceId.PadRight(30, '\0'));
            ushort msgLen = (ushort)(36 + Values.Length);

            byte[] result = new byte[42 + Values.Length];
            result[0] = 0; result[1] = 1; // unknown = 1
            result[2] = 0; result[3] = 7; // constant 7
            result[4] = (byte)(msgLen >> 8); result[5] = (byte)(msgLen & 0xFF);
            result[6] = 1; // constant 1
            result[7] = (byte)Function;
            Array.Copy(deviceIdBytes, 0, result, 8, 30);
            result[38] = (byte)(Start >> 8); result[39] = (byte)(Start & 0xFF);
            result[40] = (byte)(End >> 8); result[41] = (byte)(End & 0xFF);
            Array.Copy(Values, 0, result, 42, Values.Length);

            logger?.LogTrace("GrowattModbusFunctionMultiple.Build: deviceId={DeviceId}, function={Function}, start={Start}, end={End}, valuesLength={ValuesLength}", DeviceId, Function, Start, End, Values.Length);

            return result;
        }
    }
}