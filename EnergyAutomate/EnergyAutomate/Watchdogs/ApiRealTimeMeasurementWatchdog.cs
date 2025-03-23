using Growatt.OSS;
using System.Diagnostics;
using System.Net.Http.Headers;
using Tibber.Sdk;

namespace EnergyAutomate.Watchdogs
{
    public class ApiRealTimeMeasurementWatchdog
    {
        private IServiceProvider ServiceProvider { get; init; }

        private ApiService ApiService => ServiceProvider.GetRequiredService<ApiService>();

        public TibberApiClient? TibberApiClient { get; set; }

        private Guid? TibberHomeId { get; set; }

        private CancellationTokenSource? RealTimeMeasurementCancellationTokenSource { get; set; }

        private IObservable<RealTimeMeasurement>? RealTimeMeasurementListener { get; set; }

        public ApiRealTimeMeasurementWatchdog(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;

            Trace.WriteLine("Create new TibberApiClient for watchdog ...");            
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            TibberApiClient = GetNewTibberClient();

            if (TibberApiClient != null)
            {
                var basicData = await TibberApiClient.GetBasicData(cancellationToken);
                TibberHomeId = basicData.Data.Viewer.Homes.FirstOrDefault()?.Id;

                await StartListener(cancellationToken);
            }
        }

        private TibberApiClient GetNewTibberClient()
        {
            var configuration = ServiceProvider.GetRequiredService<IConfiguration>();
            return new TibberApiClient(configuration["ApiSettings:TibberApiToken"] ?? string.Empty, new ProductInfoHeaderValue("EnergyAutomate", "1.0"));
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await StopListenerAsync(cancellationToken);
        }

        public async Task StopListenerAsync(CancellationToken cancellationToken = default)
        {
            if (RealTimeMeasurementCancellationTokenSource != null && !RealTimeMeasurementCancellationTokenSource.IsCancellationRequested)
                await RealTimeMeasurementCancellationTokenSource.CancelAsync();

            if (TibberHomeId.HasValue && TibberApiClient != null)
                await TibberApiClient.StopRealTimeMeasurementListener(TibberHomeId.Value);
        }

        public async Task StartListener(CancellationToken cancellationToken = default)
        {
            if (TibberHomeId.HasValue && TibberApiClient != null)
            {
                try
                {
                    Trace.WriteLine("StartRealTimeMeasurementListener calling ...");

                    RealTimeMeasurementListener = await TibberApiClient.StartRealTimeMeasurementListener(TibberHomeId.Value, cancellationToken);
                    _ = RealTimeMeasurementListener.Subscribe(ApiService);
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

        public async Task RestartListener(CancellationToken cancellationToken = default)
        {
            if (TibberHomeId.HasValue && TibberApiClient != null)
            {
                if (RealTimeMeasurementListener != null)
                {
                    Trace.WriteLine("StopRealTimeMeasurementListener calling ...");

                    await TibberApiClient.StopRealTimeMeasurementListener(TibberHomeId.Value);
                    TibberApiClient.Dispose();
                    TibberApiClient = null;
                    RealTimeMeasurementListener = null;

                    GC.Collect();
                    GC.WaitForPendingFinalizers();

                    Trace.WriteLine("StopRealTimeMeasurementListener finished ...");
                }

                try
                {
                    Trace.WriteLine("Create new TibberApiClient ...");

                    TibberApiClient = GetNewTibberClient();

                    Trace.WriteLine("StartRealTimeMeasurementListener calling ...");

                    RealTimeMeasurementListener = await TibberApiClient.StartRealTimeMeasurementListener(TibberHomeId.Value, cancellationToken);
                    _ = RealTimeMeasurementListener.Subscribe(ApiService);
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
}
