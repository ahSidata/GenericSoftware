using EnergyAutomate.Services.CodeFactory;

namespace EnergyAutomate.RuntimeTemplates;

public sealed class SingleDeviceOptimizationScript
{
    public async Task ExecuteAsync(IEnergyDistributionScriptFactory factory)
    {
        var device = factory.OnlineNoahDevices
            .Where(item => !factory.IsBatteryFull(item.DeviceSn ?? string.Empty))
            .OrderBy(item => factory.GetCurrentCommittedPower(item.DeviceSn ?? string.Empty))
            .FirstOrDefault();

        if (device?.DeviceSn is null)
        {
            factory.Trace("No eligible device for single device optimization.");
            return;
        }

        await factory.RequestPowerAsync(device.DeviceSn, Math.Min(factory.Event.RequestedTotalPower, factory.MaxPower));
    }
}
