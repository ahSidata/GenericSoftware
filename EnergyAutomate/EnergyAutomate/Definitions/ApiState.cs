namespace EnergyAutomate.Definitions
{
    public class ApiState
    {
        #region Fields

        public bool IsRTMAutoModeRunning = false;
        public bool IsRTMRestrictionModeRunning = false;
        private readonly ApiService _apiService;

        #endregion Fields

        #region Public Constructors

        public ApiState(ApiService apiService)
        {
            _apiService = apiService;
        }

        #endregion Public Constructors

        #region Properties

        public bool IsGrowattBatteryEmpty { get; set; }
        public bool IsGrowattOnline => _apiService.GrowattListDevices().Where(x => x.DeviceType == "noah" && x.IsOfflineSince == null).Any();
        public DateTime Now => DateTime.UtcNow.AddHours(_apiService.ApiSettingTimeOffset);

        #endregion Properties
    }
}
