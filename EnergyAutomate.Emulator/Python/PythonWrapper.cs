using EnergyAutomate.Emulator.Growatt;
using EnergyAutomate.Emulator.Growatt.Models;
using EnergyAutomate.Emulator.Python;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Python.Runtime;
using System.Reflection;
using System.Text;

namespace EnergyAutomate.Emulator
{
    public class PythonWrapper
    {
        private GrowattMqttParser GrowattModbusMqttParser { get; set; }
        private IServiceProvider ServiceProvider { get; set; }
        private ILogger<PythonWrapper> Logger => ServiceProvider.GetRequiredService<ILogger<PythonWrapper>>();
        public GrowattClientOptions? GrowattClientOptions { get; set; }

        public delegate void LogCallback(string message);
        public delegate Task DumpCallback(string topic, byte[] payload, int qos, int retain, int state, int dup, int mid);

        private Thread? _pythonThread;
        private AutoResetEvent _stopPythonEvent = new AutoResetEvent(false);
        private dynamic? _clientInstance;

        public PythonWrapper(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
            GrowattModbusMqttParser = new GrowattMqttParser(serviceProvider);
        }

        public void StartPythonClient()
        {
            LogFromPython("[TRACE] Starting Python background thread");
            _pythonThread = new Thread(RunPythonClient)
            {
                IsBackground = true
            };
            _pythonThread.Start();
            LogFromPython("[TRACE] Python client running in background. Press ENTER to stop...");
        }

        public void StopPythonClient()
        {
            _stopPythonEvent.Set();
            _pythonThread?.Join();
        }

        private void RunPythonClient()
        {
            try
            {
                string assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory;
                LogFromPython($"[TRACE] Assembly directory: {assemblyDirectory}");

                LogFromPython("[TRACE] Initializing Python runtime in background thread");
                PythonEngine.Initialize();

                // Initialisierung und Start des Python-Clients im GIL-Kontext
                using (Py.GIL())
                {
                    dynamic sys = Py.Import("sys");
                    sys.path.append(Path.Combine(assemblyDirectory, "Python"));

                    LogCallback logCallback = LogFromPython;
                    DumpCallback dumpCallback = DumpFromPython;

                    LogFromPython("[TRACE] Importing Python client module");
                    dynamic clientModule = Py.Import("PythonGrowattClient");
                    dynamic ClientClass = clientModule.Client;

                    LogFromPython("[TRACE] Creating instance of Client class");
                    _clientInstance = ClientClass();
                    _clientInstance.set_log_callback(logCallback);
                    _clientInstance.set_dump_callback(dumpCallback);

                    if (GrowattClientOptions is not null)
                    {
                        LogFromPython("[TRACE] Set options");
                        _clientInstance.set_options(GrowattClientOptions);
                    }

                    LogFromPython("[TRACE] Starting Python client");
                    _clientInstance.run();
                }

                PythonEngine.Shutdown();
                LogFromPython("[TRACE] Python runtime shutdown complete");
            }
            catch (Exception ex)
            {
                LogFromPython("[TRACE] Exception in Python thread: " + ex);
            }
        }

        public void SetSmartWatt(ushort value)
        {
            if (_clientInstance != null)
            {
                var deviceId = "0PVP50ZR16ST00CB";
                ushort startRegister = 310;
                ushort[] values = { 0, value, 1 }; // Entspricht 0x0000, 0x022D, 0x0001

                byte[] commandPayload = GrowattModbusMqttParser.BuildSetMultipleRegistersCommand(deviceId, startRegister, values);

                _clientInstance.send_msg($"s/33/{deviceId}", commandPayload, 0, 0);
            }
        }

        public void LogFromPython(string message)
        {
            try
            {
                Logger.LogInformation("{Message}", message);
            }
            catch (Exception)
            {
                throw;
            }
        }

        public async Task DumpFromPython(string topic, byte[] payload, int qos, int retain, int state, int dup, int mid)
        {
            try
            {
                PythonMqttMessage message = new PythonMqttMessage
                {
                    Topic = topic,
                    Payload = payload,
                    Qos = qos,
                    Retain = retain,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    State = state,
                    Dup = dup,
                    Mid = mid
                };

                string dumpDirectory = Path.Combine(AppContext.BaseDirectory, "dump");
                if (!Directory.Exists(dumpDirectory))
                {
                    Directory.CreateDirectory(dumpDirectory);
                    Logger.LogInformation("[TRACE] Created dump directory at {DumpDirectory}", dumpDirectory);
                }

                DateTime dateTime = DateTimeOffset.FromUnixTimeSeconds((long)message.Timestamp).DateTime;
                string datePart = dateTime.ToString("yyyyMMdd");
                string topicPart = string.Join("_", message.Topic.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
                string fileName = $"{datePart}_{topicPart}.txt";
                string filePath = Path.Combine(dumpDirectory, fileName);

                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"Timestamp: {message.Timestamp}");
                sb.AppendLine($"DateTime: {dateTime:O}");
                sb.AppendLine($"Topic: {message.Topic}");
                sb.AppendLine($"Payload: {BitConverter.ToString(message.Payload)}");
                sb.AppendLine($"Qos: {message.Qos}");
                sb.AppendLine($"Retain: {message.Retain}");
                sb.AppendLine($"Mid: {message.Mid}");
                sb.AppendLine($"State: {message.State}");
                sb.AppendLine($"Dup: {message.Dup}");

                var modBusMessage = GrowattModbusMqttParser.ParseModbusMessage(message.Payload, message.Topic);

                if (modBusMessage != null)
                {
                    sb.AppendLine("Parsed Modbus Message:");
                    sb.AppendLine($"RawDate: {BitConverter.ToString(modBusMessage.RawData)}");
                    sb.AppendLine($"Function Code: {modBusMessage.Function.ToString()}");
                    sb.AppendLine($"Device ID: {modBusMessage.DeviceId}");
                    sb.AppendLine("RegisterBlocks:");
                    foreach (var kvp in modBusMessage.RegisterBlocks)
                    {
                        sb.AppendLine($"Start {kvp.Start}: End {kvp.End} : Values {BitConverter.ToString(kvp.Values)}");
                    }
                }
                else
                {
                    sb.AppendLine("No Modbus message parsed from payload.");
                }

                await File.AppendAllTextAsync(filePath, sb.ToString());
            }
            catch (Exception ex)
            {
                Logger.LogError("[TRACE] Error dumping MQTT message: {Exception}", ex);
            }
        }
    }
}