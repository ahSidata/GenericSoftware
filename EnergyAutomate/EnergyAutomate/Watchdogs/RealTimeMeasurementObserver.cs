namespace EnergyAutomate.Watchdogs
{
    public class RealTimeMeasurementObserver : IObserver<RealTimeMeasurement>, IDisposable
    {
        #region Public Constructors

        public RealTimeMeasurementObserver(ApiService apiService)
        {
            ApiService = apiService;
        }

        #endregion Public Constructors

        #region Properties

        private ApiService? ApiService { get; set; }

        #endregion Properties

        #region Public Methods

        public void Dispose()
        {
            ApiService = null;
        }

        public void OnCompleted() => ApiService?.OnCompleted();

        public void OnError(Exception error) => ApiService?.OnError(error);

        public void OnNext(RealTimeMeasurement value) => ApiService?.OnNext(value);

        #endregion Public Methods
    }
}
