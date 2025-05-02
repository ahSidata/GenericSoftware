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
        /// Adjusts power distribution based on various conditions such as battery state, weather,
        /// and power restrictions.
        /// </summary>
        /// <param name="value">The real-time measurement data from Tibber.</param>
        private async Task TibberRTMAdjustment3(TibberRealTimeMeasurement value)
        {
            // 1. Automodus-Prüfung
            if (ApiSettingAutoMode)
            {
                await TibberRTMAdjustment3AutoMode(value);
                return;
            }

            // 2. Inaktivitätsprüfung
            var effectiveSolarPower = CurrentState.GrowattNoahGetAvgPpvLast5Minutes();
            if (effectiveSolarPower <= 0 && CurrentState.IsGrowattBatteryEmpty)
            {
                LoggerRTM.LogInformation("Keine PV-Leistung verfügbar und Batterie leer. Keine Aktion erforderlich.");
                return;
            }

            // 3. Betriebsmodus-Ermittlung
            bool isExtensionModeActive = ApiSettingExtentionMode &&
                                         (DateTime.Now.TimeOfDay < ApiSettingExtentionExclusionFrom ||
                                          DateTime.Now.TimeOfDay > ApiSettingExtentionExclusionUntil);

            if (isExtensionModeActive)
            {
                // 4. ExtensionMode-Entscheidungen
                if (CurrentState.IsCheapRestrictionMode)
                {
                    LoggerRTM.LogInformation("ExtensionMode aktiv: Günstige Strompreise, Batterie wird geladen.");
                    await TibberRTMDefaultBatteryPriorityAsync(value);
                }
                else if (CurrentState.IsExpensiveRestrictionMode)
                {
                    LoggerRTM.LogInformation("ExtensionMode aktiv: Teure Strompreise, Energiesparmodus wird aktiviert.");
                    await TibberRTMAdjustment3AutoMode(value);
                }
                else
                {
                    int batteryLevel = GetBatteryLevel();
                    if (batteryLevel > 50)
                    {
                        LoggerRTM.LogInformation("ExtensionMode aktiv: Hoher Akkustand, Maximal-Lastbetrieb.");
                        await TibberRTMDefaultLoadPriorityMaxAsync(value, GrowattGetDeviceNoahSnList());
                    }
                    else if (batteryLevel > 20)
                    {
                        LoggerRTM.LogInformation("ExtensionMode aktiv: Mittlerer Akkustand, Energiesparmodus.");
                        await TibberRTMAdjustment3AutoMode(value);
                    }
                    else
                    {
                        LoggerRTM.LogInformation("ExtensionMode aktiv: Niedriger Akkustand, Durchschnittslastbetrieb.");
                        await TibberRTMDefaultLoadPriorityAvgAsync(value);
                    }
                }
                return;
            }

            // 5. Normaler Modus-Entscheidungen
            if (CurrentState.IsExpensiveRestrictionMode)
            {
                LoggerRTM.LogInformation("Normaler Modus: Teure Strompreise, Energiesparmodus wird aktiviert.");
                await TibberRTMAdjustment3AutoMode(value);
                return;
            }

            if (CurrentState.IsGrowattBatteryFull)
            {
                if (effectiveSolarPower > ApiSettingMaxPower * 0.75)
                {
                    LoggerRTM.LogInformation($"Normaler Modus: Akku voll und hohe Solarleistung ({effectiveSolarPower}W), Maximaler Verbrauch.");
                    await TibberRTMDefaultLoadPriorityMaxAsync(value, GrowattGetDeviceNoahSnList());
                }
                else
                {
                    LoggerRTM.LogInformation("Normaler Modus: Akku voll, Standard-Lastbetrieb.");
                    await TibberRTMDefaultLoadPriorityAvgAsync(value);
                }
                return;
            }

            if (effectiveSolarPower < ApiSettingAvgPower * 0.5)
            {
                if (GetBatteryLevel() < 30 && CurrentState.IsBelowAvgPrice)
                {
                    LoggerRTM.LogInformation("Normaler Modus: Niedrige Solarleistung, niedriger Akkustand, günstige Preise. Batterie wird geladen.");
                    await TibberRTMDefaultBatteryPriorityAsync(value);
                }
                else if (CurrentState.IsGrowattBatteryEmpty)
                {
                    LoggerRTM.LogInformation("Normaler Modus: Batterie leer, Last wird reduziert.");
                    await TibberRTMDefaultLoadPriorityAvgAsync(value);
                }
                else
                {
                    LoggerRTM.LogInformation("Normaler Modus: Niedrige Solarleistung, Automodus wird aktiviert.");
                    await TibberRTMAdjustment3AutoMode(value);
                }
            }
            else if (effectiveSolarPower > ApiSettingMaxPower * 0.8)
            {
                LoggerRTM.LogInformation("Normaler Modus: Hohe Solarleistung, Batterie wird geladen.");
                await TibberRTMDefaultBatteryPriorityAsync(value);
            }
            else
            {
                if (GetBatteryLevel() < 80)
                {
                    LoggerRTM.LogInformation("Normaler Modus: Mittlere Solarleistung, Akkustand unter 80%. Batterie wird geladen.");
                    await TibberRTMDefaultBatteryPriorityAsync(value);
                }
                else
                {
                    LoggerRTM.LogInformation("Normaler Modus: Mittlere Solarleistung, hoher Akkustand. Last wird priorisiert.");
                    await TibberRTMDefaultLoadPrioritySolarInputAsync(value);
                }
            }
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
