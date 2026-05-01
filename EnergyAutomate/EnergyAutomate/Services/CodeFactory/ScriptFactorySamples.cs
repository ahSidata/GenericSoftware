namespace EnergyAutomate.Services.CodeFactory;

/// <summary>
/// Contains sample code snippets for the Script Factory documentation.
/// </summary>
public static class ScriptFactorySamples
{
    public const string Structure = """
public class MyScript
{
    public async Task ExecuteAsync(IEnergyAdjustmentScriptFactory factory)
    {
        factory.Info("Script started");
        var power = factory.AveragePower;
        await factory.SetPowerForAllNoahsAsync(power);
        factory.Info("Script completed");
    }
}
""";

    public const string GlobalParameter = """
var setting = factory.GetGlobalParameter("ConfigKey");
""";

    public const string RuntimeState = """
var previousCount = factory.GetRuntimeState("Count") as int? ?? 0;
await factory.UpdateRuntimeStateAsync("Count", previousCount + 1, true);
""";

    public const string Logging = """
factory.Trace("Debug message");
factory.Info("Info message");
factory.Warning("Warning message");
factory.Error("Error message");
""";

    public const string Measurements = """
var allMeasurements = factory.Measurements;
factory.Info("Total measurements: " + allMeasurements.Count);
foreach (var m in allMeasurements) {
    factory.Trace("Power: " + m.Power + "W");
}
""";

    public const string AverageConsumption = """
var avgConsumption = factory.GetAverageConsumption(300);
factory.Info("Average: " + avgConsumption + "W");
if (avgConsumption > 5000) {
    factory.Warning("High consumption");
}
""";

    public const string SetCalculatedValue = """
var calculated = factory.GetAverageConsumption(300);
await factory.SetCalculatedValueAsync("AvgConsumption", calculated);
""";

    public const string SetPowerAsync = """
await factory.SetPowerAsync("SN123456", 5000);
factory.Info("Power set to 5000W");
""";

    public const string SetPowerForAllNoahs = """
var avgPower = factory.AveragePower;
await factory.SetPowerForAllNoahsAsync(avgPower);
factory.Info("Power distributed: " + avgPower + "W");
""";

    public const string BatteryPriority = """
if (factory.BatteryPriorityMode) {
    await factory.SetBatteryPriorityAsync();
    factory.Info("Battery priority enabled");
}
await factory.SetLoadPriorityAsync(3000);
""";

    public const string CheckCondition = """
if (factory.CheckCondition("PeakHours")) {
    factory.Info("Peak hours active");
    await factory.SetPowerForAllNoahsAsync(1000);
}
factory.SetActiveCondition("OffPeakHours");
""";

    public const string OnlineNoahDevices = """
var devices = factory.OnlineNoahDevices;
factory.Info("Online devices: " + devices.Count);
foreach (var device in devices) {
    var soc = factory.GetSoc(device.DeviceSn);
    factory.Info("Device " + device.DeviceSn + ": SOC " + soc + "%");
}
""";

    public const string GetSoc = """
foreach (var device in factory.OnlineNoahDevices) {
    var soc = factory.GetSoc(device.DeviceSn);
    if (soc < 20) {
        factory.Warning("Low battery: " + soc + "%");
    }
}
""";

    public const string RequestPowerDistribution = """
var distribution = new Dictionary<string, int>();
int powerPerDevice = factory.MaxPower / factory.OnlineNoahDevices.Count;
foreach (var device in factory.OnlineNoahDevices) {
    distribution[device.DeviceSn] = powerPerDevice;
}
await factory.RequestPowerDistributionAsync(distribution);
""";
}
