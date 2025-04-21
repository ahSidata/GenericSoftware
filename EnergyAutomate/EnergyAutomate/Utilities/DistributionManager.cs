using System.Diagnostics;

namespace EnergyAutomate.Utilities
{
    public class DistributionManager
    {
        public DistributionManager(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }

        private IServiceProvider ServiceProvider { get; init; }

        private ApiService ApiService => ServiceProvider.GetRequiredService<ApiService>();

        private ApiQueueWatchdog<IDeviceQuery> GrowattDeviceQueryQueueWatchdog => ServiceProvider.GetRequiredService<ApiQueueWatchdog<IDeviceQuery>>();

        private ILogger LoggerRTM => ServiceProvider.GetRequiredService<ILogger<DistributionManager>>();

        /// <summary>
        /// Sets all devices to a specified power value.
        /// </summary>
        /// <param name="devices">List of devices to configure</param>
        /// <param name="powerValue">Power value to set for each device</param>
        /// <param name="timestamp">Current timestamp</param>
        public async Task SetAllDevicesToPower(List<DeviceList> devices, int powerValue, DateTimeOffset timestamp)
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
        public async Task PerformLoadBalancing(List<DeviceList> devices, DateTimeOffset timestamp)
        {
            LoggerRTM.LogTrace("Starting load balancing for {DeviceCount} devices", devices.Count);

            if (devices.Count <= 1)
            {
                LoggerRTM.LogTrace("Load balancing not possible with 1 or 0 devices.");
                return;
            }

            var maxSocDev = devices.OrderByDescending(d => d.Soc).First();
            var minSocDev = devices.OrderBy(d => d.Soc).First();

            if (minSocDev.Soc == 0 || maxSocDev.Soc == 0)
            {
                Debugger.Break();
            }

            LoggerRTM.LogTrace("Load balancing check: MaxSoC Device {MaxDevSn} ({MaxSoC}%), MinSoC Device {MinDevSn} ({MinSoC}%)",
                maxSocDev.DeviceSn, maxSocDev.Soc, minSocDev.DeviceSn, minSocDev.Soc);

            // Only balance if SoC difference is significant (≥ 5%)
            if (maxSocDev.Soc - minSocDev.Soc >= 5)
            {
                int shift = 10; // Shift 10 W per adjustment cycle
                int maxPowerPerDevice = ApiService.ApiSettingMaxPower / devices.Count;
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
        /// <summary>
        /// Distributes the specified total power among the available devices based on SoC prioritization and real-time device data.
        /// </summary>
        /// <param name="devices">List of devices</param>
        /// <param name="totalPower">Total power to distribute</param>
        /// <param name="prioritizeHighSoc">If true, prioritize devices with higher SoC; otherwise prioritize lower SoC</param>
        /// <param name="timestamp">Current timestamp</param>
        public async Task DistributePower(List<DeviceList> devices, int totalPower, bool prioritizeHighSoc, DateTimeOffset timestamp)
        {
            int maxPowerPerDevice = devices.Count > 0 ? ApiService.ApiSettingMaxPower / devices.Count : 0;

            LoggerRTM.LogTrace("DistributePower started. Total power: {TotalPower}W, prioritize high SoC: {PrioritizeHighSoc}",
                totalPower, prioritizeHighSoc);

            // Calculate currently allocated total power
            int currentTotalPower = devices.Sum(d => d.PowerValueCommited);
            int powerDifference = totalPower - currentTotalPower;

            LoggerRTM.LogTrace("Current total power: {CurrentPower}W, target power: {TargetPower}W, difference: {Difference}W",
                currentTotalPower, totalPower, powerDifference);

            // Filter out empty batteries and devices with insufficient data
            var eligibleDevices = devices
                .Where(d => !d.IsBatteryEmpty)
                .ToList();

            // Collect real-time metrics for eligible devices
            foreach (var device in eligibleDevices)
            {
                LoggerRTM.LogTrace("Device {DeviceSn}: PowerOutput={Output}W, PowerSolar={Solar}W, PowerBattery={Battery}W, SoC={Soc}%",
                    device.DeviceSn, device.PowerValueOutput, device.PowerValueSolar, device.PowerValueBattery, device.Soc);
            }

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

            // Check if the change can be handled by a single device with optimal conditions
            if (Math.Abs(powerDifference) > 0 && Math.Abs(powerDifference) <= maxPowerPerDevice && eligibleDevices.Count > 1)
            {
                // Select device based on both SoC and real-time metrics
                var optimalDevice = SelectOptimalDevice(eligibleDevices, powerDifference > 0, prioritizeHighSoc);

                // Calculate the new power value for the selected device
                int newPower = optimalDevice.PowerValueCommited;
                if (powerDifference > 0)
                {
                    // Calculate maximum allowed increase based on device capabilities
                    int maxIncrease = Math.Min(maxPowerPerDevice - optimalDevice.PowerValueCommited,
                        Math.Max(0, GetDeviceAvailableCapacity(optimalDevice)));
                    int actualIncrease = Math.Min(powerDifference, maxIncrease);
                    newPower += actualIncrease;

                    LoggerRTM.LogTrace("Device {DeviceSn} can handle increase: Current {Current}W, Max increase {MaxIncrease}W, Actual increase {ActualIncrease}W, Solar {Solar}W, Battery {Battery}W",
                        optimalDevice.DeviceSn, optimalDevice.PowerValueCommited, maxIncrease, actualIncrease, optimalDevice.PowerValueSolar, optimalDevice.PowerValueBattery);
                }
                else // powerDifference < 0
                {
                    // Calculate maximum allowed decrease based on device current output
                    int maxDecrease = Math.Min(optimalDevice.PowerValueCommited, optimalDevice.PowerValueOutput); // Don't decrease more than current output
                    int actualDecrease = Math.Min(Math.Abs(powerDifference), maxDecrease);
                    newPower -= actualDecrease;

                    LoggerRTM.LogTrace("Device {DeviceSn} can handle decrease: Current {Current}W, Max decrease {MaxDecrease}W, Actual decrease {ActualDecrease}W, Output {Output}W",
                        optimalDevice.DeviceSn, optimalDevice.PowerValueCommited, maxDecrease, actualDecrease, optimalDevice.PowerValueOutput);
                }

                if (newPower != optimalDevice.PowerValueCommited)
                {
                    LoggerRTM.LogTrace("Single device adjustment: Updating device {DeviceSn} (SoC {Soc}%) from {OldPower}W to {NewPower}W",
                        optimalDevice.DeviceSn, optimalDevice.Soc, optimalDevice.PowerValueCommited, newPower);

                    await GrowattDeviceQueryQueueWatchdog.EnqueueAsync(new DeviceNoahSetPowerQuery
                    {
                        DeviceType = "noah",
                        DeviceSn = optimalDevice.DeviceSn,
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
            // Sort devices based on SoC priority and efficiency metrics
            var sortedDevices = SortDevicesByPriority(eligibleDevices, prioritizeHighSoc);

            LoggerRTM.LogTrace("Devices sorted by priority for balanced distribution");

            // Calculate base power per device
            int powerPerDevice = eligibleDevices.Count > 0 ? totalPower / eligibleDevices.Count : 0;

            LoggerRTM.LogTrace("Base power per device: {PowerPerDevice}W, Max power per device: {MaxPowerPerDevice}W",
                powerPerDevice, maxPowerPerDevice);

            // Calculate weighted distribution based on device capabilities
            Dictionary<string, int> allocations = CalculateWeightedDistribution(sortedDevices, totalPower, maxPowerPerDevice);

            // Apply the calculated power allocations
            foreach (var device in sortedDevices)
            {
                int allocatedPower = allocations[device.DeviceSn];

                // Check difference to current value
                int powerChange = allocatedPower - device.PowerValueCommited;
                LoggerRTM.LogTrace("Device {DeviceSn}: Current {Current}W, Allocated {Allocated}W, Change {Change}W, SoC {Soc}%, Output {Output}W, Solar {Solar}W, Battery {Battery}W",
                    device.DeviceSn, device.PowerValueCommited, allocatedPower, powerChange, device.Soc,
                    device.PowerValueOutput, device.PowerValueSolar, device.PowerValueBattery);

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

        /// <summary>
        /// Selects the optimal device for power adjustment based on real-time metrics
        /// </summary>
        private DeviceList SelectOptimalDevice(List<DeviceList> devices, bool isIncrease, bool prioritizeHighSoc)
        {
            if (isIncrease)
            {
                // For power increase, select device with:
                // 1. Appropriate SoC (high or low based on prioritization)
                // 2. High solar input (more efficient)
                // 3. Good battery status
                return prioritizeHighSoc
                    ? devices.OrderByDescending(d => d.Soc)
                            .ThenByDescending(d => d.PowerValueSolar)
                            .ThenByDescending(d => d.PowerValueBattery)
                            .First()
                    : devices.OrderBy(d => d.Soc)
                            .ThenByDescending(d => d.PowerValueSolar)
                            .ThenByDescending(d => d.PowerValueBattery)
                            .First();
            }
            else
            {
                // For power decrease, select device with:
                // 1. Appropriate SoC (high or low based on prioritization)
                // 2. Highest battery consumption (most negative PowerValueBattery)
                // - This will prioritize devices that are actively discharging their batteries
                return prioritizeHighSoc
                    ? devices.OrderByDescending(d => d.Soc)
                            .ThenBy(d => d.PowerValueBattery) // Ascending to prioritize negative values
                            .First()
                    : devices.OrderBy(d => d.Soc)
                            .ThenBy(d => d.PowerValueBattery) // Ascending to prioritize negative values
                            .First();
            }
        }

        /// <summary>
        /// Gets the available capacity for power increase based on solar and battery metrics
        /// </summary>
        private int GetDeviceAvailableCapacity(DeviceList device)
        {
            // This estimates how much more power the device could potentially provide
            // based on solar input and battery status
            return device.PowerValueSolar + (device.PowerValueBattery > 0 ? device.PowerValueBattery : 0);
        }

        /// <summary>
        /// Sorts devices by priority for power distribution
        /// </summary>
        private List<DeviceList> SortDevicesByPriority(List<DeviceList> devices, bool prioritizeHighSoc)
        {
            if (prioritizeHighSoc)
            {
                // When prioritizing high SoC (typically for increasing power):
                // Sort by SoC descending, then by solar input descending
                return devices.OrderByDescending(d => d.Soc)
                             .ThenByDescending(d => d.PowerValueSolar)
                             .ToList();
            }
            else
            {
                // When prioritizing low SoC (typically for decreasing power):
                // Sort by SoC ascending, and prioritize those with higher battery consumption (negative values)
                return devices.OrderBy(d => d.Soc)
                             .ThenBy(d => d.PowerValueBattery) // Prioritize devices that are discharging more
                             .ToList();
            }
        }

        /// <summary>
        /// Calculates weighted power distribution based on device capabilities
        /// </summary>
        private Dictionary<string, int> CalculateWeightedDistribution(List<DeviceList> devices, int totalPower, int maxPowerPerDevice)
        {
            Dictionary<string, int> allocations = new Dictionary<string, int>();
            int deviceCount = devices.Count;

            if (deviceCount == 0)
                return allocations;

            // Calculate total weighting factors
            int totalWeightingFactor = 0;
            Dictionary<string, int> deviceWeights = new Dictionary<string, int>();

            foreach (var device in devices)
            {
                // Weight based on solar input and battery status
                int weight = 100; // Base weight

                // Add bonus weight for higher solar input
                weight += (int)(device.PowerValueSolar / 10);

                // Add bonus/penalty for battery status
                if (device.PowerValueBattery > 0) // Charging
                    weight += (int)(device.PowerValueBattery / 20);
                else if (device.PowerValueBattery < 0) // Discharging
                    weight -= (int)(Math.Abs(device.PowerValueBattery) / 20);

                // Ensure minimum weight
                weight = Math.Max(10, weight);

                deviceWeights[device.DeviceSn] = weight;
                totalWeightingFactor += weight;
            }

            // First pass: calculate weighted allocation
            int remainingPower = totalPower;
            foreach (var device in devices)
            {
                if (totalWeightingFactor == 0)
                {
                    // Fallback to equal distribution
                    allocations[device.DeviceSn] = Math.Min(maxPowerPerDevice, totalPower / deviceCount);
                    continue;
                }

                double weightRatio = (double)deviceWeights[device.DeviceSn] / totalWeightingFactor;
                int initialAllocation = (int)(totalPower * weightRatio);

                // Cap at maxPowerPerDevice
                int allocation = Math.Min(initialAllocation, maxPowerPerDevice);
                // Ensure we don't exceed remaining power
                allocation = Math.Min(allocation, remainingPower);

                allocations[device.DeviceSn] = allocation;
                remainingPower -= allocation;
            }

            // Second pass: distribute any remaining power
            if (remainingPower > 0)
            {
                foreach (var device in devices.OrderByDescending(d => deviceWeights[d.DeviceSn]))
                {
                    int additionalPower = Math.Min(remainingPower, maxPowerPerDevice - allocations[device.DeviceSn]);
                    if (additionalPower > 0)
                    {
                        allocations[device.DeviceSn] += additionalPower;
                        remainingPower -= additionalPower;

                        if (remainingPower <= 0)
                            break;
                    }
                }
            }

            return allocations;
        }

    }
}
