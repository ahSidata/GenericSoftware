using EnergyAutomate.Emulator.Shelly;

namespace EnergyAutomate.Services
{
    public class ApiBackgroundService : IHostedService, IDisposable
    {
        #region Public Constructors

        public ApiBackgroundService(ApiService apiService, IConfiguration configuration, ApiRealTimeMeasurementWatchdog apiRealTimeMeasurementWatchdog, ILogger<ApiBackgroundService> logger)
        {
            ApiService = apiService;
            Configuration = configuration;
            ApiRealTimeMeasurementWatchdog = apiRealTimeMeasurementWatchdog;
            Logger = logger;
        }

        #endregion Public Constructors

        #region Properties

        private ApiRealTimeMeasurementWatchdog ApiRealTimeMeasurementWatchdog { get; init; }
        private ApiService ApiService { get; init; }
        private IConfiguration Configuration { get; init; }
        private ILogger<ApiBackgroundService> Logger { get; init; }

        private ShellyPro3EMDevice? ShellyPro3EMDevice { get; set; }

        #endregion Properties

        #region Public Methods

        public void Dispose()
        {
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            Logger.LogTrace("Starting ApiBackgroundService");

            //var proxy = new MqttProxy(
            //    proxyCertPath: "certs/server.crt",
            //    proxyKeyPath: "certs/server.key",
            //    brokerHost: "mqtt.growatt.com",
            //    brokerPort: 7006);

            //await proxy.StartAsync();

            //var device = new ShellyPro3EMDevice();
            //var udpServer = new ShellyPro3EMUdpServer(1010, device); // UDP-Port wie bei Shelly-CoAP

            // _ = Task.Run(udpServer.StartAsync);

            try
            {
                await ApiService.ApiStartAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Logger.LogInformation("ApiService startup was canceled");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "ApiService startup failed. Continuing without cached startup data.");
            }

            _ = Task.Run(() => StartRealTimeMeasurementWatchdogAsync(cancellationToken), CancellationToken.None);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            Logger.LogTrace("Stopping ApiBackgroundService");

            try
            {
                await ApiService.ApiStopAsync(cancellationToken);
                await ApiRealTimeMeasurementWatchdog.StopAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Logger.LogInformation("ApiBackgroundService stop was canceled");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "ApiBackgroundService stop failed");
            }
        }

        private async Task StartRealTimeMeasurementWatchdogAsync(CancellationToken cancellationToken)
        {
            try
            {
                Logger.LogTrace("Starting real-time measurement watchdog");
                await ApiRealTimeMeasurementWatchdog.StartAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Logger.LogInformation("Real-time measurement watchdog startup was canceled");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Real-time measurement watchdog startup failed");
            }
        }

        #endregion Public Methods
    }
}
