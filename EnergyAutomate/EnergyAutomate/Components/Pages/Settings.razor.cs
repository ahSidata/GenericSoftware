using BlazorBootstrap;
using Microsoft.AspNetCore.Components;
using System.Diagnostics.CodeAnalysis;

namespace EnergyAutomate.Components.Pages;

public partial class Settings
{
    private readonly IEnumerable<TickMark> ApiOffsetAvgTickList = ApiService.GenerateTickTickMarks(-25, 150, 5);
    private readonly IEnumerable<TickMark> ApiPowerTickList = ApiService.GenerateTickTickMarks(0, 900, 50);
    private readonly IEnumerable<TickMark> ApiSettingPowerAdjustmentFactorTickList = ApiService.GenerateTickTickMarks(0, 100, 10);
    private readonly IEnumerable<TickMark> ApiSettingPowerAdjustmentWaitCyclesTickList = ApiService.GenerateTickTickMarks(0, 5, 1);
    private readonly IEnumerable<TickMark> ApiToleranceAvgTickList = ApiService.GenerateTickTickMarks(0, 300, 10);
    private readonly IEnumerable<TickMark> AvgPowerLoadSecondsTickList = ApiService.GenerateTickTickMarks(0, 180, 5);

    [Inject]
    public required RuntimeCodeTemplateStore TemplateStore { get; set; }

    [Inject]
    public required ApiRealTimeMeasurementWatchdog RealTimeMeasurementWatchdog { get; set; }

    private string StatusText { get; set; } = "Settings are loaded from database at startup.";

    private async Task SaveSettingsAsync()
    {
        await ApiService.ApiSaveRuntimeSettingsToDatabaseAsync();
        StatusText = $"Settings saved at {DateTimeOffset.Now.LocalDateTime}.";
    }

    private async Task ReloadSettingsAsync()
    {
        await ApiService.ApiLoadRuntimeSettingsFromDatabaseAsync();
        StatusText = $"Settings reloaded at {DateTimeOffset.Now.LocalDateTime}.";
    }

    private IEnumerable<CodeTemplateViewModel> GetTemplates(string topic)
    {
        return TemplateStore.GetTemplates()
            .Where(template => string.Equals(template.Topic, topic, StringComparison.OrdinalIgnoreCase))
            .OrderBy(template => template.DisplayName);
    }
}
