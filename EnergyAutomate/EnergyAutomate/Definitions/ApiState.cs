using CoordinateSharp;
using OpenMeteo;
using Tibber.Sdk;

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
        }

        #endregion Public Constructors

        #region Properties

        public string ActiveRTMCondition { get; set; } = string.Empty;

        /// <summary>Returns the total available power from all Growatt devices.</summary>
        public int GrowattNoahTotalPPV => _apiService.GrowattGetNoahLastDatas().Sum(x => (int)(x?.ppv ?? 0));

        public bool IsCheapRestrictionMode
        {
            get
            {
                var level = _apiService.TibberListPrices().OrderBy(o => o.StartsAt).FirstOrDefault(x => x.StartsAt > Now.AddHours(-1))?.Level;
                return level == PriceLevel.Cheap || level == PriceLevel.VeryCheap;
            }
        }

        public bool IsExpensiveRestrictionMode
        {
            get
            {
                var level = _apiService.TibberListPrices().OrderBy(o => o.StartsAt).FirstOrDefault(x => x.StartsAt > Now.AddHours(-1))?.Level;
                return level == PriceLevel.Expensive || level == PriceLevel.VeryExpensive;
            }
        }

        /// <summary>Battery is empty if all Growatt devices are empty.</summary>
        public bool IsGrowattBatteryEmpty => _apiService.GrowattNoahDevices().All(x => x.IsBatteryEmpty);

        /// <summary>Full battery if all Growatt devices are full.</summary>
        public bool IsGrowattBatteryFull => _apiService.GrowattNoahDevices().All(x => x.IsBatteryFull);

        /// <summary>
        /// Returns true if the total available power from all Growatt devices is greater than the
        /// maximum power setting.
        /// </summary>
        public bool IsGrowattNoahSufficientSurplusAvailable => GrowattNoahTotalPPV >= _apiService.ApiSettingMaxPower;

        /// <summary>Returns true if all Growatt devices are offline.</summary>
        public bool IsGrowattOnline => _apiService.GrowattNoahDevices().Where(x => x.IsOfflineSince == null).Any();

        /// <summary>Current UTC time with the API setting time offset applied.</summary>
        public DateTime Now => DateTime.UtcNow.AddHours(_apiService.ApiSettingTimeOffset);

        public TimeSpan? SunRise => Coordinate.CelestialInfo.SunRise != null ? new DateTimeOffset(Coordinate.CelestialInfo.SunRise.Value, new TimeSpan(0)).LocalDateTime.TimeOfDay : null;

        public TimeSpan? SunSet => Coordinate.CelestialInfo.SunSet != null ? new DateTimeOffset(Coordinate.CelestialInfo.SunSet.Value, new TimeSpan(0)).LocalDateTime.TimeOfDay : null;

        private Coordinate Coordinate => _serviceProvider.GetRequiredService<Coordinate>();

        private OpenMeteo.OpenMeteoClient OpenMeteoClient { get; set; } = new OpenMeteo.OpenMeteoClient();

        public bool CheckRTMCondition(string condition)
        {
            if (ActiveRTMCondition != condition)
            {
                ActiveRTMCondition = condition;
                return true;
            }

            return false;
        }

        public async Task<WeatherForecast?> GetWeatherForecastAsync()
        {
            // Set custom options
            WeatherForecastOptions options = new WeatherForecastOptions();
            options.Temperature_Unit = TemperatureUnitType.celsius;
            options.Past_Days = 0;

            options.Start_date = DateTime.Now.ToString("yyyy-MM-dd");
            options.End_date = DateTime.Now.ToString("yyyy-MM-dd");

            options.Hourly.Add(HourlyOptionsParameter.cloudcover);
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
