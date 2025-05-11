using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace EnergyAutomate.Tibber
{
    public class SmlParser
    {
        // OBIS-Kennung für Wirkarbeit Bezug (1-0:1.8.0*255) in SML
        private readonly byte[] OBIS_1_8_0 = new byte[] { 0x01, 0x00, 0x01, 0x08, 0x00, 0xFF };
        private readonly string _tibberBridgePassword;
        private readonly string _tibberHost;

        public SmlParser(string tibberHost, string tibberBridgePassword)
        {
            _tibberBridgePassword = tibberBridgePassword;
            _tibberHost = tibberHost;
        }

        public async Task GetNodeData(RealTimeMeasurement realTimeMeasurement)
        {

            Console.WriteLine($"Tibber Bridge SML Parser Current Power Value: {realTimeMeasurement?.Power}");
            Console.WriteLine($"Tibber Bridge SML Parser Current PowerProduction Value: {realTimeMeasurement?.PowerProduction}");

            using var client = new HttpClient();

            var url = $"http://{_tibberHost}/data.json?node_id=1";

            // Basic Auth Header zusammenbauen
            var authToken = Encoding.ASCII.GetBytes($"admin:{_tibberBridgePassword}");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authToken));

            try
            {
                var response = await client.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    byte[] data = await response.Content.ReadAsByteArrayAsync();

                    // Optional: Hexdump der Rohdaten
                    Console.WriteLine(BitConverter.ToString(data));

                    // SML-Parser aufrufen
                    double? value = GetTotalImportKwh(data);
                    if (value != null)
                        Console.WriteLine($"Zählerstand: {value} kWh");
                    else
                        Console.WriteLine("Kein Zählerstand gefunden.");

                    // SML-Parser aufrufen
                    double? currentPowerWatt = GetCurrentPowerWatt(data);
                    if (currentPowerWatt != null)
                        Console.WriteLine($"CurrentPowerWatt: {currentPowerWatt} kWh");
                    else
                        Console.WriteLine("Kein Zählerstand gefunden.");
                }
                else
                {
                    Console.WriteLine("Fehler: " + response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler: {ex.Message}");
            }
        }

        public static double? GetTotalImportKwh(byte[] smlData)
        {
            byte[] obis = { 0x01, 0x00, 0x01, 0x08, 0x00, 0xFF };
            for (int i = 0; i < smlData.Length - obis.Length; i++)
            {
                if (smlData.Skip(i).Take(obis.Length).SequenceEqual(obis))
                {
                    for (int j = i + obis.Length; j < smlData.Length - 8; j++)
                    {
                        if (smlData[j] == 0x62 && smlData[j + 1] == 0x1E)
                        {
                            uint value = 0;
                            for (int k = 0; k < 4; k++)
                                value = (value << 8) | smlData[j + 2 + k];
                            return value / 1000.0; // Wh zu kWh
                        }
                    }
                }
            }
            return null;
        }

        public static int? GetCurrentPowerWatt(byte[] smlData)
        {
            byte[] obis = { 0x01, 0x00, 0x10, 0x07, 0x00, 0xFF };
            for (int i = 0; i < smlData.Length - obis.Length - 12; i++)
            {
                if (smlData.Skip(i).Take(obis.Length).SequenceEqual(obis))
                {
                    int j = i + obis.Length;
                    // Nach OBIS: 2x 0x01, dann 0x62 0x1B, dann 8 Bytes Wert
                    if (smlData[j] == 0x01 && smlData[j + 1] == 0x01 && smlData[j + 2] == 0x62 && smlData[j + 3] == 0x1B)
                    {
                        // 8 Bytes Wert ab j+4
                        // Momentanwert ist an Offset [j+4+4] (low) und [j+4+5] (high), also Bytes 4 und 5 im Datenblock
                        int value = smlData[j + 8] | (smlData[j + 7] << 8);
                        // Wertebereich filtern (optional): z.B. 0 ... 20000
                        if (value >= 0 && value < 20000)
                            return value;
                    }
                }
            }
            return null;
        }
    }
}