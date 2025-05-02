using EnergyAutomate.Definitions;
using EnergyAutomate.Extentions;

namespace EnergyAutomate.Services
{
    /// <summary>
    /// Partial class for handling real-time power adjustments based on Tibber's real-time measurements.
    /// </summary>
    public partial class ApiService
    {
        #region Fields

        private const int CRITICAL_BATTERY_THRESHOLD = 20;
        private const int FORECAST_HOURS = 6;
        private const int HIGH_BATTERY_THRESHOLD = 80;
        private const double HIGH_SOLAR_RATIO = 0.75;
        private const int LOW_BATTERY_THRESHOLD = 30;
        private const int MEDIUM_BATTERY_THRESHOLD = 50;
        private const double VERY_HIGH_SOLAR_RATIO = 0.8;

        #endregion Fields

        #region Private Methods

        /// <summary>
        /// Adjusts power distribution based on various conditions following the documented decision hierarchy.
        /// </summary>
        /// <param name="value">The real-time measurement data from Tibber.</param>
        private async Task TibberRTMAdjustment3(TibberRealTimeMeasurement value)
        {
            // 1. Auto Mode Check (Highest Priority)
            if (ApiSettingAutoMode)
            {
                LoggerRTM.LogInformation("Auto mode active: Using intelligent power adjustment");
                ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 1001, Key = "TibberAdjustment3Mode", Value = "ForceAuto" });
                await TibberRTMAdjustment3AutoMode(value);
                return;
            }

            // Cache common values to avoid redundant calculations
            var effectiveSolarPower = CurrentState.GrowattNoahGetAvgPpvLast5Minutes();
            var batteryLevel = GrowattGetBatteryLevel();
            var isBatteryEmpty = CurrentState.IsGrowattBatteryEmpty;
            var isBatteryFull = CurrentState.IsGrowattBatteryFull;

            // 2. Inactivity Check
            if (effectiveSolarPower <= 0 && isBatteryEmpty)
            {
                LoggerRTM.LogInformation("No PV power available and battery empty. No action required.");
                ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 1001, Key = "TibberAdjustment3Mode", Value = "Inactive" });
                return;
            }

            // 3. Mode Determination
            bool isExtensionModeActive = TibberRTMAdjustment3IsExtensionModeActive();

            // Log current system state for better diagnostics
            LoggerRTM.LogInformation("System state: Battery: {0}%, Solar: {1}W, Mode: {2}",
                batteryLevel, effectiveSolarPower, isExtensionModeActive ? "Extension" : "Normal");

            if (isExtensionModeActive)
            {
                ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 1001, Key = "TibberAdjustment3Mode", Value = "Extention" });
                await TibberRTMAdjustment3HandleExtensionMode(value, batteryLevel);
            }
            else
            {
                ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 1001, Key = "TibberAdjustment3Mode", Value = "Auto" });
                await TibberRTMAdjustment3HandleNormalMode(value, effectiveSolarPower, batteryLevel, isBatteryFull);
            }
        }

        /// <summary>Handles Extension Mode logic per the documented decision flow</summary>
        private async Task TibberRTMAdjustment3HandleExtensionMode(TibberRealTimeMeasurement value, int batteryLevel)
        {
            // Priority Sequence for Extension Mode

            // 1. Power Price Check - highest priority
            if (CurrentState.IsCheapRestrictionMode)
            {
                LoggerRTM.LogInformation("Extension Mode: Cheap electricity prices, charging battery");
                await TibberRTMDefaultBatteryPriorityAsync(value);
                return;
            }

            if (CurrentState.IsExpensiveRestrictionMode)
            {
                LoggerRTM.LogInformation("Extension Mode: Expensive electricity prices, activating energy saving mode");
                await TibberRTMAdjustment3AutoMode(value);
                return;
            }

            // 2. Morning Price Forecast
            if (TibberRTMAdjustment3IsMorningApproaching() && TibberRTMAdjustment3ShouldPreserveBatteryForMorning())
            {
                LoggerRTM.LogInformation("Extension Mode: Approaching morning hours with expected high prices, preserving battery capacity");
                await TibberRTMDefaultLoadPriorityAvgAsync(value);
                return;
            }

            // 3. Battery Level Based Decisions
            if (batteryLevel > MEDIUM_BATTERY_THRESHOLD)
            {
                LoggerRTM.LogInformation($"Extension Mode: High battery level ({batteryLevel}%), maximizing load operation");
                await TibberRTMDefaultLoadPriorityMaxAsync(value, GrowattGetDeviceNoahSnList());
            }
            else if (batteryLevel > CRITICAL_BATTERY_THRESHOLD)
            {
                LoggerRTM.LogInformation($"Extension Mode: Medium battery level ({batteryLevel}%), activating energy saving mode");
                await TibberRTMAdjustment3AutoMode(value);
            }
            else
            {
                LoggerRTM.LogInformation($"Extension Mode: Low battery level ({batteryLevel}%), running with average load");
                await TibberRTMDefaultLoadPriorityAvgAsync(value);
            }
        }

        /// <summary>Handles Normal Mode logic per the documented decision flow</summary>
        private async Task TibberRTMAdjustment3HandleNormalMode(TibberRealTimeMeasurement value, double effectiveSolarPower, int batteryLevel, bool isBatteryFull)
        {
            // Special case from original code
            if (CurrentState.IsExpensiveRestrictionMode)
            {
                LoggerRTM.LogInformation("Normal Mode: Expensive electricity prices, activating energy saving mode");
                await TibberRTMAdjustment3AutoMode(value);
                return;
            }

            // 1. Direct Solar Usage Optimization
            if (!CurrentState.IsCheapRestrictionMode)
            {
                LoggerRTM.LogInformation("Normal Mode: Not in cheap price mode, prioritizing direct solar usage");
                await TibberRTMDefaultLoadPrioritySolarInputAsync(value);
                return;
            }

            // Special case: Full battery
            if (isBatteryFull)
            {
                if (effectiveSolarPower > ApiSettingMaxPower * HIGH_SOLAR_RATIO)
                {
                    LoggerRTM.LogInformation($"Normal Mode: Battery full with high solar power ({effectiveSolarPower}W), maximizing consumption");
                    await TibberRTMDefaultLoadPriorityMaxAsync(value, GrowattGetDeviceNoahSnList());
                }
                else
                {
                    LoggerRTM.LogInformation("Normal Mode: Battery full, standard load operation");
                    await TibberRTMDefaultLoadPriorityAvgAsync(value);
                }
                return;
            }

            // 2. Battery Charging Prioritization
            await TibberRTMAdjustment3HandleBatteryChargingDecisions(value, effectiveSolarPower, batteryLevel);
        }

        #endregion Private Methods
    }
}
