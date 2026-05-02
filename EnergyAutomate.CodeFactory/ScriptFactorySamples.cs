namespace EnergyAutomate.Services.CodeFactory;

/// <summary>
/// Code samples for Script Factory documentation
/// </summary>
public static class ScriptFactorySamples
{
    public const string Structure = """
public class MyScript
{
    public async Task ExecuteAsync(IEnergyCalculationScriptFactory factory)
    {
        // Your logic here
        await Task.CompletedTask;
    }
}
""";

    public const string GlobalParameter = """
var latitude = factory.GetGlobalParameter("ApiSettings:Latitude");
var longitude = factory.GetGlobalParameter("ApiSettings:Longitude");
""";

    public const string RuntimeState = """
// Read state
var lastUpdate = factory.GetRuntimeState("lastUpdate");

// Update state
await factory.UpdateRuntimeStateAsync("lastUpdate", DateTime.UtcNow.ToString());
""";

    public const string Logging = """
factory.LogInformation("Processing started");
factory.LogWarning("Temperature above threshold");
factory.LogError("Device communication failed");
factory.LogTrace("Debug information");
""";

    public const string Measurements = """
// Access current measurements
var currentPower = factory.Measurements.CurrentPower;
var totalEnergy = factory.Measurements.TotalEnergy;
var voltage = factory.Measurements.Voltage;

// Iterate through all measurements
foreach (var measurement in factory.Measurements.History)
{
    factory.LogInformation($"Power: {measurement.Power}W");
}
""";

    public const string AverageConsumption = """
// Get average consumption over last 300 seconds
var avgPower = factory.GetAverageConsumption(300);
factory.LogInformation($"Average power: {avgPower}W");
""";

    public const string SetCalculatedValue = """
var result = await factory.SetCalculatedValueAsync(
    "monthly_savings",
    1250.50);

if (result.Success)
{
    factory.LogInformation("Value saved successfully");
}
""";

    public const string SetPowerAsync = """
// Set power for specific device
await factory.SetPowerAsync("0PVP50ZR16ST00CB", 500);

// Check result
factory.LogInformation("Power set to 500W");
""";

    public const string SetPowerForAllNoahs = """
// Distribute 2000W total power among all online Noah devices
await factory.SetPowerForAllNoahsAsync(2000);
""";

    public const string BatteryPriority = """
// Enable battery priority mode
await factory.SetBatteryPriorityAsync();

// Enable load priority mode
await factory.SetLoadPriorityAsync();
""";

    public const string CheckCondition = """
var condition = factory.CheckCondition();
if (condition == "charging")
{
    factory.LogInformation("Device is currently charging");
    await factory.SetActiveCondition("reduce_output");
}
""";

    public const string OnlineNoahDevices = """
var devices = factory.OnlineNoahDevices;
factory.LogInformation($"Found {devices.Count} online Noah devices");

foreach (var device in devices)
{
    factory.LogInformation($"Device SN: {device.DeviceSn}");
}
""";

    public const string GetSoc = """
var soc = factory.GetSoc("0PVP50ZR16ST00CB");
factory.LogInformation($"State of Charge: {soc}%");

if (soc < 20)
{
    factory.LogWarning("Battery level low - consider charging");
}
""";

    public const string RequestPowerDistribution = """
var distribution = await factory.RequestPowerDistributionAsync();

foreach (var device in distribution.Allocations)
{
    factory.LogInformation(
        $"Device {device.DeviceSn}: {device.AllocatedPower}W");
}
""";
}
