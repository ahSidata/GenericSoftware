using EnergyAutomate.Emulator.Growatt.Models;
using Microsoft.Extensions.Logging;

namespace EnergyAutomate.Emulator.Growatt
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

        /// <summary>
        /// Parses all holding registers from a Modbus message using the known register definitions.
        /// </summary>
        /// <param name="modbusMessage">The Modbus message containing the data.</param>
        /// <param name="holdingRegisters">Dictionary of register definitions.</param>
        /// <returns>Dictionary with register names and parsed values.</returns>
        public Dictionary<string, object> ParseRegisters(GrowattModbusMessage modbusMessage, Dictionary<string, GrowattParameter> keyValuePairs)
        {
            var result = new Dictionary<string, object>();

            foreach (var kvp in keyValuePairs)
            {
                string name = kvp.Key;
                var register = kvp.Value;

                // Assuming GrowattRegisterModel has a property or method to get the position
                var position = register.Growatt.Position; // Replace with actual method/property

                // Get raw data for the register
                var dataRaw = modbusMessage.GetData(position);

                if (dataRaw == null)
                {
                    continue;
                }

                // Assuming GrowattRegisterModel has a method to parse data
                var value = register.Growatt.Data.Parse(dataRaw); // Replace with actual method

                if (value == null)
                {
                    continue;
                }

                result[name] = value;
            }

            return result;
        }
    }
}