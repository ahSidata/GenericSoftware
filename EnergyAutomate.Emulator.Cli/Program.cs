// See https://aka.ms/new-console-template for more information
using EnergyAutomate.Emulator;

Console.WriteLine("EnergyAutomation Emulator Cli!");

var device = new ShellyPro3EMDevice();
var udpServer = new ShellyPro3EMUdpServer(1010, device); // UDP-Port wie bei Shelly-CoAP

Task.Run(udpServer.StartAsync).Wait();
