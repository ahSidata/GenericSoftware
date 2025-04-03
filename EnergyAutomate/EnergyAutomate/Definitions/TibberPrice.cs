using Tibber.Sdk;

namespace EnergyAutomate
{
    public class TibberPrice
    {
        #region Properties

        public Guid Id { get; set; } = Guid.NewGuid();
        public bool? AutoModeRestriction { get; set; }
        public PriceLevel? Level { get; set; }
        public DateTimeOffset StartsAt { get; set; }
        public decimal? Total { get; set; }

        #endregion Properties
    }
}
