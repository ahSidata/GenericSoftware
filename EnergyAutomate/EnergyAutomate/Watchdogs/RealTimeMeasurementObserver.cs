namespace EnergyAutomate.Watchdogs
{
    public class RealTimeMeasurementObserver : IObserver<RealTimeMeasurement>, IDisposable
    {
        #region Public Constructors

        public RealTimeMeasurementObserver(ApiRealTimeMeasurementWatchdog apiRealTimeMeasurementWatchdog, ApiService apiService)
        {
            ApiService = apiService;
            Watchdog = apiRealTimeMeasurementWatchdog;
        }

        #endregion Public Constructors

        #region Properties

        private ApiService? ApiService { get; set; }

        private ApiRealTimeMeasurementWatchdog Watchdog { get; set; }

        #endregion Properties

        #region Public Methods

        public void Dispose()
        {
            ApiService = null;
        }

        public void OnCompleted()
        {
            _ = RestartListenerAsync();
        }

        public void OnError(Exception error)
        {
            _ = RestartListenerAsync(error);
        }

        public void OnNext(RealTimeMeasurement value)
        {
            ApiService?.OnNext(value);
        }

        #endregion Public Methods

        #region Private Methods

        private async Task RestartListenerAsync(Exception? error = null)
        {
            if (!Watchdog.RestartRequested)
            {
                Watchdog.RestartRequested = true;
                await Watchdog.RestartListener();
            }
        }

        #endregion Private Methods
    }
}
