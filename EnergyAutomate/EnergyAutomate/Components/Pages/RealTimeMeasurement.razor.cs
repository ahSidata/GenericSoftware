using BlazorBootstrap;
using EnergyAutomate.Components.Layout;
using EnergyAutomate.Definitions;
using EnergyAutomate.Extentions;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Collections.Specialized;
using System.Data;
using System.Diagnostics;

namespace EnergyAutomate.Components.Pages
{
    public partial class RealTimeMeasurement : IDisposable
    {
        #region Fields

        private readonly IEnumerable<TickMark> ApiDataReadsDelaySecTickList = ApiService.GenerateTickTickMarks(0, 600, 60);
        private readonly IEnumerable<TickMark> ApiOffsetAvgTickList = ApiService.GenerateTickTickMarks(-25, 150, 5);

        private readonly IEnumerable<TickMark> ApiSettingTimeOffsetTickList = ApiService.GenerateTickTickMarks(-12, 12, 1);
        private readonly IEnumerable<TickMark> ApiToleranceAvgTickList = ApiService.GenerateTickTickMarks(0, 300, 10);
        private readonly IEnumerable<TickMark> AvgPowerLoadSecondsTickList = ApiService.GenerateTickTickMarks(0, 180, 5);
        private Tabs tabsMainRef = default!;

        #endregion Fields

        #region Properties

        [CascadingParameter]
        private MainLayout? MainLayout { get; set; }

        #endregion Properties

        #region Public Methods

        public void Dispose()
        {
            Logger.LogTrace("Dispose RealTimeMeasurement");
            ApiService.StateHasChanged -= ApiService_StateHasChanged;
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
            ApiService.TibberRealTimeMeasurementRegisterOnCollectionChanged(this, RealTimeMeasurement_CollectionChanged);
            ApiService.StateHasChanged += ApiService_StateHasChanged;
        }

        #endregion Protected Methods

        #region Private Methods



        private async void ApiService_StateHasChanged(object? sender, EventArgs e)
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

        private List<string>? PriceBackgroundColors { get; set; }

        private void GetDeviceData()
        {
            var dataSource = ApiService.TibberListRealTimeMeasurement().OrderByDescending(x => x.TS).Take(61).Reverse().ToList();

            List<double?>? GetDeviceData( string deviceSn, string propertyName)
            {
                switch(propertyName)
                {
                    case "Requested":
                        return dataSource.Select(x => x.PowerValueNewDeviceSn == deviceSn ? (double?)x.PowerValueNewRequested : null).ToList();
                    default:
                        return new List<double?>();
                }
            }            

            deviceData = new ChartData
            {
                Labels = dataSource.Select((x, index) => index % 5 == 0 ? x.Timestamp.TimeOfDay.ToString() : string.Empty).ToList(),
                Datasets = new List<IChartDataset>()
                {
                    new LineChartDataset()
                    {
                        Label = "Total Commited",
                        Data = dataSource.Select(x => (double?)x.PowerValueTotalCommited).ToList(),
                        BackgroundColor = "rgb(0, 255, 0)",
                        BorderColor = "rgb(0, 255, 0)",
                        BorderWidth = 4,
                        PointRadius = new List<double>() { 0 },
                        Stepped = true,
                        Order = 14
                    },
                    new LineChartDataset()
                    {
                        Label = "Total Requested",
                        Data = dataSource.Select(x => (double?)x.PowerValueTotalRequested).ToList(),
                        BackgroundColor = "rgb(255, 0, 0)",
                        BorderColor = "rgb(255, 0, 0)",
                        BorderWidth = 4,
                        PointRadius = new List<double>() { 0 },
                        Stepped = true,
                        Order = 13
                    },
                    new LineChartDataset()
                    {
                        Label = "New Requested",
                        Data = dataSource.Select(x => (double?)x.PowerValueNewRequested).ToList(),
                        BackgroundColor = "rgb(255, 0, 0)",
                        BorderColor = "rgb(255, 0, 0)",
                        BorderWidth = 2,
                        PointRadius = new List<double>() { 0 },
                        Stepped = true,
                        Fill = true,
                        Order = 12
                    },
                    new LineChartDataset()
                    {
                        Label = "New Commited",
                        Data = dataSource.Select(x => (double?)x.PowerValueNewCommited).ToList(),
                        BackgroundColor = "rgb(0, 255, 0)",
                        BorderColor = "rgb(0, 255, 0)",
                        BorderWidth = 2,
                        PointRadius = new List<double>() { 0 },
                        Stepped = true,
                        Fill = true,
                        Order = 11
                    }
                },
            };

            Dictionary<int, string> ColorSet = new Dictionary<int, string>() { { 1, "rgb(0, 0, 255)" }, { 2, "rgb(0, 150, 255)" } };

            var index = 1;
            foreach(var device in ApiService.GrowattAllNoahDevices())
            {
                deviceData.Datasets.Add(new LineChartDataset()
                {
                    Label = $"Noah({device.DeviceSn}) Requested",
                    Data = GetDeviceData(device.DeviceSn, "Requested"),
                    BackgroundColor = ColorSet[index],
                    BorderColor = ColorSet[index],
                    BorderWidth = 2,
                    HoverBorderWidth = 4,
                    Stepped = true,
                    Order = index
                });
                index++;
            }
        }

