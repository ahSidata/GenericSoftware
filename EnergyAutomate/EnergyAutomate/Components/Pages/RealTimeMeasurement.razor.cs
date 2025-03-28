using BlazorBootstrap;
using EnergyAutomate.Components.Layout;
using Microsoft.AspNetCore.Components;
using System.Collections.Specialized;
using System.Diagnostics;

namespace EnergyAutomate.Components.Pages
{
    public partial class RealTimeMeasurement : IDisposable
    {
        #region Fields

        private readonly IEnumerable<TickMark> ApiLockSecondsTickList = GenerateTickTickMarks(100, 1000, 50);
        private readonly IEnumerable<TickMark> ApiMaxPowerTickList = GenerateTickTickMarks(700, 900, 10);
        private readonly IEnumerable<TickMark> ApiOffsetAvgTickList = GenerateTickTickMarks(-25, 75, 5);
        private readonly IEnumerable<TickMark> ApiToleranceAvgTickList = GenerateTickTickMarks(0, 100, 5);
        private readonly IEnumerable<TickMark> AvgPowerLoadSecondsTickList = GenerateTickTickMarks(5, 180, 5);
        private Tabs tabsMainRef = default!;

        #endregion Fields

        #region Properties

        [CascadingParameter]
        private MainLayout? MainLayout { get; set; }

        #endregion Properties

        #region Public Methods

        public void Dispose()
        {
            ApiServiceInfo.RealTimeMeasurement.CollectionChanged -= RealTimeMeasurement_CollectionChanged;
            ApiServiceInfo.Prices.CollectionChanged -= Price_CollectionChanged;
            ApiServiceInfo.Devices.CollectionChanged -= ApiServiceInfo_StateHasChanged;
            ApiServiceInfo.DeviceNoahLastData.CollectionChanged -= ApiServiceInfo_StateHasChanged;
            ApiServiceInfo.StateHasChanged -= ApiServiceInfo_StateHasChanged;
        }

        #endregion Public Methods

        #region Protected Methods

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                isRealTimeMeasurementChartInitialized = false;
                await RenderTibberAsync();
            }

