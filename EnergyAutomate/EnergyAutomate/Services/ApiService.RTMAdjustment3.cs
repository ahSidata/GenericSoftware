using EnergyAutomate.Definitions;
using EnergyAutomate.Extentions;

namespace EnergyAutomate.Services
{
    /// <summary>
    /// Partial class for handling real-time power adjustments based on Tibber's real-time measurements.
    /// </summary>
    public partial class ApiService
    {
        #region Private Methods

        // Diese Methode fügen Sie zur CurrentState-Klasse hinzu
        public int GetBatteryLevel()
        {
            // Aktuellen Batteriestand aus Daten ermitteln
            var lastData = GrowattLatestNoahLastDatas().FirstOrDefault();
            return lastData?.totalBatteryPackSoc ?? 0;
        }

        // Diese Methode ermittelt die erwartete Solarleistung für die nächsten Stunden
        public double GetExpectedSolarProductionForNextHours(int hours)
        {
            // Hier könnte eine Integration mit Wetterdaten erfolgen Vereinfachte Version:
            if (CurrentState.IsCloudy)
                return 0.3 * ApiSettingMaxPower * hours; // 30% bei bewölktem Wetter
            else
                return 0.7 * ApiSettingMaxPower * hours; // 70% bei sonnigem Wetter
        }

        // Diese Methode bestimmt, ob der Akku basierend auf Wetter und Preisprognose jetzt geladen
        // werden sollte
        public bool ShouldChargeBatteryNow()
        {
            // 1. Bei günstigen Strompreisen immer laden
            if (CurrentState.IsCheapRestrictionMode || CurrentState.IsBelowAvgPrice)
                return true;

            // 2. Wenn der Akku unter 50% und gutes Wetter, laden
            if (GetBatteryLevel() < 50 && !CurrentState.IsCloudy)
                return true;

            // 3. Wenn der erwartete Solarertrag niedrig ist und der Akku nicht voll, laden
            if (GetBatteryLevel() < 95 && GetExpectedSolarProductionForNextHours(6) < ApiSettingAvgPower * 3)
                return true;

            return false;
        }

        /// <summary>
        /// Adjusts power distribution based on various conditions following the documented decision hierarchy.
        /// </summary>
        /// <param name="value">The real-time measurement data from Tibber.</param>
        private async Task TibberRTMAdjustment3(TibberRealTimeMeasurement value)
        {
            // 1. Auto Mode Check (Highest Priority)
            if (ApiSettingAutoMode)
            {
                Logger.LogInformation("Auto mode active: Using intelligent power adjustment");
                ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 1001, Key = "ActiveRTMMode", Value = "ForceAuto" });
                await TibberRTMAdjustment3AutoMode(value);
                return;
            }

