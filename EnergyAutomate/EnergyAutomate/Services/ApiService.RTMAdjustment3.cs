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
            // Determine if the current time is after 4 PM (used for specific logic).
            var b2500Mode = CurrentState.UtcNow.Hour > 16;

            // If auto mode or expensive restriction mode is active, delegate to auto mode adjustment.
            if (ApiSettingAutoMode || CurrentState.IsExpensiveRestrictionMode)
            {
                await TibberRTMAdjustment3AutoMode(value);
                return;
            }

            // If it's cloudy and the battery is not full, prioritize charging the battery.
            if (CurrentState.IsCloudy && !CurrentState.IsGrowattBatteryFull)
            {
                await TibberRTMDefaultBatteryPriorityAsync(value);
            }

            // Get the average solar power production over the last 5 minutes.
            var last5Minutes = CurrentState.GrowattNoahGetAvgPpvLast5Minutes();

            // If the average power is below the threshold (ApiSettingAvgPower + 100).
            if (last5Minutes < ApiSettingAvgPower + 100)
            {
                // If the battery is empty, prioritize load reduction.
                if (CurrentState.IsGrowattBatteryEmpty)
                {
                    await TibberRTMDefaultLoadPriorityAvgAsync(value);
                    LoggerRTM.LogInformation($"Battery is empty, set power to 0");
                }
                // If the battery is full, prioritize load with full solar power.
                else if (CurrentState.IsGrowattBatteryFull)
                {
                    await TibberRTMDefaultLoadPriorityAvgAsync(value);
                }
                else
                {
                    // Handle different restriction modes.
                    if (CurrentState.IsExpensiveRestrictionMode)
                    {
                        await TibberRTMAdjustment3AutoMode(value);
                    }
                    else if (CurrentState.IsCheapRestrictionMode)
                    {
                        await TibberRTMDefaultBatteryPriorityAsync(value);
                    }
                    else
                    {
                        // If it's cloudy and the price is not below average, prioritize load reduction.
                        if (CurrentState.IsCloudy && !CurrentState.IsBelowAvgPrice)
                        {
                            await TibberRTMDefaultLoadPriorityAvgAsync(value);
                        }
                        else
                        {
                            // Otherwise, prioritize solar input for load.
                            await TibberRTMDefaultLoadPrioritySolarInputAsync(value);
                        }
                    }
                }
            }
            // If the average power is above 840.
            else if (last5Minutes > 840)
            {
                // If the battery is full, no action is needed.
                if (CurrentState.IsGrowattBatteryFull)
                {
                    LoggerRTM.LogInformation($"Battery is full, no action needed");
                    await TibberRTMDefaultBatteryPriorityAsync(value);
                }
                else
                {
                    // If it's cloudy, prioritize charging the battery.
                    if (CurrentState.IsCloudy)
                    {
                        await TibberRTMDefaultBatteryPriorityAsync(value);
                    }
                    else
                    {
                        // If in cheap restriction mode, prioritize charging the battery with full
                        // solar power.
                        if (CurrentState.IsCheapRestrictionMode)
                        {
                            await TibberRTMDefaultBatteryPriorityAsync(value);
                        }
                        else
                        {
                            // Otherwise, force load for consumption.
                            await TibberRTMDefaultLoadPriorityMaxAsync(value, GrowattGetDeviceNoahSnList());
                        }
                    }
                }
            }
            else
            {
                // Handle cases where the average power is within a specific range.
                if (CurrentState.IsGrowattBatteryFull)
                {
                    if (CurrentState.IsExpensiveRestrictionMode)
                    {
                        await TibberRTMAdjustment3AutoMode(value);
                    }
                    else
                    {
                        await TibberRTMDefaultBatteryPriorityAsync(value);
                    }
                }
                else
                {
                    // If it's cloudy, handle based on restriction mode.
                    if (CurrentState.IsCloudy)
                    {
                        if (CurrentState.IsExpensiveRestrictionMode)
                        {
                            await TibberRTMAdjustment3AutoMode(value);
                        }
                        else
                        {
                            await TibberRTMDefaultBatteryPriorityAsync(value);
                        }
                    }
                    else
                    {
                        // If the price is not below average or in expensive restriction mode, force load.
                        if (CurrentState.IsExpensiveRestrictionMode || !CurrentState.IsBelowAvgPrice)
                        {
                            await TibberRTMAdjustment3AutoMode(value);
                        }
                        else
                        {
                            // Otherwise, prioritize solar input for load.
                            await TibberRTMDefaultLoadPrioritySolarInputAsync(value);
                        }
                    }
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
