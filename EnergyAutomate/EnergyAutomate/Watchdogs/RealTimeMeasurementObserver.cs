using Tibber.Sdk;

namespace EnergyAutomate.Watchdogs
{
    public class RealTimeMeasurementObserver : IObserver<RealTimeMeasurement>, IDisposable
    {
        ApiService? ApiService { get; set; }

        public RealTimeMeasurementObserver(ApiService apiService)
        {
            ApiService = apiService;
        }

        public void OnCompleted() => ApiService?.OnCompleted();
        public void OnError(Exception error) => ApiService?.OnError(error);
        public void OnNext(RealTimeMeasurement value) => ApiService?.OnNext(value);

        public void Dispose()
        {
            ApiService = null;
        }
    }
}
