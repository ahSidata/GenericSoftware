using System.Diagnostics;

namespace EnergyAutomate.Watchdogs
{
    public class ApiRealTimeMeasurementWatchdog
    {
        #region Public Constructors

        public ApiRealTimeMeasurementWatchdog(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;

            Trace.WriteLine("Create new TibberApiClient for watchdog ...", "Tibber");
        }

        #endregion Public Constructors

        #region Properties

        public TibberApiClient? TibberApiClient { get; set; }
        private ApiService ApiService => ServiceProvider.GetRequiredService<ApiService>();
        private ILogger Logger => ServiceProvider.GetRequiredService<ILogger<ApiRealTimeMeasurementWatchdog>>();
        private CancellationTokenSource? RealTimeMeasurementCancellationTokenSource { get; set; }
        private IObservable<RealTimeMeasurement>? RealTimeMeasurementListener { get; set; }
        private IDisposable? RealTimeMeasurementObserver { get; set; }
        private IServiceProvider ServiceProvider { get; init; }


        public bool RestartRequested { get; set; }

        #endregion Properties

        #region Public Methods

        public async Task RestartListener()
        {
            await Task.Delay(5000);

            if (ApiService.TibberHomeId.HasValue && TibberApiClient != null)
            {
                try
                {
                    RealTimeMeasurementListener = null;
                    RealTimeMeasurementObserver?.Dispose();
                    TibberApiClient?.Dispose();
                    TibberApiClient = null;
                }
                catch (Exception)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    Trace.WriteLine("StopRealTimeMeasurementListener finished ...", "Tibber");
                }

                _ = StartListener();

                RestartRequested = false;
            }
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await StartListener(cancellationToken);
        }

        public async Task StartListener(CancellationToken cancellationToken = default)
        {
            TibberApiClient = ServiceProvider.GetRequiredService<TibberApiClient>();

            if (TibberApiClient != null)
            {
                if (!ApiService.TibberHomeId.HasValue)
                {
                    ApiService.TibberHomeId = await ServiceProvider.GetRequiredService<TibberApiClient>().GetHomeId(cancellationToken);
                }

                if (ApiService.TibberHomeId.HasValue && TibberApiClient != null)
                {
                    try
                    {
                        Trace.WriteLine("StartRealTimeMeasurementListener calling ...", "Tibber");

                        RealTimeMeasurementListener = await TibberApiClient.StartRealTimeMeasurementListener(ApiService.TibberHomeId.Value, null, cancellationToken);
                        RealTimeMeasurementObserver = RealTimeMeasurementListener.Subscribe(new RealTimeMeasurementObserver(this, ApiService));
                    }
                    catch (OperationCanceledException ex) when (ex.CancellationToken == cancellationToken)
                    {
                        Console.WriteLine("Operation cancled.");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.Message);
                        await Task.Delay(5000);
                        _ = StartListener();
                    }
                }
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await StopListenerAsync(cancellationToken);
        }

        public async Task StopListenerAsync(CancellationToken cancellationToken = default)
        {
            if (RealTimeMeasurementCancellationTokenSource != null && !RealTimeMeasurementCancellationTokenSource.IsCancellationRequested)
                await RealTimeMeasurementCancellationTokenSource.CancelAsync();

            if (ApiService.TibberHomeId.HasValue && TibberApiClient != null)
                await TibberApiClient.StopRealTimeMeasurementListener(ApiService.TibberHomeId.Value, cancellationToken);

            if (RealTimeMeasurementObserver != null)
            {
                RealTimeMeasurementObserver.Dispose();
                RealTimeMeasurementObserver = null;
            }
        }

        #endregion Public Methods
    }
}
