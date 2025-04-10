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

        private ILogger<ApiState> Logger { get; set; }

        public string ActiveRTMAdjustment { get; set; } = string.Empty;
        public string ActiveRTMCondition { get; set; } = string.Empty;
        public string ActiveTPCondition { get; set; } = string.Empty;

        public bool IsCloudy
        {
            get
            {
                var values = WeatherForecast?.Hourly?.Direct_radiation_instant;
                var times = WeatherForecast?.Hourly?.Time;

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
        }

        /// <summary>Returns the total available power from all Growatt devices.</summary>
        public int GrowattNoahTotalPPV => _apiService.GrowattLatestNoahLastDatas().Sum(x => (int)(x?.ppv ?? 0));

        public double GrowattNoahGetAvgPpvLast5Minutes()
        {
            // Aktuelle Zeit
            var now = UtcNow;

            // Hole die letzten Daten
            var lastDatas = _apiService.GrowattLatestNoahLastDatas();

            // Filtere die Daten der letzten 5 Minuten
            var ppvValues = lastDatas
                .Where(data => data != null && (now - data.TS).TotalMinutes <= 5)
                .Select(data => data.ppv);

            // Berechne den Durchschnitt
            return ppvValues.Any() ? ppvValues.Average() : 0;
        }


        public int GrowattNoahTotalDefaultPower => _apiService.GrowattLatestNoahInfoDatas().Sum(x => (int)(x?.DefaultPower ?? 0));

        public bool IsCheapRestrictionMode
        {
            get
            {
                var level = _apiService.TibberListPrices().OrderBy(o => o.StartsAt).FirstOrDefault(x => x.StartsAt > UtcNow.AddHours(-1))?.Level;
                return level == PriceLevel.Cheap || level == PriceLevel.VeryCheap;
            }
        }

        public bool IsExpensiveRestrictionMode
        {
            get
            {
                var level = _apiService.TibberListPrices().OrderBy(o => o.StartsAt).FirstOrDefault(x => x.StartsAt > UtcNow.AddHours(-1))?.Level;
                return level == PriceLevel.Expensive || level == PriceLevel.VeryExpensive;
            }
        }

        public bool IsBelowAvgPrice
        {
            get
            {
                var avg = _apiService.TibberListPrices().Where(o => o.StartsAt.Date == UtcNow.Date).ToList().Average(x => x.Total);
                var currentStartAt = UtcNow.Date.AddHours(UtcNow.Hour - _apiService.ApiSettingTimeOffset);
                var total = _apiService.TibberListPrices().FirstOrDefault(x => x.StartsAt == currentStartAt)?.Total;
                return total < avg;
            }
        }

        /// <summary>Battery is empty if all Growatt devices are empty.</summary>
        public bool IsGrowattBatteryEmpty => _apiService.GrowattAllNoahDevices().All(x => x.IsBatteryEmpty);

        /// <summary>Full battery if all Growatt devices are full.</summary>
        public bool IsGrowattBatteryFull => _apiService.GrowattAllNoahDevices().All(x => x.IsBatteryFull);

        public int GrowattGetNoahCurrentIsDischarchingState()
        {
            return _apiService.GrowattGetDeviceLists()
                .Where(w => w.DeviceType == "noah")
                .Max(noah => _apiService.GrowattGetNoahLastDataPerDevice(noah.DeviceSn)?.totalBatteryPackChargingStatus ?? 0);
        }

        /// <summary>
        /// Returns true if the total available power from all Growatt devices is greater than the
        /// maximum power setting.
        /// </summary>
        public bool IsGrowattNoahSufficientSurplusAvailable => GrowattNoahTotalPPV >= _apiService.ApiSettingMaxPower;

        /// <summary>Returns true if all Growatt devices are offline.</summary>
        public bool IsGrowattOnline => _apiService.GrowattGetDevicesNoahOnline().Any();

        /// <summary>Current UTC time with the API setting time offset applied.</summary>
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

        public TimeSpan? SunRise => Coordinate.CelestialInfo.SunRise != null ? new DateTimeOffset(Coordinate.CelestialInfo.SunRise.Value, new TimeSpan(0)).LocalDateTime.TimeOfDay : null;

        public TimeSpan? SunSet => Coordinate.CelestialInfo.SunSet != null ? new DateTimeOffset(Coordinate.CelestialInfo.SunSet.Value, new TimeSpan(0)).LocalDateTime.TimeOfDay : null;

        private Coordinate Coordinate => _serviceProvider.GetRequiredService<Coordinate>();

        private OpenMeteo.OpenMeteoClient OpenMeteoClient { get; set; } = new OpenMeteo.OpenMeteoClient();

        public bool CheckRTMCondition(string condition)
        {
            if (ActiveRTMCondition != condition)
            {
                ActiveRTMCondition = condition;
                _apiService.ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 51, Key = "ActiveRTMCondition", Value = condition });
                Logger.LogTrace("CheckRTMCondition {condition}", condition);
                return true;
            }

            return false;
        }

        public bool CheckRTMAdjustment(string condition)
        {
            if (ActiveRTMAdjustment != condition)
            {
                ActiveRTMAdjustment = condition;
                _apiService.ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 51, Key = "ActiveRTMAdjustment", Value = condition });
                Logger.LogTrace("CheckRTMAdjustment {condition}", condition);
                return true;
            }

            return false;
        }

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


        public WeatherForecast? WeatherForecast { get; set; }

        public async Task<WeatherForecast?> GetWeatherForecastAsync()
        {
            // Set custom options
            WeatherForecastOptions options = new WeatherForecastOptions();
            options.Temperature_Unit = TemperatureUnitType.celsius;
            options.Past_Days = 0;

            options.Start_date = DateTime.Now.ToString("yyyy-MM-dd");
            options.End_date = DateTime.Now.ToString("yyyy-MM-dd");

            options.Hourly.Add(HourlyOptionsParameter.direct_radiation_instant);
            options.Hourly.Add(HourlyOptionsParameter.cloudcover_low);
            options.Hourly.Add(HourlyOptionsParameter.cloudcover_mid);
            options.Hourly.Add(HourlyOptionsParameter.cloudcover_high);
            options.Daily.Add(DailyOptionsParameter.daylight_duration);
            options.Daily.Add(DailyOptionsParameter.sunshine_duration);

            var latitude = Coordinate.Latitude.ToDouble();
            var longitude = Coordinate.Longitude.ToDouble();

            options.Latitude = (float)latitude;
            options.Longitude = (float)longitude;

            return await OpenMeteoClient.QueryAsync(options);
        }

        #endregion Properties
    }
}
