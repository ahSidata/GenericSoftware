using EnergyAutomate.Services.CodeFactory;

namespace EnergyAutomate.RuntimeTemplates;

public sealed class AutoModeAdjustmentScript
{
    public async Task ExecuteAsync(IEnergyAdjustmentScriptFactory factory)
    {
        if (!factory.Event.IsGrowattOnline)
        {
            factory.Trace("Growatt is offline. Adjustment skipped.");
            return;
        }

        if (factory.RestrictionMode && factory.Event.IsExpensiveRestrictionMode)
        {
            await factory.SetBatteryPriorityAsync();
            await factory.SetPowerForAllNoahsAsync(0);
            factory.SetActiveCondition("Restriction_BatteryPriority_ZeroPower");
            return;
        }

        await factory.SetLoadPriorityAsync(factory.AveragePower);
        factory.SetActiveCondition($"LoadPriority_{factory.AveragePower}");
    }
}
