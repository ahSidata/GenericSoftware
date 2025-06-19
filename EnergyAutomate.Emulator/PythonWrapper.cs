using Python.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnergyAutomate.Emulator
{
    public class PythonWrapper
    {
        public GrowattClientOptions? GrowattClientOptions { get; set; }

        // Define the delegate type for callbacks from Python
        public delegate void LogCallback(string message);

        private Thread? _pythonThread;
        private AutoResetEvent _stopPythonEvent = new AutoResetEvent(false);

        public void StartPythonClient()
        {
            Console.WriteLine("[TRACE] Starting Python background thread");
            _pythonThread = new Thread(RunPythonClient)
            {
                IsBackground = true
            };
            _pythonThread.Start();


            Console.WriteLine("[TRACE] Python client running in background. Press ENTER to stop...");
            Console.ReadLine();

            Console.WriteLine("[TRACE] Signaling Python thread to stop");
            _stopPythonEvent.Set();
            _pythonThread.Join();
        }

        private void RunPythonClient()
        {
            try
            {
                Console.WriteLine("[TRACE] Initializing Python runtime in background thread");
                Python.Runtime.Runtime.PythonDLL = @"C:\Users\alexander.hailfinger\AppData\Local\Programs\Python\Python313\python313.dll";
                PythonEngine.Initialize();
                using (Py.GIL())
                {
                    dynamic sys = Py.Import("sys");
                    sys.path.append(@"E:\VisualStudio\Repos\GenericSoftware\EnergyAutomate.Emulator.Cli");

                    Console.WriteLine("[TRACE] Configuring Python logging");
                    dynamic logging = Py.Import("logging");
                    dynamic sysmod = Py.Import("sys");
                    logging.basicConfig(
                        level: logging.DEBUG,
                        format: "%(asctime)s [%(levelname)s] %(name)s: %(message)s",
                        stream: sysmod.stdout
                    );

                    // Create a delegate instance
                    LogCallback logCallback = LogFromPython;

                    Console.WriteLine("[TRACE] Importing Python client module");
                    dynamic clientModule = Py.Import("client");

                    Console.WriteLine("[TRACE] Getting Client class from client module");
                    dynamic ClientClass = clientModule.Client;

                    Console.WriteLine("[TRACE] Creating instance of Client class");
                    dynamic clientInstance = ClientClass(logCallback);

                    if (GrowattClientOptions is not null)
                    {
                        Console.WriteLine("[TRACE] Set options");
                        clientInstance.set_options(GrowattClientOptions);
                    }

                    Console.WriteLine("[TRACE] Starting Python client");
                    clientInstance.start();

                    Console.WriteLine("[TRACE] Python client started. Waiting for stop signal...");
                    _stopPythonEvent.WaitOne();

                    Console.WriteLine("[TRACE] Stopping Python client");
                    if (clientInstance.HasAttr("stop"))
                    {
                        clientInstance.stop();
                    }
                }
                PythonEngine.Shutdown();
                Console.WriteLine("[TRACE] Python runtime shutdown complete");
            }
            catch (Exception ex)
            {
                Console.WriteLine("[TRACE] Exception in Python thread: " + ex);
            }
        }

        // This method will be called from Python
        public void LogFromPython(string message)
        {
            Console.WriteLine($"[PYTHON LOG] {message}");
        }
    }
}
