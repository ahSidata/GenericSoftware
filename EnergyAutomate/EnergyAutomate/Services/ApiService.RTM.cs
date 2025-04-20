using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EnergyAutomate.Services
{
    public partial class ApiService
    {
        private async Task TibberRTMAdjustment3SetPower(TibberRealTimeMeasurement value)
        {
            LoggerRTM.LogTrace("Starting TibberRTMAdjustment3SetPower. TotalPower: {TotalPower}, ApiSettingAvgPower: {TargetPower}",
                value.TotalPower, ApiSettingAvgPower);

            if (_adjustmentWaitCycles < ApiSettingPowerAdjustmentWaitCycles)
            {
                _adjustmentWaitCycles++;
                LoggerRTM.LogTrace("Wait because WaitCycles: {WaitCycles} < {PowerAdjustmentWaitCycles}",
                    _adjustmentWaitCycles, ApiSettingPowerAdjustmentWaitCycles);
                return;
            }

            _adjustmentWaitCycles = 0;
            LoggerRTM.LogTrace("Reset adjustment wait cycles to 0.");

            var powerValueTotalCommited = CurrentState.PowerValueTotalCommited;
            int deltaTotalPower = value.TotalPower - ApiSettingAvgPower;
            var upperlimit = ApiSettingAvgPower + (ApiSettingAvgPowerHysteresis / 2);
            var lowerlimit = ApiSettingAvgPower - (ApiSettingAvgPowerHysteresis / 2);

            LoggerRTM.LogTrace("Calculated limits. DeltaTotalPower: {DeltaTotalPower}, UpperLimit: {UpperLimit}, LowerLimit: {LowerLimit}, CommitedTotalPower: {CommitedTotalPower}",
                deltaTotalPower, upperlimit, lowerlimit, powerValueTotalCommited);

            var onlineDevices = GrowattGetDevicesNoahOnline();
            if (onlineDevices == null || !onlineDevices.Any())
            {
                LoggerRTM.LogTrace("No devices available for adjustment.");
                return;
            }

            // Bezug: Über oberem Limit → Hochregeln
            if (value.TotalPower > upperlimit)
            {
                var requestedTotalPower = ApiSettingMaxPower;
                LoggerRTM.LogTrace("Increasing power to maximum. RequestedTotalPower: {RequestedTotalPower}", requestedTotalPower);

                int maxPowerPerDevice = requestedTotalPower / onlineDevices.Count;

                var devices = onlineDevices.OrderByDescending(o => o.Soc).ToList();
                foreach (var device in devices)
                {
                    LoggerRTM.LogTrace("Device {DeviceSn} - SoC: {Soc}%, PowerValueCommited: {PowerValue}, IsBatteryEmpty: {IsBatteryEmpty}, IsBatteryFull: {IsBatteryFull}",
                        device.DeviceSn, device.Soc, device.PowerValueCommited, device.IsBatteryEmpty, device.IsBatteryFull);

                    if (device.IsBatteryEmpty)
                    {
                        LoggerRTM.LogTrace("Skipping device {DeviceSn} - battery too low (SoC: {Soc}%).",
                            device.DeviceSn, device.Soc);
                        continue;
                    }

                    int newPower;
                    if (device.PowerValueCommited <= 0)
                    {
                        newPower = Math.Min(Math.Max(100, device.Soc * 4), maxPowerPerDevice);
                        LoggerRTM.LogTrace("REAKTIVIERE Gerät {DeviceSn} mit SoC {Soc}% auf: {NewPower}W",
                            device.DeviceSn, device.Soc, newPower);
                    }
                    else
                    {
                        newPower = Math.Min(Math.Max(device.PowerValueCommited + 100, (int)(device.PowerValueCommited * 1.5)), maxPowerPerDevice);
                        LoggerRTM.LogTrace("Device {DeviceSn} - Setting to maximum available power: {NewPower}W",
                            device.DeviceSn, newPower);
                    }

                    device.PowerValueRequested = newPower;
                    await GrowattDeviceQueryQueueWatchdog.EnqueueAsync(new DeviceNoahSetPowerQuery
                    {
                        DeviceType = "noah",
                        DeviceSn = device.DeviceSn,
                        Value = newPower,
                        Force = true,
                        TS = value.TS
                    });
                }
            }
            // Einspeisung: Unter unterem Limit → Schnell reduzieren
            else if (value.TotalPower < lowerlimit)
            {
                int baseReductionPercent = 60;
                if (value.TotalPower < -100)
                {
                    baseReductionPercent = 80;
                }

                int maxReduction = powerValueTotalCommited * baseReductionPercent / 100;

                if (value.TotalPower < -150 && powerValueTotalCommited > 300)
                {
                    maxReduction = powerValueTotalCommited - ApiSettingAvgPower;
                }

                int reactionFactor = (value.TotalPower < -100) ? 3 : 2;
                int desiredReduction = Math.Min(Math.Abs(value.TotalPower) * reactionFactor, maxReduction);

                int minimumTotalPower = ApiSettingAvgPower;
                var requestedTotalPower = Math.Max(powerValueTotalCommited - desiredReduction, minimumTotalPower);

                if (value.TotalPower < -200)
                {
                    minimumTotalPower = ApiSettingAvgPower / 2;
                    requestedTotalPower = Math.Max(minimumTotalPower, powerValueTotalCommited - desiredReduction);
                }

                LoggerRTM.LogTrace("SCHNELLERE Reduktion wegen Einspeisung. RequestedTotalPower: {RequestedTotalPower} (max reduction: {MaxReduction}W, {BaseReductionPercent}%)",
                    requestedTotalPower, maxReduction, baseReductionPercent);

                if (requestedTotalPower < powerValueTotalCommited)
                {
                    int remainingReduction = powerValueTotalCommited - requestedTotalPower;
                    var activeDevices = onlineDevices.Where(d => d.PowerValueCommited > 0).ToList();
                    if (!activeDevices.Any())
                    {
                        LoggerRTM.LogTrace("No active devices to reduce power.");
                        return;
                    }

                    var devices = activeDevices.OrderBy(o => o.Soc).ToList();
                    double totalCommitedActivePower = activeDevices.Sum(d => d.PowerValueCommited);
                    double remainingReductionDouble = remainingReduction;

                    foreach (var device in devices)
                    {
                        if (remainingReduction <= 0 || device.PowerValueCommited <= 0)
                            continue;

                        double powerShare = device.PowerValueCommited / totalCommitedActivePower;
                        int deviceReduction = (int)(remainingReductionDouble * powerShare);

                        int maxDeviceReduction = Math.Min(device.PowerValueCommited,
                                                       Math.Max(device.PowerValueCommited * baseReductionPercent / 100, 50));
                        int actualReduction = Math.Min(deviceReduction, maxDeviceReduction);

                        if (device.Soc < 20)
                        {
                            actualReduction = Math.Min(device.PowerValueCommited, remainingReduction);
                        }

                        int newPower = Math.Max(0, device.PowerValueCommited - actualReduction);

                        LoggerRTM.LogTrace("Device {DeviceSn} reducing power by {Reduction}W. Setting to: {NewPower}W",
                            device.DeviceSn, actualReduction, newPower);

                        device.PowerValueRequested = newPower;
                        await GrowattDeviceQueryQueueWatchdog.EnqueueAsync(new DeviceNoahSetPowerQuery
                        {
                            DeviceType = "noah",
                            DeviceSn = device.DeviceSn,
                            Value = newPower,
                            Force = true,
                            TS = value.TS
                        });

                        remainingReduction -= actualReduction;
                    }
                }
            }
            // Im Zielbereich: keine Leistungsregelung, optionales Loadbalancing
            else
            {
                LoggerRTM.LogTrace("No adjustment needed. TotalPower within target range.");

                // --- Optionales SoC-basiertes Loadbalancing bei größerer Differenz (z.B. >5%) ---
                var devices = onlineDevices.ToList();
                var maxSocDev = devices.OrderByDescending(d => d.Soc).First();
                var minSocDev = devices.OrderBy(d => d.Soc).First();

                if (maxSocDev.Soc - minSocDev.Soc >= 5)
                {
                    int shift = 10; // z.B. 10 W umschichten

                    int maxShiftUp = (ApiSettingMaxPower / devices.Count) - maxSocDev.PowerValueCommited;
                    int maxShiftDown = minSocDev.PowerValueCommited; // nur bis 0W

                    int actualShift = Math.Min(shift, Math.Min(maxShiftUp, maxShiftDown));

                    if (actualShift > 0)
                    {
                        LoggerRTM.LogTrace("Loadbalancing: Umschichten von {Shift}W von Device {MinDev} (SoC {MinSoc}%) zu Device {MaxDev} (SoC {MaxSoc}%)",
                            actualShift, minSocDev.DeviceSn, minSocDev.Soc, maxSocDev.DeviceSn, maxSocDev.Soc);

                        minSocDev.PowerValueRequested = minSocDev.PowerValueCommited - actualShift;
                        maxSocDev.PowerValueRequested = maxSocDev.PowerValueCommited + actualShift;

                        await GrowattDeviceQueryQueueWatchdog.EnqueueAsync(new DeviceNoahSetPowerQuery
                        {
                            DeviceType = "noah",
                            DeviceSn = minSocDev.DeviceSn,
                            Value = minSocDev.PowerValueRequested,
                            Force = true,
                            TS = value.TS
                        });

                        await GrowattDeviceQueryQueueWatchdog.EnqueueAsync(new DeviceNoahSetPowerQuery
                        {
                            DeviceType = "noah",
                            DeviceSn = maxSocDev.DeviceSn,
                            Value = maxSocDev.PowerValueRequested,
                            Force = true,
                            TS = value.TS
                        });
                    }
                    else
                    {
                        LoggerRTM.LogTrace("Loadbalancing: Kein Spielraum zum Umschichten.");
                    }
                }
                else
                {
                    LoggerRTM.LogTrace("Loadbalancing: SoC-Differenz <5%, keine Aktion.");
                }
            }

            LoggerRTM.LogTrace("Final adjustments completed.");

            await Task.CompletedTask;
        }
    }
}