            // 2. Inactivity Check
            var effectiveSolarPower = CurrentState.GrowattNoahGetAvgPpvLast5Minutes();
            if (effectiveSolarPower <= 0 && CurrentState.IsGrowattBatteryEmpty)
            {
                Logger.LogInformation("No PV power available and battery empty. No action required.");
                ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 1001, Key = "ActiveRTMMode", Value = "Inactive" });
                return;
            }

            // 3. Mode Determination
            bool isExtensionModeActive = ApiSettingExtentionMode &&
                                        (DateTime.UtcNow.TimeOfDay < ApiSettingExtentionExclusionFrom ||
                                         DateTime.UtcNow.TimeOfDay > ApiSettingExtentionExclusionUntil);

            if (isExtensionModeActive)
            {
                ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 1001, Key = "ActiveRTMMode", Value = "Extension" });
                await HandleExtensionMode(value);
            }
            else
            {
                ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 1001, Key = "ActiveRTMMode", Value = "Mormal" });
                await HandleNormalMode(value, effectiveSolarPower);
            }
        }

        /// <summary>
        /// Handles Extension Mode logic per the documented decision flow
        /// </summary>
        private async Task HandleExtensionMode(TibberRealTimeMeasurement value)
        {
            // Priority Sequence for Extension Mode

            // 1. Power Price Check
            if (CurrentState.IsCheapRestrictionMode)
            {
                LoggerRTM.LogInformation("Extension Mode: Cheap electricity prices, charging battery");
                await TibberRTMDefaultBatteryPriorityAsync(value);
                return;
            }

            if (CurrentState.IsExpensiveRestrictionMode || !CurrentState.IsBelowAvgPrice)
            {
                LoggerRTM.LogInformation("Extension Mode: Expensive electricity prices, activating energy saving mode");
                await TibberRTMAdjustment3AutoMode(value);
                return;
            }

            // 2. Battery Level Based Decisions
            int batteryLevel = GetBatteryLevel();
            if (batteryLevel > 50)
            {
                LoggerRTM.LogInformation("Extension Mode: High battery level, maximizing load operation");
                await TibberRTMDefaultLoadPriorityMaxAsync(value, GrowattGetDeviceNoahSnList());
            }
            else if (batteryLevel > 20)
            {
                LoggerRTM.LogInformation("Extension Mode: Medium battery level, activating energy saving mode");
                await TibberRTMAdjustment3AutoMode(value);
            }
            else
            {
                LoggerRTM.LogInformation("Extension Mode: Low battery level, running with average load");
                await TibberRTMDefaultLoadPriorityAvgAsync(value);
            }
        }

        private async Task HandleNormalMode(TibberRealTimeMeasurement value, double effectiveSolarPower)
        {
            // Priority Sequence for Normal Mode per documentation

            // Special case from original code (not explicitly in docs but in original code)
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

            // 2. Battery Charging Prioritization
            int batteryLevel = GetBatteryLevel();
            bool poorWeatherForecast = CurrentState.IsCloudy;
            bool lowBatteryLevel = batteryLevel < 30;
            bool cheapPrices = CurrentState.IsCheapRestrictionMode;

            if (poorWeatherForecast ||
                (lowBatteryLevel && cheapPrices) ||
                (lowBatteryLevel && poorWeatherForecast) ||
                (batteryLevel < 80 && !poorWeatherForecast))  // Battery < 80% with good weather
            {
                LoggerRTM.LogInformation("Normal Mode: Conditions favorable for battery charging");
                if (poorWeatherForecast) LoggerRTM.LogInformation("- Poor weather forecast");
                if (lowBatteryLevel && cheapPrices) LoggerRTM.LogInformation("- Low battery + cheap prices");
                if (lowBatteryLevel && poorWeatherForecast) LoggerRTM.LogInformation("- Low battery + poor weather");
                if (batteryLevel < 80 && !poorWeatherForecast) LoggerRTM.LogInformation("- Battery < 80% with good weather");

                await TibberRTMDefaultBatteryPriorityAsync(value);
                return;
            }

            // Special case: Full battery (from original code)
            if (CurrentState.IsGrowattBatteryFull)
            {
                if (effectiveSolarPower > ApiSettingMaxPower * 0.75)
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

            // 3. Default Fallback - when no other condition applies
            LoggerRTM.LogInformation("Normal Mode: No specific conditions met, using auto mode for optimal distribution");
            await TibberRTMAdjustment3AutoMode(value);
        }

        /// <summary>
        /// Handles automatic power adjustment when in auto mode or under expensive restriction mode.
        /// </summary>
        /// <param name="value">The real-time measurement data from Tibber.</param>
        private async Task TibberRTMAdjustment3AutoMode(TibberRealTimeMeasurement value)
        {
            // Check and clear conditions for load priority adjustment.
            await TibberRTMCheckConditionAsync("LoadPriority_SetPower", [ new( async () =>
                    {
                        // Clear all time segments for Noah devices.
                        await GrowattClearAllDeviceNoahTimeSegments();
                    }, () =>
                    {
                        // Check if any time segments are enabled.
                        var anyEnabledTimesegments = GrowattLatestNoahInfoDatas().Any(x => x!.TimeSegments.Any(x => x.Enable == "1"));
                        return Task.FromResult(!anyEnabledTimesegments);
                    })
            ]);

            // Adjust power distribution.
            await TibberRTMAdjustment3SetPower(value);
        }

        /// <summary>
        /// Adjusts power distribution based on real-time measurements, ensuring power limits and
        /// hysteresis are respected.
        /// </summary>
        /// <param name="value">The real-time measurement data from Tibber.</param>
        private async Task TibberRTMAdjustment3SetPower(TibberRealTimeMeasurement value)
        {
            // Log the start of the power adjustment process.
            LoggerRTM.LogTrace("Starting TibberRTMAdjustment3SetPower. TotalPower: {TotalPower}, ApiSettingAvgPower: {TargetPower}, ApiSettingAvgPowerOffset: {TargetOffset}, ApiSettingMaxPower: {MaxPower}",
                value.TotalPower, ApiSettingAvgPower, ApiSettingAvgPowerOffset, ApiSettingMaxPower);

            // Handle wait cycles for power adjustment.
            if (_adjustmentWaitCycles < ApiSettingPowerAdjustmentWaitCycles)
            {
                _adjustmentWaitCycles++;
                LoggerRTM.LogTrace("Wait because WaitCycles: {WaitCycles} < {PowerAdjustmentWaitCycles}",
                    _adjustmentWaitCycles, ApiSettingPowerAdjustmentWaitCycles);
                return;
            }

            // Reset wait cycles after the required number of cycles.
            _adjustmentWaitCycles = 0;
            LoggerRTM.LogTrace("Reset adjustment wait cycles to 0.");

            // Get the committed power value, ensuring it's non-negative.
            var powerValueTotalCommited = Math.Max(0, CurrentState?.PowerValueTotalCommited ?? 0);

            // Calculate the delta and limits for power adjustment.
            int deltaTotalPower = value.TotalPower - ApiSettingAvgPowerOffset;
            var upperLimit = ApiSettingAvgPowerOffset + (ApiSettingAvgPowerHysteresis / 2);
            var lowerLimit = ApiSettingAvgPowerOffset - (ApiSettingAvgPowerHysteresis / 2);

            LoggerRTM.LogTrace("Calculated limits. DeltaTotalPower: {DeltaTotalPower}, UpperLimit: {UpperLimit}, LowerLimit: {LowerLimit}, CommitedTotalPower: {CommitedTotalPower}",
                deltaTotalPower, upperLimit, lowerLimit, powerValueTotalCommited);

            // Get the list of online devices.
            var onlineDevices = GrowattGetDevicesNoahOnline();
            if (onlineDevices == null || !onlineDevices.Any())
            {
                LoggerRTM.LogTrace("No devices available for adjustment.");
                return;
            }

            LoggerRTM.LogTrace("Found {DeviceCount} online devices", onlineDevices.Count);
            int maxPowerPerDevice = onlineDevices.Count > 0 ? ApiSettingMaxPower / onlineDevices.Count : 0;

            // Handle cases where the total power exceeds the maximum allowed power.
            if (Math.Abs(value.TotalPower) > ApiSettingMaxPower)
            {
                if (value.TotalPower < 0)
                {
                    // If the total power is negative and exceeds the limit, set all devices to 0.
                    LoggerRTM.LogTrace("Negative TotalPower ({TotalPower}) exceeds ApiSettingMaxPower ({MaxPower}). Setting all device power values to 0 W.",
                        value.TotalPower, ApiSettingMaxPower);

                    await DistributionManager.SetAllDevicesToPower(onlineDevices, 0, value.TS);
                    return;
                }
                if (value.TotalPower > 0)
                {
                    // If the total power is positive and exceeds the limit, distribute power evenly.
                    LoggerRTM.LogTrace("Positive TotalPower ({TotalPower}) exceeds ApiSettingMaxPower ({MaxPower}). Setting all device power values to {PowerPerDevice} W.",
                    value.TotalPower, ApiSettingMaxPower, maxPowerPerDevice);

                    await DistributionManager.SetAllDevicesToPower(onlineDevices, maxPowerPerDevice, value.TS);
                    return;
                }
            }

            // Handle cases where the total power is within the hysteresis range.
            if (value.TotalPower >= lowerLimit && value.TotalPower <= upperLimit)
            {
                LoggerRTM.LogTrace("TotalPower within hysteresis range ({Lower} to {Upper}). Performing load balancing.",
                    lowerLimit, upperLimit);
                await DistributionManager.PerformLoadBalancing(onlineDevices, value.TS);
                return;
            }

            // Adjust power based on the delta value.
            double adjustmentFactor = ApiSettingPowerAdjustmentFactor / 100.0;
            int adjustedDelta = (int)(deltaTotalPower * adjustmentFactor);
            LoggerRTM.LogTrace("Adjusted delta (after adjustment factor {Factor}%): {AdjustedDelta}",
                ApiSettingPowerAdjustmentFactor, adjustedDelta);

            if (deltaTotalPower > 0)
            {
                // If the delta is positive, increase the total power.
                int desiredTotalPower = Math.Min(ApiSettingMaxPower, powerValueTotalCommited + adjustedDelta);

                LoggerRTM.LogTrace("Positive delta detected. Change total power from {CurrentPower} to {DesiredPower} based on adjusted delta {AdjustedDelta}\"",
                    powerValueTotalCommited, desiredTotalPower, adjustedDelta);

                LoggerRTM.LogTrace("Distributing total power of {TotalPower}W with high SoC prioritization", desiredTotalPower);
                await DistributionManager.DistributePower(onlineDevices, desiredTotalPower, prioritizeHighSoc: true, value.TS);
            }
            else if (deltaTotalPower < 0)
            {
                // If the delta is negative, decrease the total power.
                int desiredTotalPower = Math.Max(0, powerValueTotalCommited + adjustedDelta);

                LoggerRTM.LogTrace("Negative delta detected. Change total power from {CurrentPower} to {DesiredPower} based on adjusted delta {AdjustedDelta}",
                    powerValueTotalCommited, desiredTotalPower, adjustedDelta);

                LoggerRTM.LogTrace("Distributing total power of {TotalPower}W with low SoC prioritization", desiredTotalPower);
                await DistributionManager.DistributePower(onlineDevices, desiredTotalPower, prioritizeHighSoc: false, value.TS);
            }
            else
            {
                // If the delta is zero, maintain the current power or perform load balancing.
                LoggerRTM.LogTrace("Delta is exactly 0. Performing load balancing.");
                await DistributionManager.PerformLoadBalancing(onlineDevices, value.TS);
            }

            LoggerRTM.LogTrace("Power adjustment procedure completed.");
        }

        #endregion Private Methods
    }
}