            await base.OnAfterRenderAsync(firstRender);
        }

        protected override void OnInitialized()
        {
            ApiServiceInfo.RealTimeMeasurement.CollectionChanged += RealTimeMeasurement_CollectionChanged;
            ApiServiceInfo.Prices.CollectionChanged += Price_CollectionChanged;
            ApiServiceInfo.Devices.CollectionChanged += ApiServiceInfo_StateHasChanged;
            ApiServiceInfo.DeviceNoahLastData.CollectionChanged += ApiServiceInfo_StateHasChanged;
            ApiServiceInfo.StateHasChanged += ApiServiceInfo_StateHasChanged;
        }

        #endregion Protected Methods

        #region Private Methods

        private static IEnumerable<TickMark> GenerateTickTickMarks(int start, int end, int step)
        {
            var tickMarks = new List<TickMark>();
            for (int i = start; i <= end; i += step)
            {
                tickMarks.Add(new TickMark { Label = i.ToString(), Value = i.ToString() });
            }
            return tickMarks;
        }

        private async void ApiServiceInfo_StateHasChanged(object? sender, EventArgs e)
        {
            await InvokeAsync(StateHasChanged);
        }

        #endregion Private Methods

        #region Tibber

        private LineChart deviceChart = default!;
        private ChartData deviceData = default!;
        private bool isDeviceChartInitialized;
        private bool isPriceChartInitialized;
        private bool isRealTimeMeasurementChartInitialized;
        private LineChart priceChart = default!;
        private ChartData priceData = default!;
        private LineChart realTimeMeasurementChart = default!;
        private ChartData realTimeMeasurementData = default!;
        private Tabs tabsTibberRef = default!;
        private List<string>? PriceBackgroundColors { get; set; }

        private void GetDeviceData()
        {
            var dataSource = ApiServiceInfo.RealTimeMeasurement.OrderByDescending(x => x.Timestamp).Take(61).Reverse().ToList();

            deviceData = new ChartData
            {
                Labels = dataSource.Select((x, index) => index % 5 == 0 ? x.Timestamp.TimeOfDay.ToString() : string.Empty).ToList(),
                Datasets = new List<IChartDataset>()
                {
                    new LineChartDataset()
                    {
                        Label = "AvgOutputValue",
                        Data = dataSource.Select(x => (double?)x.CommitedPowerValue).ToList(),
                        BackgroundColor = "rgb(0, 255, 0)",
                        BorderColor = "rgb(0, 255, 0)",
                        BorderWidth = 2,
                        PointRadius = new List<double>() { 0 },
                        Order = 3
                    }
                },
            };
        }

        private void GetPriceData()
        {
            var todayOnly = DateTime.Now.Hour < 13;
            var hours = Enumerable.Range(0, todayOnly ? 24 : 48).Select(i => i % 24).ToArray();
            List<double?> dataToday = ApiServiceInfo.GetPriceTodayDatas();
            List<double?> dataTomorrow = ApiServiceInfo.GetPriceTomorrowDatas();
            double? avgToday = dataToday.Any() ? dataToday.Average() : 0;
            double? avgTomorrow = dataTomorrow.Any() ? dataTomorrow.Average() : 0;

            var dataPoints = todayOnly ? dataToday : dataToday.Concat(dataTomorrow).ToList();
            var currentHour = DateTime.Now.Hour + 1;

            var dataCurrentHour = dataPoints.Select((point, index) => index < 23 && (hours[index] == currentHour || hours[index + 1] == currentHour) ? point : null).ToList();
            var dataAvgPoints = dataToday.Select(point => point > avgToday ? avgToday : point).Concat(dataTomorrow.Select(point => point > avgTomorrow ? avgTomorrow : point)).ToList();
            var dataAvgLinePoints = Enumerable.Repeat(avgToday, dataToday.Count).Concat(Enumerable.Repeat(avgTomorrow, dataTomorrow.Count)).ToList();

            var avghighPrices = ApiServiceInfo.Prices
                .Where(w => w.AutoModeRestriction == true && w.Total.HasValue)
                .GroupBy(g => g.StartsAt.Date)
                .ToDictionary(g => g.Key, g => (double?)g.Average(a => a.Total));

            var avgHighPricePoints = ApiServiceInfo.Prices
                .OrderBy(x => x.StartsAt)
                .Select(x => x.AutoModeRestriction == true ? avghighPrices[x.StartsAt.Date] : null)
                .ToList();

            priceData = new ChartData
            {
                Labels = hours.Select(x => x.ToString()).ToList(),
                Datasets = new List<IChartDataset>()
                {
                    new LineChartDataset()
                    {
                        Label = "Price",
                        Data = dataPoints,
                        BackgroundColor = "rgb(255, 166, 0)",
                        BorderColor = "rgb(255, 166, 0)",
                        BorderWidth = 2,
                        PointRadius = new List<double>() { 0.0 },
                        HoverBorderWidth = 4,
                        Fill = true,
                        Stepped = true,
                        Order = 6
                    },
                    new LineChartDataset()
                    {
                        Label = "Avg",
                        Data = dataAvgPoints,
                        BackgroundColor = "rgb(88, 80, 141)",
                        BorderWidth = 1,
                        PointRadius = new List<double>() { 1 },
                        HoverBorderWidth = 4,
                        Fill = true,
                        Stepped = true,
                        Order = 5
                    },
                    new LineChartDataset()
                    {
                        Label = "Avg high price",
                        Data = avgHighPricePoints,
                        BackgroundColor = "rgb(0, 128, 255)",
                        BorderColor = "rgb(0, 128, 255)",
                        BorderWidth = 2,
                        PointRadius = new List<double>() { 0.0 },
                        Stepped = true,
                        Fill = true,
                        Order = 3
                    },
                    new LineChartDataset()
                    {
                        Label = "AvgLine",
                        Data = dataAvgLinePoints,
                        BackgroundColor = "rgb(0, 0, 0)",
                        BorderColor = "rgb(0, 0, 0)",
                        BorderWidth = 2,
                        PointRadius = new List<double>() { 0.0 },
                        Stepped = true,
                        Order = 2
                    },
                    new LineChartDataset()
                    {
                        Label = "Current Hour",
                        Data = dataCurrentHour,
                        BackgroundColor = "rgb(255, 0, 0)",
                        BorderColor = "rgb(255, 0, 0)",
                        BorderWidth = 2,
                        PointRadius = new List<double>() { 0 },
                        Fill = true,
                        Stepped = true,
                        Order = 1
                    }
                }
            };
        }

        private void GetRealTimeMeasurementData()
        {
            var dataSource = ApiServiceInfo.RealTimeMeasurement.OrderByDescending(x => x.Timestamp).Take(61).Reverse().ToList();

            var AvgPowerList = dataSource.Select(x => x.TotalPower).ToList();

            realTimeMeasurementData = new ChartData
            {
                Labels = dataSource.Select((x, index) => index % 5 == 0 ? x.Timestamp.TimeOfDay.ToString() : string.Empty).ToList(),
                Datasets = new List<IChartDataset>()
                {
                    new LineChartDataset()
                    {
                        Label = "Consumtion",
                        Data = dataSource.Select(x => (double?)x.Power).ToList(),
                        BackgroundColor = "rgb(88, 80, 141)",
                        BorderColor = "rgb(88, 80, 141)",
                        BorderWidth = 2,
                        HoverBorderWidth = 4,
                        Fill = true,
                        Stepped = true,
                        Order = 5
                    },
                    new LineChartDataset()
                    {
                        Label = "Production",
                        Data = dataSource.Select(x => (double?)x.PowerProduction).Select(s => s *(-1)).ToList(),
                        BackgroundColor = "rgb(255, 166, 0)",
                        BorderColor = "rgb(255, 166, 0)",
                        BorderWidth = 2,
                        HoverBorderWidth = 4,
                        Fill = true,
                        Stepped = true,
                        Order = 4
                    },
                    new LineChartDataset()
                    {
                        Label = "AvgPowerConsumption",
                        Data = dataSource.Select(x => (double?)x.AvgPowerConsumption).ToList(),
                        BackgroundColor = "rgb(0, 0, 0)",
                        BorderColor = "rgb(0, 0, 0)",
                        BorderWidth = 2,
                        PointRadius = new List<double>() { 0 },
                        Order = 3
                    },
                    new LineChartDataset()
                    {
                        Label = "AvgPowerProduction",
                        Data = dataSource.Select(x => (double?)x.AvgPowerProduction).ToList(),
                        BackgroundColor = "rgb(0, 0, 0)",
                        BorderColor = "rgb(0, 0, 0)",
                        BorderWidth = 2,
                        PointRadius = new List<double>() { 0 },
                        Order = 2
                    },
                    new LineChartDataset()
                    {
                        Label = "SettingOffSetAvg",
                        Data = dataSource.Select(x => (double?)x.SettingOffSetAvg).ToList(),
                        BackgroundColor= "rgb(0, 255, 0)",
                        BorderColor = "rgb(255, 0, 0)",
                        BorderWidth = 2,
                        PointRadius = new List<double>() { 0 },
                        Stepped = true,
                        Order = 1
                    },
                    new LineChartDataset()
                    {
                        Label = "UpperLimit",
                        Data = dataSource.Select(x => (double?)x.SettingOffSetAvg + (x.SettingToleranceAvg/2)).ToList(),
                        BackgroundColor= "rgb(255, 0, 0)",
                        BorderColor = "rgb(255, 0, 0)",
                        BorderWidth = 2,
                        PointRadius = new List<double>() { 0 },
                        Stepped = true,
                        Order = 1
                    },
                    new LineChartDataset()
                    {
                        Label = "LowerLimit",
                        Data = dataSource.Select(x => (double?)x.SettingOffSetAvg - (x.SettingToleranceAvg/2)).ToList(),
                        BackgroundColor= "rgb(255, 0, 0)",
                        BorderColor = "rgb(255, 0, 0)",
                        BorderWidth = 2,
                        PointRadius = new List<double>() { 0 },
                        Stepped = true,
                        Order = 1
                    },
                }
            };
        }

        private async void Price_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (isPriceChartInitialized && priceChart != null)
            {
                GetRealTimeMeasurementData();
                if (priceData != null)
                {
                    await priceChart.UpdateValuesAsync(priceData);
                }
            }
        }

        private async void RealTimeMeasurement_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            try
            {
                if (isRealTimeMeasurementChartInitialized && realTimeMeasurementChart != null)
                {
                    GetRealTimeMeasurementData();
                    if (realTimeMeasurementData != null)
                    {
                        await realTimeMeasurementChart.UpdateValuesAsync(realTimeMeasurementData);
                    }
                }
                if (isDeviceChartInitialized && deviceChart != null)
                {
                    GetDeviceData();
                    if (deviceData != null)
                    {
                        await deviceChart.UpdateValuesAsync(deviceData);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in RealTimeMeasurement_CollectionChanged: {ex.Message}");
            }
        }

        private async Task RenderTibberAsync()
        {
            var realTimeMeasurementChartOptions = new LineChartOptions();

            realTimeMeasurementChartOptions.Interaction.Mode = InteractionMode.Index;
            realTimeMeasurementChartOptions.Plugins.Title!.Text = $"Tibber power consumption";
            realTimeMeasurementChartOptions.Plugins.Title.Display = true;
            realTimeMeasurementChartOptions.Plugins.Title.Font = new ChartFont { Size = 20 };
            realTimeMeasurementChartOptions.Responsive = true;
            realTimeMeasurementChartOptions.Scales.X!.Title = new ChartAxesTitle { Text = "Seconds (one minute)", Display = true };
            realTimeMeasurementChartOptions.Scales.Y!.Title = new ChartAxesTitle { Text = "Watt", Display = true };
            realTimeMeasurementChartOptions.MaintainAspectRatio = false;
            GetRealTimeMeasurementData();
            await realTimeMeasurementChart.InitializeAsync(chartData: realTimeMeasurementData, chartOptions: realTimeMeasurementChartOptions);
            isRealTimeMeasurementChartInitialized = true;

            var priceChartOptions = new LineChartOptions { };
            priceChartOptions.Interaction.Mode = InteractionMode.Index;
            priceChartOptions.Plugins.Title!.Text = "Tibber price forecast";
            priceChartOptions.Plugins.Title.Display = true;
            priceChartOptions.Plugins.Title.Font = new ChartFont { Size = 20 };
            priceChartOptions.Responsive = true;
            priceChartOptions.Scales.X!.Title = new ChartAxesTitle { Text = "Today / Tomorrow", Display = true };
            priceChartOptions.Scales.Y!.Title = new ChartAxesTitle { Text = "Euro", Display = true };
            priceChartOptions.MaintainAspectRatio = false;

            GetPriceData();
            await priceChart.InitializeAsync(chartData: priceData, chartOptions: priceChartOptions);
            isPriceChartInitialized = true;

            var deviceChartOptions = new LineChartOptions();

            deviceChartOptions.Interaction.Mode = InteractionMode.Index;
            deviceChartOptions.Plugins.Title!.Text = $"Device power values";
            deviceChartOptions.Plugins.Title.Display = true;
            deviceChartOptions.Plugins.Title.Font = new ChartFont { Size = 20 };
            deviceChartOptions.Responsive = true;
            deviceChartOptions.Scales.Y = new ChartAxes() { Min = 0, Max = 800 };
            deviceChartOptions.Scales.X!.Title = new ChartAxesTitle { Text = "Seconds (one minute)", Display = true };
            deviceChartOptions.Scales.Y!.Title = new ChartAxesTitle { Text = "Watt", Display = true };
            deviceChartOptions.MaintainAspectRatio = false;
            GetDeviceData();
            await deviceChart.InitializeAsync(chartData: deviceData, chartOptions: deviceChartOptions);
            isDeviceChartInitialized = true;
        }

        #endregion Tibber

        #region Growatt

        private Tabs tabsGrowattRef = default!;

        private async Task ClearDeviceNoahTimeSegments()
        {
            await ApiServiceInfo.InvokeClearDeviceNoahTimeSegments();
        }

        private async Task RefreshDeviceList()
        {
            await ApiServiceInfo.InvokeRefreshDeviceList();
        }

        private async Task RefreshNoahLastData()
        {
            await ApiServiceInfo.InvokeRefreshNoahsLastData();
        }

        private async Task RefreshNoahs()
        {
            await ApiServiceInfo.InvokeRefreshNoahs();
        }

        #endregion Growatt
    }
}
