using EnergyAutomate.Services.CodeFactory;

namespace EnergyAutomate.RuntimeTemplates;

public sealed class DefaultDistributionManagerScript
{
    public async Task ExecuteAsync(IEnergyDistributionManagerScriptFactory factory)
    {
        if (factory.Event.Devices.Count == 0)
        {
            factory.Trace("No devices available for distribution manager.");
            return;
        }

        if (factory.Event.TotalPower < 200 && factory.Event.Devices.Count > 1)
        {
            var mainDevice = factory.Event.Devices
                .OrderByDescending(device => device.Soc)
                .First();

            foreach (var device in factory.Event.Devices)
            {
                await factory.RequestPowerAsync(
                    device.DeviceSn ?? string.Empty,
                    device == mainDevice ? factory.Event.TotalPower : 0);
            }

            factory.Trace("Low power rule applied by distribution manager.");
            return;
        }

        await factory.RunWeightedDistributionAsync();
    }
}
