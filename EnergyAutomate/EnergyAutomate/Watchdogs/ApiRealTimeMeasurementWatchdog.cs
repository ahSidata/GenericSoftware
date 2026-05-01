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
#pragma warning disable CS8625
                    RealTimeMeasurementListener = null;
#pragma warning restore CS8625
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
            var configuration = ServiceProvider.GetService<IConfiguration>();
            var token = configuration?["ApiSettings:TibberApiToken"];
            if (string.IsNullOrWhiteSpace(token) || token.Contains("your-api-token", StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogWarning("Tibber API token is missing or placeholder. Real-time listener will not start.");
                return;
            }

            TibberApiClient = ServiceProvider.GetRequiredService<TibberApiClient>();

            if (TibberApiClient != null)
            {
                if (!ApiService.TibberHomeId.HasValue)
                {
                    Logger.LogTrace("Loading Tibber basic data");
                    var basicData = await TibberApiClient.GetBasicData(cancellationToken);
                    var homeId = basicData.Data?.Viewer?.Homes?.FirstOrDefault()?.Id;

                    if (!homeId.HasValue)
                    {
                        Logger.LogWarning("Tibber returned no home id. Real-time listener will not start.");
                        return;
                    }

                    ApiService.TibberHomeId = homeId.Value;
                }

                if (ApiService.TibberHomeId.HasValue && TibberApiClient != null)
                {
                    try
                    {
                        Logger.LogTrace("StartRealTimeMeasurementListener calling");

#pragma warning disable CS8625
                        RealTimeMeasurementListener = await TibberApiClient.StartRealTimeMeasurementListener(ApiService.TibberHomeId.Value, null, cancellationToken);
#pragma warning restore CS8625
                        RealTimeMeasurementObserver = RealTimeMeasurementListener.Subscribe(new RealTimeMeasurementObserver(this, ApiService));
                        Logger.LogInformation("Tibber real-time measurement listener started for home {HomeId}", ApiService.TibberHomeId.Value);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        Logger.LogInformation("Tibber real-time listener startup was canceled");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Tibber real-time listener startup failed");
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
                await TibberApiClient.StopRealTimeMeasurementListener(ApiService.TibberHomeId.Value);

            if (RealTimeMeasurementObserver != null)
            {
#pragma warning disable CS8625
                RealTimeMeasurementObserver = null;
#pragma warning restore CS8625
            }
        }

        #endregion Public Methods
    }
}