        private void GetPriceData()
        {
            var dataItems = ApiService.TibberGetPriceDatas();
            var today = DateTimeOffset.UtcNow;

            // Offset auslesen
            TimeSpan offset = dataItems.FirstOrDefault()?.StartsAt.Offset ?? new TimeSpan(0);

            // Beispiel UTC-Zeit
            DateTime utcTime = DateTime.UtcNow;

            // Offset auf UTC-Zeit addieren
            DateTimeOffset currentTime = new DateTimeOffset(DateTime.UtcNow).ToOffset(offset);

            var dataPoints = dataItems.Select(x => (double?)x.Total).ToList();

            var dataToday = dataItems.Take(24).ToList();
            var dataTomorrow = dataItems.Skip(24).Take(24).ToList();
            double? avgToday = dataToday.Average(x => (double?)x.Total);
            double? avgTomorrow = dataTomorrow.Average(x => (double?)x.Total);

            var dataAvgPoints = dataToday.Select(x => (double?)x.Total < avgToday ? (double?)x.Total : null)
                .Concat(dataTomorrow.Select(x => (double?)x.Total < avgTomorrow ? (double?)x.Total : null)).ToList();

            var dataAvgLinePoints = dataToday.Select(x => (double?)x.Total < avgToday ? (double?)x.Total : avgToday)
                .Concat(dataTomorrow.Select(x => (double?)x.Total < avgTomorrow ? (double?)x.Total : avgTomorrow)).ToList();

            var highFactor = 4;

            var dataHighPrices = dataToday.Select(x => (int)(x.Level ?? 0) > 2 ? avgToday / highFactor : null).Concat(dataTomorrow.Select(x => (int)(x.Level ?? 0) > 2 ? avgTomorrow / highFactor : null)).ToList();
            var dataLowPrices = dataToday.Select(x => (int)(x.Level ?? 0) > 0 && (int)(x.Level ?? 0) < 3 ? avgToday / highFactor : null).Concat(dataTomorrow.Select(x => (int)(x.Level ?? 0) > 0 && (int)(x.Level ?? 0) < 3 ? avgTomorrow / highFactor : null)).ToList();

            var dataCurrentHour = dataToday.Select((x, index) =>
                index < 25 &&
                (x.StartsAt.Date == currentTime.Date && x.StartsAt.Hour == currentTime.Hour) || 
                (x.StartsAt.Date == currentTime.Date && x.StartsAt.Hour == currentTime.Hour + 1)
                ? (double?)x.Total : null
                ).Concat(dataTomorrow.Select(x => (double?)null)).ToList();

            priceData = new ChartData
            {
                Labels = dataItems.Select(x => x.StartsAt.Hour.ToString()).ToList(),
                Datasets = new List<IChartDataset>()
                {
                    new LineChartDataset()
                    {
                        Label = "Price",
                        Data = dataPoints,                      
                        Fill = true,
                        Stepped = true,
                        Order = 5
                    }.SetDefaultStyle("rgb(255, 255, 0)"),
                    new LineChartDataset()
                    {
                        Label = "Avg",
                        Data = dataAvgPoints,
                        Fill = true,
                        Stepped = true,
                        Order = 4
                    }.SetDefaultStyle("rgb(190, 190, 190)"),
                    new LineChartDataset()
                    {
                        Label = "Avg",
                        Data = dataAvgLinePoints,
                        Fill = true,
                        Stepped = true,
                        Order = 4
                    }.SetDefaultStyle("rgb(190, 190, 190)", radius: 0),
                    new LineChartDataset()
                    {
                        Label = "Current Hour",
                        Data = dataCurrentHour,
                        Fill = true,
                        Stepped = true,
                        Order = 3
                    }.SetDefaultStyle("rgb(0, 0, 255)"),
                    new LineChartDataset()
                    {
                        Label = "Avg high price",
                        Data = dataHighPrices,
                        Stepped = true,
                        Fill = true,
                        Order = 2
                    }.SetDefaultStyle(backgroundColor: "rgb(255, 0, 0)", radius: 0, borderWidth: 0),
                    new LineChartDataset()
                    {
                        Label = "Avg low price",
                        Data = dataLowPrices,
                        Stepped = true,
                        Fill = true,
                        Order = 2
                    }.SetDefaultStyle(backgroundColor: "rgb(0, 255, 0)", radius: 0, borderWidth: 0),
                    new LineChartDataset()
                    {
                        Label = "AvgLine",
                        Data = dataAvgLinePoints,
                        Stepped = true,
                        Order = 1
                    }.SetDefaultStyle("rgb(0, 0, 0)", radius: 0, borderWidth: 3)
                }
            };
        }

        private void GetRealTimeMeasurementData()
        {
            var dataSource = ApiService.TibberListRealTimeMeasurement().OrderByDescending(x => x.Timestamp).Take(61).Reverse().ToList();

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
                        Data = dataSource.Select(x => (double?)x.PowerProduction).Select(s => -s).ToList(),
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
                        Data = dataSource.Select(x => (double?)x.PowerAvgConsumption).ToList(),
                        BackgroundColor = "rgb(0, 0, 0)",
                        BorderColor = "rgb(0, 0, 0)",
                        BorderWidth = 2,
                        PointRadius = new List<double>() { 0 },
                        Order = 3
                    },
                    new LineChartDataset()
                    {
                        Label = "AvgPowerProduction",
                        Data = dataSource.Select(x => (double?)x.PowerAvgProduction).Select(s => -s).ToList(),
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
                        Data = dataSource.Select(x => (double?)x.SettingOffSetAvg + (x.SettingAvgPowerHysteresis/2)).ToList(),
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
                        Data = dataSource.Select(x => (double?)x.SettingOffSetAvg - (x.SettingAvgPowerHysteresis/2)).ToList(),
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

        private async void RealTimeMeasurement_CollectionChanged()
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

        #endregion Growatt
    }
}
