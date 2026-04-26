using EnergyAutomate.Services.CodeFactory;

namespace EnergyAutomate.RuntimeTemplates;

public sealed class EqualDistributionScript
{
    public async Task ExecuteAsync(IEnergyDistributionScriptFactory factory)
    {
        var devices = factory.OnlineNoahDevices
            .Where(device => !factory.IsBatteryFull(device.DeviceSn ?? string.Empty))
            .ToList();

        if (devices.Count == 0)
        {
            factory.Trace("No eligible devices for distribution.");
            return;
        }

        var powerPerDevice = factory.Event.RequestedTotalPower / devices.Count;
        var maxPerDevice = factory.MaxPower / devices.Count;

        var distribution = devices.ToDictionary(
            device => device.DeviceSn!,
            device => Math.Min(powerPerDevice, maxPerDevice));

        await factory.RequestPowerDistributionAsync(distribution);
    }
}
