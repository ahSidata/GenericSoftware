// See https://aka.ms/new-console-template for more information
using EnergyAutomate.Emulator;
using MQTTnet.Diagnostics.Logger;

// Logger erzeugen
var mqttEventLogger = new MqttNetEventLogger("MeinProxyLogger");

// Logging-Events abonnieren
mqttEventLogger.LogMessagePublished += (sender, args) =>
{
    var log = args.LogMessage;

    if (log != null)
    {
        log.Message = log.Message.Replace("\a", string.Empty);
    }

    Console.WriteLine($"[{log.Timestamp:HH:mm:ss}] [{log.Source}] [{log.Level}] {log}");
    if (log.Exception != null) Console.WriteLine(log.Exception);
};


Console.WriteLine("EnergyAutomation Emulator Cli!");

//var device = new ShellyPro3EMDevice();
//var udpServer = new ShellyPro3EMUdpServer(1010, device); // UDP-Port wie bei Shelly-CoAP

//Task.Run(udpServer.StartAsync).Wait();

var proxy = new GrowattMqttProxy(
    proxyCertPath: "certs/server.crt",
    proxyKeyPath: "certs/server.key",
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
