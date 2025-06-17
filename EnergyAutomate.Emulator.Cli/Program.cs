using EnergyAutomate.Emulator;
using MQTTnet.Diagnostics.Logger;
using System.Net.Security;
using System.Net.Sockets;
using System.Net;
using System.Runtime.ConstrainedExecution;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace EnergyAutomate.Emulator.Cli
{
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            AppContext.SetSwitch("System.Net.EnableSslKeyLogging", true);

            Console.WriteLine("[TRACE] Program start");
            Console.WriteLine("[TRACE] .NET Version: " + Environment.Version);
            string keylogPath = Environment.GetEnvironmentVariable("SSLKEYLOGFILE");
            Console.WriteLine("[TRACE] SSLKEYLOGFILE: " + (keylogPath ?? "(not set)"));

            if (!string.IsNullOrWhiteSpace(keylogPath))
            {
                try
                {
                    File.AppendAllText(keylogPath, "[TRACE] Test write: " + DateTime.Now + Environment.NewLine);
                    Console.WriteLine("[TRACE] Test line written to SSLKEYLOGFILE");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[TRACE] Could not write to SSLKEYLOGFILE: " + ex);
                }
            }

            //await TCPAsync();
            await MQTTAsync();
        }

        private static async Task TCPAsync()
        {
            int localPort = 7006;
            string remoteHost = "mqtt.growatt.com";
            int remotePort = 7006;

            var cert = X509Certificate2.CreateFromPemFile("certs/server.crt", "certs/server.key");

            var listener = new TcpListener(IPAddress.Any, localPort);
            listener.Start();
            Console.WriteLine($"Listening on port {localPort}...");

            while (true)
            {
                var client = await listener.AcceptTcpClientAsync();
                _ = HandleClientAsync(client, remoteHost, remotePort, cert);
            }
        }

        private static async Task HandleClientAsync(TcpClient client, string remoteHost, int remotePort, X509Certificate2 cert)
        {
            Console.WriteLine("Client connected.");

            using var clientStream = client.GetStream();
            using var sslClient = new SslStream(clientStream, false);
            await sslClient.AuthenticateAsServerAsync(cert, clientCertificateRequired: false, enabledSslProtocols: System.Security.Authentication.SslProtocols.Tls12, checkCertificateRevocation: false);

            // Connect to real broker
            using var broker = new TcpClient();
            await broker.ConnectAsync(remoteHost, remotePort);
            using var brokerStream = broker.GetStream();
            using var sslBroker = new SslStream(brokerStream, false, (sender, cert, chain, errors) => true); // skip validation (can be hardened)

            await sslBroker.AuthenticateAsClientAsync(remoteHost);

            // Proxy both directions
            var t1 = PumpAsync(sslClient, sslBroker, "Client → Broker");
            var t2 = PumpAsync(sslBroker, sslClient, "Broker → Client");

            await Task.WhenAny(t1, t2);
            Console.WriteLine("Connection closed.");
        }

        private static async Task PumpAsync(Stream input, Stream output, string direction)
        {
            var buffer = new byte[8192];
            try
            {
                while (true)
                {
                    int bytesRead = await input.ReadAsync(buffer.AsMemory(0, buffer.Length));
                    if (bytesRead == 0) break;

                    // Optional: hier kannst du MQTT-Pakete inspizieren
                    Console.WriteLine($"{direction}: {bytesRead} bytes");

                    await output.WriteAsync(buffer.AsMemory(0, bytesRead));
                    await output.FlushAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{direction} failed: {ex.Message}");
            }
        }

        private static async Task MQTTAsync()
        {
            // Logger erzeugen
            var mqttEventLogger = new MqttNetEventLogger("MeinProxyLogger");

            // Logging-Events abonnieren (optional)
            //mqttEventLogger.LogMessagePublished += (sender, eventArgs) =>
            //{
            //    var log = eventArgs.LogMessage;
            //    if (log != null)
            //    {
            //        log.Message = log.Message.Replace("\a", string.Empty);
            //    }
            //    Console.WriteLine($"[{log.Timestamp:HH:mm:ss}] [{log.Source}] [{log.Level}] {log}");
            //    if (log.Exception != null) Console.WriteLine(log.Exception);
            //};

            Console.WriteLine("EnergyAutomation Emulator Cli!");

            var proxy = new GrowattMqttProxy(
                proxyCertPath: "certs/server.crt",
                proxyKeyPath: "certs/server.key",
                proxyHost: "ah.azure.sidata.com",
                proxyPort: 7006,
                mqttNetEventLogger: mqttEventLogger);

            await proxy.StartAsync();

           Console.WriteLine("Press Ctrl+C to exit.");

            var exitEvent = new TaskCompletionSource();
            Console.CancelKeyPress += (sender, e) =>
            {
                Console.WriteLine("Exiting...");
                exitEvent.SetResult();
                e.Cancel = true;
            };
            await exitEvent.Task;
        }

        private static async Task DumpAsync()
        {
            // Logger erzeugen
            var mqttEventLogger = new MqttNetEventLogger("MeinProxyLogger");

            Console.WriteLine("EnergyAutomation Emulator Cli!");

            var growattMqttClient = new GrowattMqttClient("mqtt.growatt.com", 7006, "0PVP5ZR23CT00V4", "0PVP5ZR23CT00V4", "Growatt", mqttEventLogger);

            await growattMqttClient.ConnectAsync();

            while (!growattMqttClient.IsConnected)
            {
                await Task.Delay(1000);
            }

            Console.WriteLine("Press Ctrl+C to exit.");

            var exitEvent = new TaskCompletionSource();
            Console.CancelKeyPress += (sender, e) =>
            {
                Console.WriteLine("Exiting...");
                exitEvent.SetResult();
                e.Cancel = true;
            };
            await exitEvent.Task;
        }
    }
}
