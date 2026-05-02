using System;
using EnergyAutomate.Services.CodeFactory;

namespace EnergyAutomate.RuntimeTemplates;

public sealed class ProductionConsumptionSplitScript
{
    public async Task ExecuteAsync(IEnergyCalculationScriptFactory factory)
    {
        var production = factory.Event.PowerProduction ?? 0;
        var consumption = factory.Event.Power;
        var netPower = consumption - production;

        await factory.SetCalculatedValueAsync("NetPower", netPower);
        await factory.SetCalculatedValueAsync("PowerProduction", production);
        await factory.SetCalculatedValueAsync("PowerConsumption", consumption);

        factory.Trace($"Calculated split. NetPower={netPower}");
    }
}
