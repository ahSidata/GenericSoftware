using System;

namespace EnergyAutomate.Tibber
{
    public class TibberPrice
    {
        #region Properties

        public bool? AutoModeRestriction { get; set; }
        public Guid Id { get; set; } = Guid.NewGuid();
        public PriceLevel? Level { get; set; }
        public DateTimeOffset StartsAt { get; set; }
        public decimal? Total { get; set; }

        #endregion Properties
    }
}
