using EnergyAutomate.Services.CodeFactory;

namespace EnergyAutomate.RuntimeTemplates;

public sealed class RestrictionModeAdjustmentScript
{
    public async Task ExecuteAsync(IEnergyAdjustmentScriptFactory factory)
    {
        if (!factory.RestrictionMode || !factory.Event.IsExpensiveRestrictionMode)
        {
            factory.Trace("Restriction mode is not active.");
            return;
        }

        await factory.SetBatteryPriorityAsync();
        await factory.SetPowerForAllNoahsAsync(0);
        factory.SetActiveCondition("RestrictionMode_ZeroInjection");
    }
}
