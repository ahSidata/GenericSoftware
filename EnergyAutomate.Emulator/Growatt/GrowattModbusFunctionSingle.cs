using Microsoft.Extensions.Logging;
using System.Text;

namespace EnergyAutomate.Emulator.Growatt
{
    public class GrowattModbusFunctionSingle
    {
        public string DeviceId { get; set; } = string.Empty;
        public GrowattModbusFunction Function { get; set; }
        public ushort Register { get; set; }
        public ushort Value { get; set; }

        public static GrowattModbusFunctionSingle? Parse(byte[] buffer, ILogger logger = null)
        {
            if (buffer == null || buffer.Length < 42)
            {
                logger?.LogWarning("GrowattModbusFunctionSingle.Parse: Buffer too short.");
                return null;
            }

            ushort unknown = (ushort)(buffer[0] << 8 | buffer[1]);
            ushort constant7 = (ushort)(buffer[2] << 8 | buffer[3]);
            ushort msgLen = (ushort)(buffer[4] << 8 | buffer[5]);
            byte constant1 = buffer[6];
            byte function = buffer[7];
            string deviceId = Encoding.ASCII.GetString(buffer, 8, 30).Trim('\0');
            ushort register = (ushort)(buffer[38] << 8 | buffer[39]);
            ushort value = (ushort)(buffer[40] << 8 | buffer[41]);

            logger?.LogTrace("GrowattModbusFunctionSingle.Parse: deviceId={DeviceId}, function={Function}, register={Register}, value={Value}", deviceId, function, register, value);

            return new GrowattModbusFunctionSingle
            {
                DeviceId = deviceId,
                Function = (GrowattModbusFunction)function,
                Register = register,
                Value = value
            };
        }

        public byte[] Build(ILogger logger = null)
        {
            byte[] deviceIdBytes = Encoding.ASCII.GetBytes(DeviceId.PadRight(30, '\0'));
            ushort msgLen = 36;

            byte[] result = new byte[42];
            result[0] = 0; result[1] = 1; // unknown = 1
            result[2] = 0; result[3] = 7; // constant 7
            result[4] = (byte)(msgLen >> 8); result[5] = (byte)(msgLen & 0xFF);
            result[6] = 1; // constant 1
            result[7] = (byte)Function;
            Array.Copy(deviceIdBytes, 0, result, 8, 30);
            result[38] = (byte)(Register >> 8); result[39] = (byte)(Register & 0xFF);
            result[40] = (byte)(Value >> 8); result[41] = (byte)(Value & 0xFF);

            logger?.LogTrace("GrowattModbusFunctionSingle.Build: deviceId={DeviceId}, function={Function}, register={Register}, value={Value}", DeviceId, Function, Register, Value);

            return result;
        }
    }
}