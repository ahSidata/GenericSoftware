using EnergyAutomate.Services.CodeFactory;

namespace EnergyAutomate.RuntimeTemplates;

public sealed class BatteryPriorityAdjustmentScript
{
    public async Task ExecuteAsync(IEnergyAdjustmentScriptFactory factory)
    {
        await factory.SetBatteryPriorityAsync();
        await factory.ClearPowerRequestsAsync();
        factory.SetActiveCondition("BatteryPriority_ClearPower");
    }
}
