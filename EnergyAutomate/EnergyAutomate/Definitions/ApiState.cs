using CoordinateSharp;
using EnergyAutomate.Extentions;
using OpenMeteo;

namespace EnergyAutomate.Definitions
{
    public class ApiState
    {
        #region Fields

        public bool IsRTMAutoModeRunning = false;
        public bool IsRTMRestrictionModeRunning = false;
        private readonly ApiService _apiService;
        private IServiceProvider _serviceProvider;

        #endregion Fields

        #region Public Constructors

        public ApiState(IServiceProvider serviceProvider, ApiService apiService)
        {
            _serviceProvider = serviceProvider;
            _apiService = apiService;
            Logger = _serviceProvider.GetRequiredService<ILogger<ApiState>>();
        }

        #endregion Public Constructors

        #region Properties

        public string ActiveRTMAdjustment { get; set; } = string.Empty;
        public string ActiveRTMCondition { get; set; } = string.Empty;
        public string ActiveTPCondition { get; set; } = string.Empty;
        public int GrowattNoahTotalDefaultPower => _apiService.GrowattLatestNoahInfoDatas().Sum(x => (int)(x?.DefaultPower ?? 0));
        /// <summary>Returns the total available power from all Growatt devices.</summary>
        public int GrowattNoahTotalPPV => _apiService.GrowattLatestNoahLastDatas().Sum(x => (int)(x?.ppv ?? 0));

        public int PowerValueTotalCommited => _apiService.GrowattGetDevicesNoahOnline().Any() ? _apiService.GrowattGetDevicesNoahOnline().Sum(x => x.PowerValueCommited) : 0;

        public int PowerValueTotalRequested => _apiService.GrowattGetDevicesNoahOnline().Any() ? _apiService.GrowattGetDevicesNoahOnline().Sum(x => x.PowerValueRequested) : 0;

        public bool IsBelowAvgPrice
        {
            get
            {
                var avg = _apiService.TibberListPrices().Where(o => o.StartsAt.UtcDateTime.Date == UtcNow.Date).ToList().Average(x => x.Total);
                var currentStartAt = UtcNow.Date.AddHours(UtcNow.Hour - _apiService.ApiSettingTimeOffset);
                var total = _apiService.TibberListPrices().FirstOrDefault(x => x.StartsAt.UtcDateTime.Date == UtcNow.Date && x.StartsAt.UtcDateTime.Hour == UtcNow.Hour)?.Total;
                return total < avg;
            }
        }
        public bool IsCheapRestrictionMode
        {
            get
            {
                var level = _apiService.TibberListPrices().OrderBy(o => o.StartsAt).FirstOrDefault(x => x.StartsAt > UtcNow.AddHours(-1))?.Level;
                return level == PriceLevel.Cheap || level == PriceLevel.VeryCheap;
            }
        }
        public bool IsCloudy(WeatherForecast? weatherForecast = null)
        {
            weatherForecast ??= WeatherForecastToday;

            var values = weatherForecast?.Hourly?.Direct_radiation_instant;
            var times = weatherForecast?.Hourly?.Time;

            if (values != null && times != null)
            {
                var powerCount = 0;
                var totalCount = 0;
                for (int i = 0; i < times.Length; i++)
                {
                    var hour = DateTime.Parse(times[i]).Hour;
                    if (hour > 10 && hour < 16 && values[i].HasValue)
                    {
                        totalCount++;
                        powerCount += (int)values[i]!.Value;
                    }
                }

                var averagePower = powerCount / totalCount;

                // Check if the average power is less than 200 W/m²
                return averagePower < 200;
            }

            return false;
        }

