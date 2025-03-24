using Tibber.Sdk;

namespace EnergyAutomate
{
    public class ApiPrice
    {
        #region Properties

        public bool? AutoModeRestriction { get; set; }
        public PriceLevel? Level { get; set; }
        public DateTime StartsAt { get; set; }
        public decimal? Total { get; set; }

        #endregion Properties
    }
}
