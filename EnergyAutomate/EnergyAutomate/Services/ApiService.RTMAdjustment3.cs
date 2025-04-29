namespace EnergyAutomate.Services
{
    /// <summary>
    /// Partial class for handling real-time power adjustments based on Tibber's real-time measurements.
    /// </summary>
    public partial class ApiService
    {
        #region Private Methods

        /// <summary>
        /// Adjusts power distribution based on various conditions such as battery state, weather,
        /// and power restrictions.
        /// </summary>
        /// <param name="value">The real-time measurement data from Tibber.</param>
        private async Task TibberRTMAdjustment3(TibberRealTimeMeasurement value)
        {
            // 1. Überprüfung spezieller Bedingungen, die immer Vorrang haben
            if (ApiSettingAutoMode)
            {
                await TibberRTMAdjustment3AutoMode(value);
                return;
            }

            // Effektive Solarleistung prüfen - diese berücksichtigt bereits die tatsächlich
            // gemessene Leistung der Panels, unabhängig von Tageszeit
            var effectiveSolarPower = CurrentState.GrowattNoahGetAvgPpvLast5Minutes();

            // Nach 16 Uhr kommt die Energie vom B2500 Akku
            bool isAfter16h = DateTime.Now.Hour >= 16;

            LoggerRTM.LogInformation($"Effektive Solarleistung: {effectiveSolarPower}W, Nach 16 Uhr: {isAfter16h}");

            // Nach 16 Uhr: Keine Batterie-Priorisierung, da Strom vom B2500-Akku
            if (isAfter16h)
            {
                LoggerRTM.LogInformation("Nach 16 Uhr: Energie kommt vom B2500-Akku, Last wird priorisiert");

                if (CurrentState.IsCheapRestrictionMode)
                {
                    LoggerRTM.LogInformation($"B2500 liefert noch {effectiveSolarPower}W: Cheap condition > BatteryPrio");
                    await TibberRTMDefaultBatteryPriorityAsync(value);
                }
                else if (effectiveSolarPower > 100) // Minimalschwelle für relevante Solarenergie
                {
                    // Wenn überhaupt noch Solar-Energie verfügbar ist, Verbrauch optimieren
                    LoggerRTM.LogInformation($"B2500 liefert noch {effectiveSolarPower}W: Last wird entsprechend angepasst");
                    await TibberRTMDefaultLoadPrioritySolarInputAsync(value);
                }
                else if (CurrentState.IsExpensiveRestrictionMode)
                {
                    LoggerRTM.LogInformation("Teure Strompreise, energiesparende Verteilung aktivieren");
                    await TibberRTMAdjustment3AutoMode(value);
                }
                else
                {
                    LoggerRTM.LogInformation("Standard-Last aktivieren");
                    await TibberRTMDefaultLoadPriorityAvgAsync(value);
                }
                return;
            }

            // Vor 16 Uhr: Normale Entscheidungslogik basierend auf tatsächlicher Solarleistung und Batteriestand

            // Wenn der Akku nicht voll ist, höchste Priorität auf Ladung legen
            if (!CurrentState.IsGrowattBatteryFull)
            {
                // Akkuladestrategie - Vorrangige Bedingungen prüfen

                // a. Bei günstigen Strompreisen oder Überschuss-Solarenergie: Akku laden
                if (CurrentState.IsCheapRestrictionMode || CurrentState.IsBelowAvgPrice)
                {
                    LoggerRTM.LogInformation("Günstige Strompreise: Batterie wird geladen");
                    await TibberRTMDefaultBatteryPriorityAsync(value);
                    return;
                }

                // b. Bei hoher Solarleistung: Akku laden
                if (effectiveSolarPower > ApiSettingAvgPower)
                {
                    LoggerRTM.LogInformation($"Hohe Solarleistung ({effectiveSolarPower}W): Batterie wird geladen");
                    await TibberRTMDefaultBatteryPriorityAsync(value);
                    return;
                }
            }

            // 2. Wenn der Akku voll ist oder wir teure Strompreise haben 
            // und nicht genug Solarleistung verfügbar ist - Verbraucher-Modus

            // Bei teuren Strompreisen: Automodus für optimale Verteilung
            if (CurrentState.IsExpensiveRestrictionMode)
            {
                await TibberRTMAdjustment3AutoMode(value);
                return;
            }

            // Wenn der Akku voll ist: Verbrauch optimieren
            if (CurrentState.IsGrowattBatteryFull)
            {
                // Bei hohen Solarleistungen: Maximaler Verbrauch
                if (effectiveSolarPower > ApiSettingMaxPower * 0.75) // Über 75% der maximalen Leistung
                {
                    LoggerRTM.LogInformation($"Akku voll und hohe Solarleistung ({effectiveSolarPower}W): Hohe Last");
                    await TibberRTMDefaultLoadPriorityMaxAsync(value, GrowattGetDeviceNoahSnList());
                }
                else
                {
                    LoggerRTM.LogInformation($"Akku voll: Standard-Last");
                    await TibberRTMDefaultLoadPriorityAvgAsync(value);
                }
                return;
            }

            // 3. Standard-Situationen basierend auf Solarleistung

            // a. Niedrige Solarleistung
            if (effectiveSolarPower < ApiSettingAvgPower * 0.5) // Weniger als 50% der Zielleistung
            {
                // Wenn der Akku fast leer ist und die Preise günstig sind: Akku laden
                if (GetBatteryLevel() < 30 && CurrentState.IsBelowAvgPrice)
                {
                    LoggerRTM.LogInformation($"Niedrige Solarleistung, niedriger Akkustand, günstige Preise: Akku laden");
                    await TibberRTMDefaultBatteryPriorityAsync(value);
                }
                else if (CurrentState.IsGrowattBatteryEmpty)
                {
                    // Bei leerem Akku: Last reduzieren
                    LoggerRTM.LogInformation($"Batterie ist leer, Last reduzieren");
                    await TibberRTMDefaultLoadPriorityAvgAsync(value);
                }
                else
                {
                    // Im Normalfall bei wenig Solarenergie: Automodus für optimale Verteilung
                    await TibberRTMAdjustment3AutoMode(value);
                }
            }
            // b. Hohe Solarleistung
            else if (effectiveSolarPower > ApiSettingMaxPower * 0.8) // Über 80% der maximalen Leistung
            {
                // Bei hoher Solarleistung priorisieren wir die Batterieladung
                LoggerRTM.LogInformation($"Hohe Solarleistung: Akku laden");
                await TibberRTMDefaultBatteryPriorityAsync(value);
            }
            // c. Mittlere Solarleistung
            else
            {
                // Bei mittlerer Solarleistung: Batterie laden solange der Akkustand nicht sehr hoch ist
                if (GetBatteryLevel() < 80)
                {
                    LoggerRTM.LogInformation($"Mittlere Solarleistung, Akkustand unter 80%: Akku laden");
                    await TibberRTMDefaultBatteryPriorityAsync(value);
                }
                else
                {
                    // Bei fast vollem Akku: Last priorisieren
                    LoggerRTM.LogInformation($"Mittlere Solarleistung, hoher Akkustand: Last priorisieren");
                    await TibberRTMDefaultLoadPrioritySolarInputAsync(value);
                }
            }
        }

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
            // Hier könnte eine Integration mit Wetterdaten erfolgen
            // Vereinfachte Version:
            if (CurrentState.IsCloudy)
                return 0.3 * ApiSettingMaxPower * hours; // 30% bei bewölktem Wetter
            else
                return 0.7 * ApiSettingMaxPower * hours; // 70% bei sonnigem Wetter
        }

        // Diese Methode bestimmt, ob der Akku basierend auf Wetter und Preisprognose jetzt geladen werden sollte
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