        public bool IsExpensiveRestrictionMode
        {
            get
            {
                var level = _apiService.TibberListPrices().OrderBy(o => o.StartsAt).FirstOrDefault(x => x.StartsAt > UtcNow.AddHours(-1))?.Level;
                return level == PriceLevel.Expensive || level == PriceLevel.VeryExpensive;
            }
        }
        /// <summary>Battery is empty if all Growatt devices are empty.</summary>
        public bool IsGrowattBatteryEmpty => _apiService.GrowattAllNoahDevices().All(x => x.IsBatteryEmpty);
        /// <summary>Full battery if all Growatt devices are full.</summary>
        public bool IsGrowattBatteryFull => _apiService.GrowattAllNoahDevices().All(x => x.IsBatteryFull);
        /// <summary>
        /// Returns true if the total available power from all Growatt devices is greater than the
        /// maximum power setting.
        /// </summary>
        public bool IsGrowattNoahSufficientSurplusAvailable => GrowattNoahTotalPPV >= _apiService.ApiSettingMaxPower;
        /// <summary>Returns true if all Growatt devices are offline.</summary>
        public bool IsGrowattOnline => _apiService.GrowattGetDevicesNoahOnline().Any();
        public TimeSpan? SunRise => WeatherForecastToday?.Daily?.Sunrise?.Length > 0 ? DateTime.Parse(WeatherForecastToday.Daily.Sunrise[0]).TimeOfDay : null;
        public TimeSpan? SunSet => WeatherForecastToday?.Daily?.Sunset?.Length > 0 ? DateTime.Parse(WeatherForecastToday.Daily.Sunset[0]).TimeOfDay : null;
        /// <summary>Current UTC time with the API setting time offset applied.</summary>
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
        public WeatherForecast? WeatherForecastToday { get; set; }
        public WeatherForecast? WeatherForecastTomorrow { get; set; }
        private Coordinate Coordinate => _serviceProvider.GetRequiredService<Coordinate>();
        private ILogger<ApiState> Logger { get; set; }
        private OpenMeteo.OpenMeteoClient OpenMeteoClient { get; set; } = new OpenMeteo.OpenMeteoClient();

        public bool CheckTibberPricesCondition(string condition)
        {
            if (ActiveTPCondition != condition)
            {
                ActiveTPCondition = condition;
                _apiService.ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 51, Key = "ActiveTPCondition", Value = condition });
                Logger.LogTrace("CheckTPCondition {condition}", condition);
                return true;
            }

            return false;
        }

        public async Task<WeatherForecast?> GetWeatherForecastAsync(DateTime? dateTime = null)
        {
            // Set custom options
            WeatherForecastOptions options = new WeatherForecastOptions();
            options.Timezone = "auto";
            options.Temperature_Unit = TemperatureUnitType.celsius;
            options.Past_Days = 0;

            dateTime ??= DateTime.UtcNow;

            options.Start_date = dateTime.Value.ToString("yyyy-MM-dd");
            options.End_date = dateTime.Value.ToString("yyyy-MM-dd");

            options.Hourly.Add(HourlyOptionsParameter.direct_radiation_instant);
            options.Hourly.Add(HourlyOptionsParameter.cloudcover_low);
            options.Hourly.Add(HourlyOptionsParameter.cloudcover_mid);
            options.Hourly.Add(HourlyOptionsParameter.cloudcover_high);
            options.Daily.Add(DailyOptionsParameter.daylight_duration);
            options.Daily.Add(DailyOptionsParameter.sunshine_duration);
            options.Daily.Add(DailyOptionsParameter.sunrise);
            options.Daily.Add(DailyOptionsParameter.sunset);

            var latitude = Coordinate.Latitude.ToDouble();
            var longitude = Coordinate.Longitude.ToDouble();

            options.Latitude = (float)latitude;
            options.Longitude = (float)longitude;

            return await OpenMeteoClient.QueryAsync(options);
        }

        public int GrowattGetNoahCurrentIsDischarchingState()
        {
            return _apiService.GrowattGetDeviceLists()
                .Where(w => w.DeviceType == "noah")
                .Max(noah => _apiService.GrowattGetNoahLastDataPerDevice(noah.DeviceSn)?.totalBatteryPackChargingStatus ?? 0);
        }

        public double GrowattNoahGetAvgPpvLast5Minutes()
        {
            // Aktuelle Zeit
            var now = UtcNow;

            // Hole die letzten Daten
            var lastDatas = _apiService.GrowattGetNoahLastDatas();

            // Filtere die Daten der letzten 5 Minuten
            var groupedPpvSums = lastDatas
                .Where(data => data != null && (now - data.TS).TotalMinutes <= 5)
                .GroupBy(data => data.deviceSn)
                .Select(group => new
                {
                    DeviceSn = group.Key,
                    PpvSum = group.Average(data => data.ppv) // Berechne die Summe der ppv-Werte pro Gerät
                })
                .ToList();



            // Berechne den Durchschnitt
            return groupedPpvSums.Any() ? groupedPpvSums.Sum(x => x.PpvSum) : 0;
        }

        #endregion Properties
    }
}
