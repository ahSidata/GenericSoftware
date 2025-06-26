
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using paho.mqtt.client;
using Python.Runtime;
using System.Reflection;
using System.Text;

namespace EnergyAutomate.Emulator
{
    public class PythonWrapper
    {
        private GrowattModbusMqttParser GrowattModbusMqttParser { get; set; }

        private IServiceProvider ServiceProvider { get; set; }
        private ILogger<PythonWrapper> Logger => ServiceProvider.GetRequiredService<ILogger<PythonWrapper>>();

        public GrowattClientOptions? GrowattClientOptions { get; set; }

        // Define the delegate type for callbacks from Python
        public delegate void LogCallback(string message);

        public delegate void DumpCallback(string topic, byte[] payload, int qos, int retain, int state, int dup, int mid);

        private Thread? _pythonThread;
        private AutoResetEvent _stopPythonEvent = new AutoResetEvent(false);

        public PythonWrapper(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;

            GrowattModbusMqttParser  = new GrowattModbusMqttParser(serviceProvider.GetRequiredService<ILogger<GrowattModbusMqttParser>>());
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
                //Python.Runtime.Runtime.PythonDLL = @"C:\Users\alexander.hailfinger\AppData\Local\Programs\Python\Python313\python313.dll";
                PythonEngine.Initialize();
                using (Py.GIL())
                {
                    dynamic sys = Py.Import("sys");
                    sys.path.append(assemblyDirectory);

                    dynamic sysmod = Py.Import("sys");

                    // Create a delegate instance
                    LogCallback logCallback = LogFromPython;

                    // Create a delegate instance
                    DumpCallback dumpCallback = DumpFromPython;

                    LogFromPython("[TRACE] Importing Python client module");
                    dynamic clientModule = Py.Import("client");

                    LogFromPython("[TRACE] Getting Client class from client module");
                    dynamic ClientClass = clientModule.Client;

                    LogFromPython("[TRACE] Creating instance of Client class");
                    dynamic clientInstance = ClientClass();

                    clientInstance.set_log_callback(logCallback);
                    clientInstance.set_dump_callback(dumpCallback);

                    if (GrowattClientOptions is not null)
                    {
                        LogFromPython("[TRACE] Set options");
                        clientInstance.set_options(GrowattClientOptions);
                    }

                    LogFromPython("[TRACE] Starting Python client");
                    clientInstance.start();

                    LogFromPython("[TRACE] Python client started. Waiting for stop signal...");
                    _stopPythonEvent.WaitOne();

                    LogFromPython("[TRACE] Stopping Python client");
                    if (clientInstance.HasAttr("stop"))
                    {
                        clientInstance.stop();
                    }
                }
                PythonEngine.Shutdown();
                LogFromPython("[TRACE] Python runtime shutdown complete");
            }
            catch (Exception ex)
            {
                LogFromPython("[TRACE] Exception in Python thread: " + ex);
            }
        }

        // This method will be called from Python
        public void LogFromPython(string message)
        {
            Logger.LogInformation("{Message}", message);
        }

        // This method will be called from Python
        public void DumpFromPython(string topic, byte[] payload, int qos, int retain, int state, int dup, int mid)
        {
            MQTTMessage message = new MQTTMessage
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

            try
            {
                // Ensure the dump directory exists
                string dumpDirectory = Path.Combine(AppContext.BaseDirectory, "dump");
                if (!Directory.Exists(dumpDirectory))
                {
                    Directory.CreateDirectory(dumpDirectory);
                    Logger.LogInformation("[TRACE] Created dump directory at {DumpDirectory}", dumpDirectory);
                }

                // Format date and topic for filename
                DateTime dateTime = DateTimeOffset.FromUnixTimeSeconds((long)message.Timestamp).DateTime;
                string datePart = dateTime.ToString("yyyyMMdd_HHmmss");
                string topicPart = string.Join("_", message.Topic.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
                string fileName = $"{datePart}_{topicPart}.txt";
                string filePath = Path.Combine(dumpDirectory, fileName);

                // Prepare message content
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"Timestamp: {message.Timestamp}");
                sb.AppendLine($"DateTime: {dateTime:O}");
                sb.AppendLine($"Topic: {message.Topic}");
                sb.AppendLine($"Payload: {Encoding.UTF8.GetString(message.Payload)}");
                sb.AppendLine($"Qos: {message.Qos}");
                sb.AppendLine($"Retain: {message.Retain}");
                sb.AppendLine($"Mid: {message.Mid}");
                sb.AppendLine($"State: {message.State}");
                sb.AppendLine($"Dup: {message.Dup}");

                //Parse the payload of the message

                var modBusMessage = GrowattModbusMqttParser.ParseModbusMessage(message.Payload);

                if (modBusMessage != null)
                {
                    sb.AppendLine("Parsed Modbus Message:");
                    sb.AppendLine($"  Function Code: {modBusMessage.FunctionCode}");
                    sb.AppendLine($"  Data: {BitConverter.ToString(modBusMessage.Data)}");
                    sb.AppendLine($"  Address: {modBusMessage.DeviceId}");
                    sb.AppendLine($"  Raw: {BitConverter.ToString(modBusMessage.Raw)}");
                }
                else
                {
                    sb.AppendLine("No Modbus message parsed from payload.");
                } 

                // Write to file
                File.WriteAllText(filePath, sb.ToString());
            }
            catch (Exception ex)
            {
                Logger.LogError("[TRACE] Error dumping MQTT message: {Exception}", ex);
            }
        }
    }
}
