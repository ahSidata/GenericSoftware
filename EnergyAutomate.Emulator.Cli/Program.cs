// See https://aka.ms/new-console-template for more information
using EnergyAutomate.Emulator;

Console.WriteLine("EnergyAutomation Emulator Cli!");

//var device = new ShellyPro3EMDevice();
//var udpServer = new ShellyPro3EMUdpServer(1010, device); // UDP-Port wie bei Shelly-CoAP

//Task.Run(udpServer.StartAsync).Wait();

var proxy = new GrowattMqttProxy(
    proxyCertPath: "certs/server.crt",
    proxyKeyPath: "certs/server.key",
    proxyPort: 7006);

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
