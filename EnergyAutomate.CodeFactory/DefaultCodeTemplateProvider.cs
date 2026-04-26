using System.Reflection;

namespace EnergyAutomate.Services.CodeFactory;

public sealed class DefaultCodeTemplateProvider : ICodeTemplateProvider
{
    public IReadOnlyList<CodeTemplateDefinition> GetTemplates() =>
    [
        new(
            "Calculation",
            "calculation.average-power",
            "Average Power Calculation",
            "Calculates derived average consumption and production values from recent measurements.",
            "csharp",
            LoadTemplate("Calculation.AveragePowerCalculation.cs")),
        new(
            "Calculation",
            "calculation.production-consumption-split",
            "Production/Consumption Split",
            "Calculates net power values and separates production from consumption.",
            "csharp",
            LoadTemplate("Calculation.ProductionConsumptionSplit.cs")),
        new(
            "Adjustment",
            "adjustment.auto-mode",
            "Auto Mode Adjustment",
            "Decides the target operating mode and requested total power based on realtime values.",
            "csharp",
            LoadTemplate("Adjustment.AutoModeAdjustment.cs")),
        new(
            "Adjustment",
            "adjustment.restriction-mode",
            "Restriction Mode Adjustment",
            "Handles expensive price windows by switching to battery priority and zero requested power.",
            "csharp",
            LoadTemplate("Adjustment.RestrictionModeAdjustment.cs")),
        new(
            "Adjustment",
            "adjustment.battery-priority",
            "Battery Priority Adjustment",
            "Forces battery priority mode and clears output power requests.",
            "csharp",
            LoadTemplate("Adjustment.BatteryPriorityAdjustment.cs")),
        new(
            "Distribution",
            "distribution.equal",
            "Equal Distribution",
            "Distributes requested total power equally over all eligible online Noah devices.",
            "csharp",
            LoadTemplate("Distribution.EqualDistribution.cs")),
        new(
            "Distribution",
            "distribution.soc-weighted",
            "SOC Weighted Distribution",
            "Distributes more requested power to devices with lower state of charge.",
            "csharp",
            LoadTemplate("Distribution.SocWeightedDistribution.cs")),
        new(
            "Distribution",
            "distribution.single-device-optimization",
            "Single Device Optimization",
            "Requests power from the currently least loaded eligible device first.",
            "csharp",
            LoadTemplate("Distribution.SingleDeviceOptimization.cs")),
        new(
            "DistributionManager",
            "distribution-manager.default",
            "Default Distribution Manager",
            "Coordinates high-level distribution rules before concrete device power requests are enqueued.",
            "csharp",
            LoadTemplate("DistributionManager.DefaultDistributionManager.cs"))
    ];

    private static string LoadTemplate(string resourceSuffix)
    {
        var assembly = typeof(DefaultCodeTemplateProvider).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(resourceSuffix, StringComparison.Ordinal));

        if (resourceName is null)
        {
            throw new InvalidOperationException($"Embedded template resource '{resourceSuffix}' was not found.");
        }

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded template resource '{resourceSuffix}' could not be opened.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
