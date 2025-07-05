using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnergyAutomate.Emulator.Models
{
    public class GrowattClientOptions
    {
        public string? ClientId { get; set; }

        public string? BrokerHost { get; set; }
        public int BrokerPort { get; set; }

        public string? GrowattHost { get; set; }
        public int GrowattPort { get; set; }
    }
}
