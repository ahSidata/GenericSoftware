using System;
using EnergyAutomate.Services.CodeFactory;

namespace EnergyAutomate.RuntimeTemplates;

public sealed class SocWeightedDistributionScript
{
    public async Task ExecuteAsync(IEnergyDistributionScriptFactory factory)
    {
        var devices = factory.OnlineNoahDevices
            .Where(device => !factory.IsBatteryFull(device.DeviceSn ?? string.Empty))
            .ToList();

        if (devices.Count == 0)
        {
            factory.Trace("No eligible devices for SOC weighted distribution.");
            return;
        }

        var totalWeight = devices.Sum(device => Math.Max(1, 100 - factory.GetSoc(device.DeviceSn ?? string.Empty)));
        var distribution = devices.ToDictionary(
            device => device.DeviceSn!,
            device => factory.Event.RequestedTotalPower * Math.Max(1, 100 - factory.GetSoc(device.DeviceSn ?? string.Empty)) / totalWeight);

        await factory.RequestPowerDistributionAsync(distribution);
    }
}
