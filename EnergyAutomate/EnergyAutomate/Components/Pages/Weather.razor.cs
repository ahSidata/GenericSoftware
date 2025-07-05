using BlazorBootstrap;
using OpenMeteo;
using System.Diagnostics;

namespace EnergyAutomate.Components.Pages
{
    public partial class Weather
    {
        private LineChart radiationChart = default!;
        private ChartData radiationData = default!;
        private LineChart cloudcoverChart = default!;
        private ChartData cloudcoverData = default!;

        private bool isWeatherChartInitialized;

        #region Properties

        public WeatherForecast? WeatherForecast { get; set; }

        #endregion Properties

        #region Protected Methods

        protected override async Task OnInitializedAsync()
        {
            WeatherForecast = await ApiService.CurrentState.GetWeatherForecastAsync();

            await base.OnInitializedAsync();
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                isWeatherChartInitialized = false;
                await RenderChartsAsync();
            }

            await base.OnAfterRenderAsync(firstRender);
        }

        #endregion Protected Methods

        private async Task GetWeatherData()
        {
            var weatherForecast = await ApiService.CurrentState.GetWeatherForecastAsync();
            var label_values = weatherForecast?.Hourly?.Time?.Select(s => DateTime.Parse(s).ToString("HH:mm")).ToList();
            var direct_radiation_instant = weatherForecast?.Hourly?.Direct_radiation_instant?.Select(s => (double?)s).ToList();
            var charge_window = weatherForecast?.Hourly?.Direct_radiation_instant?.Select(s => (double?)s).ToList();
            var hourlyIndex = weatherForecast?.Hourly?.Time?.Select(s => DateTime.Parse(s).ToUniversalTime()).ToArray();

            if (charge_window != null && hourlyIndex != null)
            {

                for (int i = 0; i < charge_window.Count; i++)
                {
                    if
                    (
                        !(
                        hourlyIndex[i].Hour > ApiService.CurrentState.BatteryChargeStart.Hour ||
                        hourlyIndex[i].Hour < ApiService.CurrentState.BatteryChargeEnd.Hour
                        )
                    )
                    {
                        charge_window[i] = null;
                    }
                }
            }

            var cloudcover_low = weatherForecast?.Hourly?.Cloudcover_low?.Select(s => (double?)s).ToList();
            var cloudcover_mid = weatherForecast?.Hourly?.Cloudcover_mid?.Select(s => (double?)s).ToList();
            var cloudcover_high = weatherForecast?.Hourly?.Cloudcover_high?.Select(s => (double?)s).ToList();

            var dataSource =

            radiationData = new ChartData
            {
                Labels = label_values,
                Datasets = new List<IChartDataset>()
                {
                    new LineChartDataset()
                    {

                        Label = "charge_window",
                        Data = charge_window,
                        BackgroundColor = "rgba(0, 255, 0, 0.5)",
                        BorderColor = "rgb(0, 255, 0)",
                        BorderWidth = 2,
                        PointRadius = new List<double>() { 0 },
                        Stepped = true,
                        Fill = true,
                        Order = 2
                    },
                    new LineChartDataset()
                    {

                        Label = "direct_radiation_instant",
                        Data = direct_radiation_instant,
                        BackgroundColor = "rgb(255, 0, 0)",
                        BorderColor = "rgb(255, 0, 0)",
                        BorderWidth = 2,
                        PointRadius = new List<double>() { 0 },
                        Stepped = true,
                        Order = 1
                    }
                },
            };

            cloudcoverData = new ChartData
            {
                Labels = label_values,
                Datasets = new List<IChartDataset>()
                {
                    new LineChartDataset()
                    {
                        Label = "cloudcover_low",
                        Data = cloudcover_low,
                        BackgroundColor = "rgba(173,216,230, 0.5)",
                        BorderColor = "rgb(173,216,230)",
                        BorderWidth = 2,
                        PointRadius = new List<double>() { 2 },
                        Stepped = true,
                        Fill = true,
                        Order = 1
                    },
                    new LineChartDataset()
                    {
                        Label = "cloudcover_mid",
                        Data = cloudcover_mid,
                        BackgroundColor = "rgba(0, 0, 255, 0.5)",
                        BorderColor = "rgb(0, 0, 255)",
                        BorderWidth = 2,
                        PointRadius = new List<double>() { 2 },
                        Stepped = true,
                        Fill = true,
                        Order = 2
                    },
                    new LineChartDataset()
                    {
                        Label = "cloudcover_high",
                        Data = cloudcover_high,
                        BackgroundColor = "rgba(0, 0, 155, 0.5)",
                        BorderColor = "rgb(0, 0, 155)",
                        BorderWidth = 2,
                        PointRadius = new List<double>() { 2 },
                        Stepped = true,
                        Fill = true,
                        Order = 3
                    }
                },
            };
        }

        private async Task RenderChartsAsync()
        {
            await GetWeatherData();

            var radiationChartOptions = new LineChartOptions();

            radiationChartOptions.Interaction.Mode = InteractionMode.Index;
            radiationChartOptions.Responsive = true;
            radiationChartOptions.Scales.Y = new ChartAxes() { Min = 0, Max = 800 };
            radiationChartOptions.Scales.X!.Title = new ChartAxesTitle { Text = "Time", Display = true };
            radiationChartOptions.Scales.Y!.Title = new ChartAxesTitle { Text = "Watt", Display = true };
            radiationChartOptions.MaintainAspectRatio = false;
            await radiationChart.InitializeAsync(chartData: radiationData, chartOptions: radiationChartOptions);
            var cloudcoverChartOptions = new LineChartOptions();

            cloudcoverChartOptions.Interaction.Mode = InteractionMode.Index;
            cloudcoverChartOptions.Responsive = true;
            cloudcoverChartOptions.Scales.Y = new ChartAxes() { Min = 0, Max = 100 };
            cloudcoverChartOptions.Scales.X!.Title = new ChartAxesTitle { Text = "Time", Display = true };
            cloudcoverChartOptions.Scales.Y!.Title = new ChartAxesTitle { Text = "Percent", Display = true };
            cloudcoverChartOptions.MaintainAspectRatio = false;
            await cloudcoverChart.InitializeAsync(chartData: cloudcoverData, chartOptions: cloudcoverChartOptions);

            isWeatherChartInitialized = true;
        }

        private async Task UpdateChartsAsync()
        {
            try
            {
                if (isWeatherChartInitialized && radiationChart != null)
                {
                    await GetWeatherData();
                    if (radiationData != null)
                    {
                        await radiationChart.UpdateValuesAsync(radiationData);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in UpdateChartsAsync: {ex.Message}");
            }
        }
    }
}
