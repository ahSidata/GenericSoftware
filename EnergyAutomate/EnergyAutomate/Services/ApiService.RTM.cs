using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EnergyAutomate.Services
{
    public partial class ApiService
    {
        /// <summary>
        /// Adjusts power distribution based on real-time measurements from Tibber.
        /// </summary>
        /// <param name="value">The current real-time measurement data</param>
        private async Task TibberRTMAdjustment3SetPower(TibberRealTimeMeasurement value)
        {
            LoggerRTM.LogTrace("Starting TibberRTMAdjustment3SetPower. TotalPower: {TotalPower}, ApiSettingAvgPower: {TargetPower}, ApiSettingAvgPowerOffset: {TargetOffset}, ApiSettingMaxPower: {MaxPower}",
                value.TotalPower, ApiSettingAvgPower, ApiSettingAvgPowerOffset, ApiSettingMaxPower);

            // Handle wait cycles for power adjustment
            if (_adjustmentWaitCycles < ApiSettingPowerAdjustmentWaitCycles)
            {
                _adjustmentWaitCycles++;
                LoggerRTM.LogTrace("Wait because WaitCycles: {WaitCycles} < {PowerAdjustmentWaitCycles}",
                    _adjustmentWaitCycles, ApiSettingPowerAdjustmentWaitCycles);
                return;
            }

            _adjustmentWaitCycles = 0;
            LoggerRTM.LogTrace("Reset adjustment wait cycles to 0.");

            // Get committed power value, ensuring it's non-negative
            var powerValueTotalCommited = Math.Max(0, CurrentState?.PowerValueTotalCommited ?? 0);

            // Calculate delta and limits
            int deltaTotalPower = value.TotalPower - ApiSettingAvgPowerOffset;
            var upperLimit = ApiSettingAvgPowerOffset + (ApiSettingAvgPowerHysteresis / 2);
            var lowerLimit = ApiSettingAvgPowerOffset - (ApiSettingAvgPowerHysteresis / 2);

            LoggerRTM.LogTrace("Calculated limits. DeltaTotalPower: {DeltaTotalPower}, UpperLimit: {UpperLimit}, LowerLimit: {LowerLimit}, CommitedTotalPower: {CommitedTotalPower}",
                deltaTotalPower, upperLimit, lowerLimit, powerValueTotalCommited);

            // Get online devices
            var onlineDevices = GrowattGetDevicesNoahOnline();
            if (onlineDevices == null || !onlineDevices.Any())
            {
                LoggerRTM.LogTrace("No devices available for adjustment.");
                return;
            }

            LoggerRTM.LogTrace("Found {DeviceCount} online devices", onlineDevices.Count);
            int maxPowerPerDevice = onlineDevices.Count > 0 ? ApiSettingMaxPower / onlineDevices.Count : 0;

            // Handle excessive positive power
            if (Math.Abs(value.TotalPower) > ApiSettingMaxPower)
            {
                if (value.TotalPower < 0)
                {
                    // Requirement #3: For negative TotalPower exceeding limit, set all to 0
                    LoggerRTM.LogTrace("Negative TotalPower ({TotalPower}) exceeds ApiSettingMaxPower ({MaxPower}). Setting all device power values to 0 W.",
                        value.TotalPower, ApiSettingMaxPower);

                    await SetAllDevicesToPower(onlineDevices, 0, value.TS);
                    return;
                }
                if (value.TotalPower > 0)
                {
                    // Requirement #3: For positive TotalPower exceeding limit, set all to even distribution
                    LoggerRTM.LogTrace("Positive TotalPower ({TotalPower}) exceeds ApiSettingMaxPower ({MaxPower}). Setting all device power values to {PowerPerDevice} W.",
                    value.TotalPower, ApiSettingMaxPower, maxPowerPerDevice);

                    await SetAllDevicesToPower(onlineDevices, maxPowerPerDevice, value.TS);
                    return;
                }
            }

            // Handle hysteresis range (Requirement #4)
            if (value.TotalPower >= lowerLimit && value.TotalPower <= upperLimit)
            {
                LoggerRTM.LogTrace("TotalPower within hysteresis range ({Lower} to {Upper}). Performing load balancing.",
                    lowerLimit, upperLimit);
                await PerformLoadBalancing(onlineDevices, value.TS);
                return;
            }

            // Adjust power based on delta
            // Use ApiSettingPowerAdjustmentFactor to scale the delta (100 represents 100%)
            double adjustmentFactor = ApiSettingPowerAdjustmentFactor / 100.0;
            int adjustedDelta = (int)(deltaTotalPower * adjustmentFactor);
            LoggerRTM.LogTrace("Adjusted delta (after adjustment factor {Factor}%): {AdjustedDelta}",
                ApiSettingPowerAdjustmentFactor, adjustedDelta);

            if (deltaTotalPower > 0)
            {
                // Calculate the desired total power based on current state
                int desiredTotalPower = Math.Min(ApiSettingMaxPower, powerValueTotalCommited + adjustedDelta);

                LoggerRTM.LogTrace("Positive delta detected. Change total power from {CurrentPower} to {DesiredPower} based on adjusted delta {AdjustedDelta}\"",
                    powerValueTotalCommited, desiredTotalPower, adjustedDelta);

                LoggerRTM.LogTrace("Distributing total power of {TotalPower}W with high SoC prioritization", desiredTotalPower);
                await DistributePower(onlineDevices, desiredTotalPower, prioritizeHighSoc: true, value.TS);
            }
            else if (deltaTotalPower < 0)
            {
                // Calculate desired total power based on current committed power and adjusted delta
                int desiredTotalPower = Math.Max(0, powerValueTotalCommited + adjustedDelta);

                LoggerRTM.LogTrace("Negative delta detected. Change total power from {CurrentPower} to {DesiredPower} based on adjusted delta {AdjustedDelta}",
                    powerValueTotalCommited, desiredTotalPower, adjustedDelta);

                LoggerRTM.LogTrace("Distributing total power of {TotalPower}W with low SoC prioritization", desiredTotalPower);
                await DistributePower(onlineDevices, desiredTotalPower, prioritizeHighSoc: false, value.TS);
            }
            else
            {
                // Delta is exactly 0, maintain current power or perform load balancing
                LoggerRTM.LogTrace("Delta is exactly 0. Performing load balancing.");
                await PerformLoadBalancing(onlineDevices, value.TS);
            }

            LoggerRTM.LogTrace("Power adjustment procedure completed.");
        }

        /// <summary>
        /// Sets all devices to a specified power value.
        /// </summary>
        /// <param name="devices">List of devices to configure</param>
        /// <param name="powerValue">Power value to set for each device</param>
        /// <param name="timestamp">Current timestamp</param>
        private async Task SetAllDevicesToPower(List<DeviceList> devices, int powerValue, DateTimeOffset timestamp)
        {
            foreach (var device in devices)
            {
                LoggerRTM.LogTrace("Setting device {DeviceSn} (SoC {Soc}%) to {Power}W",
                    device.DeviceSn, device.Soc, powerValue);

                await GrowattDeviceQueryQueueWatchdog.EnqueueAsync(new DeviceNoahSetPowerQuery
                {
                    DeviceType = "noah",
                    DeviceSn = device.DeviceSn,
                    Value = powerValue,
                    Force = true,
                    TS = timestamp
                });
            }

            LoggerRTM.LogTrace("All {DeviceCount} devices set to {Power}W", devices.Count, powerValue);
        }

        /// <summary>
        /// Performs load balancing between devices based on their SoC values.
        /// Shifts power from devices with lower SoC to those with higher SoC.
        /// </summary>
        /// <param name="devices">List of devices to balance</param>
        /// <param name="timestamp">Current timestamp</param>
        private async Task PerformLoadBalancing(List<DeviceList> devices, DateTimeOffset timestamp)
        {
            LoggerRTM.LogTrace("Starting load balancing for {DeviceCount} devices", devices.Count);

            if (devices.Count <= 1)
            {
                LoggerRTM.LogTrace("Load balancing not possible with 1 or 0 devices.");
                return;
            }

            var maxSocDev = devices.OrderByDescending(d => d.Soc).First();
            var minSocDev = devices.OrderBy(d => d.Soc).First();
            LoggerRTM.LogTrace("Load balancing check: MaxSoC Device {MaxDevSn} ({MaxSoC}%), MinSoC Device {MinDevSn} ({MinSoC}%)",
                maxSocDev.DeviceSn, maxSocDev.Soc, minSocDev.DeviceSn, minSocDev.Soc);

            // Only balance if SoC difference is significant (≥ 5%)
            if (maxSocDev.Soc - minSocDev.Soc >= 5)
            {
                int shift = 10; // Shift 10 W per adjustment cycle
                int maxPowerPerDevice = ApiSettingMaxPower / devices.Count;
                int maxShiftUp = maxPowerPerDevice - maxSocDev.PowerValueCommited;
                int maxShiftDown = minSocDev.PowerValueCommited;

                LoggerRTM.LogTrace("Load balancing possible. MaxShiftUp: {MaxShiftUp}W, MaxShiftDown: {MaxShiftDown}W",
                    maxShiftUp, maxShiftDown);

                int actualShift = Math.Min(shift, Math.Min(maxShiftUp, maxShiftDown));

                if (actualShift > 0)
                {
                    LoggerRTM.LogTrace("Load balancing: Shifting {Shift}W from Device {MinDev} (SoC {MinSoc}%) to Device {MaxDev} (SoC {MaxSoc}%)",
                        actualShift, minSocDev.DeviceSn, minSocDev.Soc, maxSocDev.DeviceSn, maxSocDev.Soc);

                    var minSocDevPowerValueRequested = minSocDev.PowerValueCommited - actualShift;
                    var maxSocDevPowerValueRequested = maxSocDev.PowerValueCommited + actualShift;

                    await GrowattDeviceQueryQueueWatchdog.EnqueueAsync(new DeviceNoahSetPowerQuery
                    {
                        DeviceType = "noah",
                        DeviceSn = minSocDev.DeviceSn,
                        Value = minSocDevPowerValueRequested,
                        Force = true,
                        TS = timestamp
                    });

                    await GrowattDeviceQueryQueueWatchdog.EnqueueAsync(new DeviceNoahSetPowerQuery
                    {
                        DeviceType = "noah",
                        DeviceSn = maxSocDev.DeviceSn,
                        Value = maxSocDevPowerValueRequested,
                        Force = true,
                        TS = timestamp
                    });

                    LoggerRTM.LogTrace("Load balancing completed. Device {MinDev} set to {MinPower}W, Device {MaxDev} set to {MaxPower}W",
                        minSocDev.DeviceSn, minSocDevPowerValueRequested, maxSocDev.DeviceSn, maxSocDevPowerValueRequested);
                }
                else
                {
                    LoggerRTM.LogTrace("Load balancing: No room for shifting (actualShift={ActualShift}).", actualShift);
                }
            }
            else
            {
                LoggerRTM.LogTrace("Load balancing: SoC difference {SocDiff}% is <5%, no action.",
                    maxSocDev.Soc - minSocDev.Soc);
            }
        }

        /// <summary>
        /// Distributes the specified total power among the available devices based on SoC prioritization.
        /// </summary>
        /// <param name="devices">List of devices</param>
        /// <param name="totalPower">Total power to distribute</param>
        /// <param name="prioritizeHighSoc">If true, prioritize devices with higher SoC; otherwise prioritize lower SoC</param>
        /// <param name="timestamp">Current timestamp</param>
        private async Task DistributePower(List<DeviceList> devices, int totalPower, bool prioritizeHighSoc, DateTimeOffset timestamp)
        {
            int maxPowerPerDevice = devices.Count > 0 ? ApiSettingMaxPower / devices.Count : 0;

            LoggerRTM.LogTrace("DistributePower started. Total power: {TotalPower}W, prioritize high SoC: {PrioritizeHighSoc}",
                totalPower, prioritizeHighSoc);

            // Calculate currently allocated total power
            int currentTotalPower = devices.Sum(d => d.PowerValueCommited);
            int powerDifference = totalPower - currentTotalPower;

            LoggerRTM.LogTrace("Current total power: {CurrentPower}W, target power: {TargetPower}W, difference: {Difference}W",
                currentTotalPower, totalPower, powerDifference);

            // Filter out empty batteries
            var eligibleDevices = devices
                .Where(d => !d.IsBatteryEmpty)
                .ToList();

            LoggerRTM.LogTrace("Found {EligibleCount} eligible devices (non-empty) from {TotalCount} total devices",
                eligibleDevices.Count, devices.Count);

            // Handle empty devices
            if (devices.Any(d => d.IsBatteryEmpty))
            {
                var emptyDevices = devices.Where(d => d.IsBatteryEmpty).ToList();
                foreach (var device in emptyDevices)
                {
                    LoggerRTM.LogTrace("Device {DeviceSn} excluded from power distribution: Battery empty (SoC {Soc}%)",
                        device.DeviceSn, device.Soc);

                    await GrowattDeviceQueryQueueWatchdog.EnqueueAsync(new DeviceNoahSetPowerQuery
                    {
                        DeviceType = "noah",
                        DeviceSn = device.DeviceSn,
                        Value = 0,
                        Force = true,
                        TS = timestamp
                    });
                }
            }

            if (!eligibleDevices.Any())
            {
                LoggerRTM.LogTrace("No eligible devices for power distribution. Aborting.");
                return;
            }

            // Check if the change can be handled by a single device
            if (Math.Abs(powerDifference) > 0 && Math.Abs(powerDifference) <= maxPowerPerDevice && eligibleDevices.Count > 1)
            {
                // Select device with best SoC for adjustment (highest SoC for increases, lowest for decreases)
                var targetDevice = prioritizeHighSoc
                    ? eligibleDevices.OrderByDescending(d => d.Soc).First()
                    : eligibleDevices.OrderBy(d => d.Soc).First();

                // Calculate the new power value for the selected device
                int newPower = targetDevice.PowerValueCommited;
                if (powerDifference > 0)
                {
                    // Calculate maximum allowed increase
                    int maxIncrease = maxPowerPerDevice - targetDevice.PowerValueCommited;
                    int actualIncrease = Math.Min(powerDifference, maxIncrease);
                    newPower += actualIncrease;

                    LoggerRTM.LogTrace("Device {DeviceSn} can handle increase: Current {Current}W, Max increase {MaxIncrease}W, Actual increase {ActualIncrease}W",
                        targetDevice.DeviceSn, targetDevice.PowerValueCommited, maxIncrease, actualIncrease);
                }
                else // powerDifference < 0
                {
                    // Calculate maximum allowed decrease
                    int maxDecrease = targetDevice.PowerValueCommited; // Don't go below 0
                    int actualDecrease = Math.Min(Math.Abs(powerDifference), maxDecrease);
                    newPower -= actualDecrease;

                    LoggerRTM.LogTrace("Device {DeviceSn} can handle decrease: Current {Current}W, Max decrease {MaxDecrease}W, Actual decrease {ActualDecrease}W",
                        targetDevice.DeviceSn, targetDevice.PowerValueCommited, maxDecrease, actualDecrease);
                }

                if (newPower != targetDevice.PowerValueCommited)
                {
                    LoggerRTM.LogTrace("Single device adjustment: Updating device {DeviceSn} (SoC {Soc}%) from {OldPower}W to {NewPower}W",
                        targetDevice.DeviceSn, targetDevice.Soc, targetDevice.PowerValueCommited, newPower);

                    await GrowattDeviceQueryQueueWatchdog.EnqueueAsync(new DeviceNoahSetPowerQuery
                    {
                        DeviceType = "noah",
                        DeviceSn = targetDevice.DeviceSn,
                        Value = newPower,
                        Force = true,
                        TS = timestamp
                    });

                    // Don't update the other devices
                    LoggerRTM.LogTrace("Power adjustment completed using single device");
                    return;
                }
                else
                {
                    LoggerRTM.LogTrace("Single device adjustment not possible due to limits. Using balanced distribution.");
                }
            }

            // If single-device adjustment is not possible, distribute power across all devices
            // Sort devices based on SoC priority
            var sortedDevices = prioritizeHighSoc
                ? eligibleDevices.OrderByDescending(d => d.Soc).ToList()
                : eligibleDevices.OrderBy(d => d.Soc).ToList();

            LoggerRTM.LogTrace("Devices sorted by SoC ({Order}) for balanced distribution",
                prioritizeHighSoc ? "descending" : "ascending");

            // Calculate power to distribute for each device
            int powerPerDevice = eligibleDevices.Count > 0 ? totalPower / eligibleDevices.Count : 0;

            LoggerRTM.LogTrace("Base power per device: {PowerPerDevice}W, Max power per device: {MaxPowerPerDevice}W",
                powerPerDevice, maxPowerPerDevice);

            int remainingPower = totalPower;
            foreach (var device in sortedDevices)
            {
                int allocatedPower;
                if (remainingPower >= maxPowerPerDevice)
                {
                    allocatedPower = maxPowerPerDevice;
                    remainingPower -= maxPowerPerDevice;
                }
                else
                {
                    allocatedPower = remainingPower;
                    remainingPower = 0;
                }

                // Check difference to current value
                int powerChange = allocatedPower - device.PowerValueCommited;
                LoggerRTM.LogTrace("Device {DeviceSn}: Current {Current}W, Allocated {Allocated}W, Change {Change}W",
                    device.DeviceSn, device.PowerValueCommited, allocatedPower, powerChange);

                await GrowattDeviceQueryQueueWatchdog.EnqueueAsync(new DeviceNoahSetPowerQuery
                {
                    DeviceType = "noah",
                    DeviceSn = device.DeviceSn,
                    Value = allocatedPower,
                    Force = true,
                    TS = timestamp
                });
            }

            LoggerRTM.LogTrace("Balanced power distribution completed for {DeviceCount} devices", sortedDevices.Count);
        }

    }
}