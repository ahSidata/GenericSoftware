using Microsoft.Extensions.Logging;
using System.Text;

namespace EnergyAutomate.Emulator.Growatt
{
    public class GrowattMetadata
    {
        public string DeviceSn { get; set; } = string.Empty;
        public DateTime? Timestamp { get; set; }

        public int Size() => 37;

        public static GrowattMetadata? Parse(byte[] buffer, ILogger? logger = null)
        {
            if (buffer.Length < 37)
            {
                logger?.LogWarning("GrowattMetadata.Parse: Buffer too short.");
                return null;
            }
            string deviceSn = Encoding.ASCII.GetString(buffer, 0, 30).Trim('\0');
            int offset = 30;
            int year = buffer[offset];
            int month = buffer[offset + 1];
            int day = buffer[offset + 2];
            int hour = buffer[offset + 3];
            int minute = buffer[offset + 4];
            int second = buffer[offset + 5];
            int millis = buffer[offset + 6];
            DateTime? timestamp = null;
            try
            {
                timestamp = new DateTime(year + 2000, month, day, hour, minute, second, millis);
            }
            catch (Exception ex)
            {
                logger?.LogTrace(ex, "GrowattMetadata.Parse: Invalid timestamp data.");
            }
            return new GrowattMetadata { DeviceSn = deviceSn, Timestamp = timestamp };
        }

        public byte[] Build()
        {
            var result = new byte[37];
            var snBytes = Encoding.ASCII.GetBytes(DeviceSn.PadRight(30, '\0'));
            Array.Copy(snBytes, 0, result, 0, 30);
            if (Timestamp.HasValue)
            {
                var ts = Timestamp.Value;
                result[30] = (byte)(ts.Year - 2000);
                result[31] = (byte)ts.Month;
                result[32] = (byte)ts.Day;
                result[33] = (byte)ts.Hour;
                result[34] = (byte)ts.Minute;
                result[35] = (byte)ts.Second;
                result[36] = (byte)(ts.Millisecond / 1); // Python: microsecond/1000, C#: Millisecond
            }
            return result;
        }
    }
}