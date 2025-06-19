using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnergyAutomate.Emulator
{
    public class GrowattClientOptions
    {
        public string? Host { get; set; }
        public int Port { get; set; }

        public override string ToString()
        {
            return $"Person(Host={Host}, Port={Port})";
        }
    }
}
