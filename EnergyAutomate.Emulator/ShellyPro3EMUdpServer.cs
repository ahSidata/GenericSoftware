using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace EnergyAutomate.Emulator;

public class ShellyPro3EMUdpServer
{
    private readonly int _port;
    private readonly ShellyPro3EMDevice _device;

    public ShellyPro3EMUdpServer(int port, ShellyPro3EMDevice device)
    {
        _port = port;
        _device = device;
    }

    public static string? GetMacAddressForListener(IPAddress listenerIp)
    {
        // Alle Netzwerkinterfaces abrufen
        var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();

        foreach (var ni in networkInterfaces)
        {
            // IP-Adressen des Interfaces abrufen
            var ipProperties = ni.GetIPProperties();
            var unicastAddresses = ipProperties.UnicastAddresses;

            // Prüfen, ob die IP-Adresse des Listeners zu diesem Interface gehört
            if (unicastAddresses.Any(ua => ua.Address.Equals(listenerIp)))
            {
                // MAC-Adresse zurückgeben
                return ni.GetPhysicalAddress().ToString();
            }
        }

        return null; // Keine passende MAC-Adresse gefunden
    }

    public async Task StartAsync()
    {
        using var _udpClient = new UdpClient(_port);
        Console.WriteLine($"Shelly UDP-Server läuft auf Port {_port}");

        string ipString = "192.168.178.10";
        IPAddress ipAddress = IPAddress.Parse(ipString);
        var macAddress = GetMacAddressForListener(ipAddress).ToLower();

        while (true)
        {
            var result = await _udpClient.ReceiveAsync();
            string request = Encoding.UTF8.GetString(result.Buffer);
            string remoteIp = result.RemoteEndPoint.Address.ToString();
            int remotePort = result.RemoteEndPoint.Port;

            // Protokolliere Anfrage + Absender
            Console.WriteLine("==== UDP Anfrage erhalten ====");
            Console.WriteLine($"Von:    {remoteIp}:{remotePort}");
            Console.WriteLine("Anfrage:");
            Console.WriteLine(request);

            // Verarbeite Anfrage
            string response = _device.HandleCommand(request, macAddress);

            // Protokolliere Antwort
            Console.WriteLine("Antwort:");
            Console.WriteLine(response);
            Console.WriteLine("==== Ende ====\n");

            await Task.Delay(5000);

            // Direkt zurück an den Absender senden
            byte[] responseBytes = Encoding.UTF8.GetBytes(response);
            await _udpClient.SendAsync(responseBytes, responseBytes.Length, result.RemoteEndPoint);
        }
    }
}