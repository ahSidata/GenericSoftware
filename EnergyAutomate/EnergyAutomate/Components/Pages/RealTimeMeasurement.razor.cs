using BlazorBootstrap;
using EnergyAutomate.Components.Layout;
using EnergyAutomate.Definitions;
using Microsoft.AspNetCore.Components;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;

namespace EnergyAutomate.Components.Pages
{
    public partial class RealTimeMeasurement : IDisposable
    {
        #region Fields

        private readonly IEnumerable<TickMark> ApiSettingTimeOffsetTickList = GenerateTickTickMarks(-12, 12, 1);
        private readonly IEnumerable<TickMark> ApiLockSecondsTickList = GenerateTickTickMarks(100, 1000, 50);
        private readonly IEnumerable<TickMark> ApiMaxPowerTickList = GenerateTickTickMarks(700, 900, 10);
        private readonly IEnumerable<TickMark> ApiOffsetAvgTickList = GenerateTickTickMarks(-25, 150, 5);
        private readonly IEnumerable<TickMark> ApiToleranceAvgTickList = GenerateTickTickMarks(0, 300, 10);
        private readonly IEnumerable<TickMark> AvgPowerLoadSecondsTickList = GenerateTickTickMarks(0, 180, 5);
        private readonly IEnumerable<TickMark> ApiDataReadsDelaySecTickList = GenerateTickTickMarks(0, 600, 60);
        private Tabs tabsMainRef = default!;

        #endregion Fields

        #region Properties

        [CascadingParameter]
        private MainLayout? MainLayout { get; set; }

        #endregion Properties

        #region Public Methods

        public void Dispose()
        {
            ApiService.GrowattRealTimeMeasurementChanged -= RealTimeMeasurement_CollectionChanged;
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
            ApiService.GrowattRealTimeMeasurementChanged += RealTimeMeasurement_CollectionChanged;
            ApiService.StateHasChanged += ApiService_StateHasChanged;
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
        private Tabs tabsTibberRef = default!;
        private List<string>? PriceBackgroundColors { get; set; }

        private void GetDeviceData()
        {
            var dataSource = ApiService.TibberListRealTimeMeasurement().OrderByDescending(x => x.Timestamp).Take(61).Reverse().ToList();

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
            var dataItems = ApiService.TibberGetPriceDatas();
            var today = DateTimeOffset.UtcNow;
         
            // Offset auslesen
            TimeSpan offset = dataItems.FirstOrDefault()?.StartsAt.Offset ?? new TimeSpan(0);

            // Beispiel UTC-Zeit
            DateTime utcTime = DateTime.UtcNow;

            // Offset auf UTC-Zeit addieren
            DateTimeOffset currentTime = new DateTimeOffset(DateTime.UtcNow).ToOffset(offset);


            var hours = dataItems.Select(s => s.StartsAt.Hour);
            var dataPoints = dataItems.Select(x => (double?)x.Total).ToList();

            var dataToday = dataItems.Take(24).ToList();
            var dataTomorrow = dataItems.Skip(24).Take(24).ToList();
            double? avgToday = dataToday.Average(x => (double?)x.Total);
            double? avgTomorrow = dataTomorrow.Average(x => (double?)x.Total);

            var dataAvgPoints = dataToday.Select(x => (double?)x.Total < avgToday ? (double?)x.Total : avgToday).Concat(dataTomorrow.Select(x => (double?)x.Total < avgTomorrow ? (double?)x.Total : avgTomorrow)).ToList();
            var dataHighPrices = dataToday.Select(x => (int)(x.Level ?? 0) > 2 ? avgToday : null).Concat(dataTomorrow.Select(x => (int)(x.Level ?? 0) > 2 ? avgTomorrow : null)).ToList(); 

            var dataCurrentHour = dataToday.Select((x, index) => 
                index < 25 &&
                ( x.StartsAt.Hour == currentTime.Hour || x.StartsAt.Hour == currentTime.Hour + 1)
                ? (double?)x.Total : null
                ).Concat(dataTomorrow.Select(x => (double?)null)).ToList(); 
  
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
                        Order = 5
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
                        Order = 4
                    },
                    new LineChartDataset()
                    {
                        Label = "AvgLine",
                        Data = dataAvgPoints,
                        BackgroundColor = "rgb(0, 0, 0)",
                        BorderColor = "rgb(0, 0, 0)",
                        BorderWidth = 2,
                        PointRadius = new List<double>() { 0.0 },
                        Stepped = true,
                        Order = 3
                    },
                    new LineChartDataset()
                    {
                        Label = "Avg high price",
                        Data = dataHighPrices,
                        BackgroundColor = "rgb(0, 128, 255)",
                        BorderColor = "rgb(0, 128, 255)",
                        BorderWidth = 2,
                        PointRadius = new List<double>() { 0.0 },
                        Stepped = true,
                        Fill = true,
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
                        Data = dataSource.Select(x => (double?)x.AvgPowerProduction).Select(s => -s).ToList(),
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

        private async void RealTimeMeasurement_CollectionChanged(object? sender, EventArgs eventArgs)
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

        private Grid<GrowattProgram> gridPrograms = default!;

        private IEnumerable<GrowattProgram>? listPrograms;

        private HashSet<GrowattProgram>? GrowattSelectedPrograms;

        private async Task<GridDataProviderResult<GrowattProgram>> GrowattProgramDataProvider(GridDataProviderRequest<GrowattProgram> request)
        {
            Console.WriteLine("EmployeesDataProvider called...");

            if (listPrograms is null) // pull employees only one time for client-side filtering, sorting, and paging
                listPrograms = GrowattGetPrograms(); // call a service or an API to pull the employees

            return await Task.FromResult(request.ApplyTo(listPrograms));
        }

        private IEnumerable<GrowattProgram> GrowattGetPrograms()
        {
            return new List<GrowattProgram>() {
                new GrowattProgram() { Id = "1", Name = "Program 1", IsActive = true },
                new GrowattProgram() { Id = "2", Name = "Program 2", IsActive = false },
                new GrowattProgram() { Id = "3", Name = "Program 3", IsActive = false }
            };
        }

        public async Task GrowattSetProgramActive(GrowattProgram? growattProgram)
        {
            if (growattProgram != null)
            {
                await ApiService.GrowattSetProgramActive(growattProgram);
                GrowattSelectedPrograms = null;
                await gridPrograms.RefreshDataAsync();
            }
        }

        private int ApiSettingDataReadsDelaySec_DeviceNoahLastDataQuery
        {
            get
            {
                return ApiService.ApiSettingDataReadsDelaySec["DeviceNoahLastDataQuery"];
            }
            set
            {
                ApiService.ApiSettingDataReadsDelaySec["DeviceNoahLastDataQuery"] = value;
            }
        }        
        
        private int ApiSettingDataReadsDelaySec_DeviceNoahInfoQuery
        {
            get
            {
                return ApiService.ApiSettingDataReadsDelaySec["DeviceNoahInfoQuery"];
            }
            set
            {
                ApiService.ApiSettingDataReadsDelaySec["DeviceNoahInfoQuery"] = value;
            }
        }

        #endregion Growatt

    }
}
