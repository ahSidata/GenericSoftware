using EnergyAutomate.Services.CodeFactory;

namespace EnergyAutomate.RuntimeTemplates;

public sealed class AveragePowerCalculationScript
{
    public async Task ExecuteAsync(IEnergyCalculationScriptFactory factory)
    {
        var avgConsumption = factory.GetAverageConsumption(60);
        var avgProduction = factory.GetAverageProduction(60);

        await factory.SetCalculatedValueAsync("PowerAvgConsumption", avgConsumption);
        await factory.SetCalculatedValueAsync("PowerAvgProduction", avgProduction);

        factory.Trace($"Calculated averages. Consumption={avgConsumption}; Production={avgProduction}");
    }
}
