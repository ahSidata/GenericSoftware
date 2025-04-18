using System;

namespace EnergyAutomate.Tibber
{
    public class TibberRealTimeMeasurement : RealTimeMeasurement
    {
        #region Public Constructors

        public TibberRealTimeMeasurement()
        { }

        public TibberRealTimeMeasurement(RealTimeMeasurement measurement)
        {
            Timestamp = measurement.Timestamp;
            TS = measurement.Timestamp.ToUniversalTime();
            Power = measurement.Power;
            LastMeterConsumption = measurement.LastMeterConsumption;
            AccumulatedConsumption = measurement.AccumulatedConsumption;
            AccumulatedConsumptionLastHour = measurement.AccumulatedConsumptionLastHour;
            AccumulatedProduction = measurement.AccumulatedProduction;
            AccumulatedProductionLastHour = measurement.AccumulatedProductionLastHour;
            AccumulatedCost = measurement.AccumulatedCost;
            AccumulatedReward = measurement.AccumulatedReward;
            Currency = measurement.Currency;
            MinPower = measurement.MinPower;
            AveragePower = measurement.AveragePower;
            MaxPower = measurement.MaxPower;
            PowerProduction = measurement.PowerProduction;
            PowerReactive = measurement.PowerReactive;
            PowerProductionReactive = measurement.PowerProductionReactive;
            MinPowerProduction = measurement.MinPowerProduction;
            MaxPowerProduction = measurement.MaxPowerProduction;
            LastMeterProduction = measurement.LastMeterProduction;
            VoltagePhase1 = measurement.VoltagePhase1;
            VoltagePhase2 = measurement.VoltagePhase2;
            VoltagePhase3 = measurement.VoltagePhase3;
            CurrentPhase1 = measurement.CurrentPhase1;
            CurrentPhase2 = measurement.CurrentPhase2;
            CurrentPhase3 = measurement.CurrentPhase3;
            PowerFactor = measurement.PowerFactor;
            SignalStrength = measurement.SignalStrength;
        }

        #endregion Public Constructors

        #region Properties

        public int? PowerAvgConsumption { get; set; }
        public int? PowerAvgProduction { get; set; }
        public int? PowerValueBattery { get; set; }
        public int? PowerValueNewCommited { get; set; }
        public string PowerValueNewDeviceSn { get; set; }
        public int? PowerValueNewRequested { get; set; }
        public int? PowerValueOutput { get; set; }
        public int? PowerValueSolar { get; set; }
        public int? PowerValueTotalCommited { get; set; }
        public int? PowerValueTotalDefault { get; set; }
        public int? PowerValueTotalRequested { get; set; }
        public bool SettingAutoMode { get; set; }
        public int? SettingAvgPowerHysteresis { get; set; }
        public bool SettingBatteryPriorityMode { get; set; }
        public int? SettingOffSetAvg { get; set; }
        public int? SettingPowerLoadSeconds { get; set; }
        public bool SettingRestrictionMode { get; set; }
        public bool SettingRestrictionState { get; set; }

        public int TotalPower => Power > 0 ? (int)Power : -(int)(PowerProduction ?? 0);

        public DateTimeOffset TS { get; set; }

        #endregion Properties
    }
}
