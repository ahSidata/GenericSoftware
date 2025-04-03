namespace EnergyAutomate.Definitions
{
    public class ApiState
    {
        private readonly ApiService _apiService;

        public ApiState(ApiService apiService)
        {
            _apiService = apiService;
        }

        public DateTime Now => DateTime.UtcNow.AddHours(_apiService.ApiSettingTimeOffset);

        public bool IsGrowattOnline => _apiService.GrowattListDevices().Where(x => x.DeviceType == "noah" && x.IsOfflineSince == null).Any();

        public bool IsGrowattBatteryEmpty { get; set; }

        public bool IsRTMAutoModeRunning = false;

        public bool IsRTMRestrictionModeRunning = false;
 
    }
}
