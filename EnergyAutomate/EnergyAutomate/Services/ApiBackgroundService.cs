using EnergyAutomate.Emulator;
using EnergyAutomate.Utilities;

namespace EnergyAutomate.Services
{
    public class ApiBackgroundService : IHostedService, IDisposable
    {
        #region Public Constructors

        public ApiBackgroundService(ApiService apiService, IConfiguration configuration, ApiRealTimeMeasurementWatchdog apiRealTimeMeasurementWatchdog)
        {
            ApiService = apiService;
            Configuration = configuration;
            ApiRealTimeMeasurementWatchdog = apiRealTimeMeasurementWatchdog;
        }

        #endregion Public Constructors

        #region Properties

        private ApiRealTimeMeasurementWatchdog ApiRealTimeMeasurementWatchdog { get; init; }
        private ApiService ApiService { get; init; }
        private IConfiguration Configuration { get; init; }

        private ShellyPro3EMDevice ShellyPro3EMDevice { get; set; }

        #endregion Properties

        #region Public Methods

        public void Dispose()
        {
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            //var proxy = new MqttProxy(
            //    proxyCertPath: "certs/server.crt",
            //    proxyKeyPath: "certs/server.key",
            //    brokerHost: "mqtt.growatt.com",
            //    brokerPort: 7006);

            //await proxy.StartAsync();

            //var device = new ShellyPro3EMDevice();
           //var udpServer = new ShellyPro3EMUdpServer(1010, device); // UDP-Port wie bei Shelly-CoAP

           // _ = Task.Run(udpServer.StartAsync);

            await ApiRealTimeMeasurementWatchdog.StartAsync(CancellationTokenSource.CreateLinkedTokenSource(cancellationToken).Token);
            await ApiService.ApiStartAsync(CancellationTokenSource.CreateLinkedTokenSource(cancellationToken).Token);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await ApiService.ApiStopAsync(CancellationTokenSource.CreateLinkedTokenSource(cancellationToken).Token);
            await ApiRealTimeMeasurementWatchdog.StopAsync(CancellationTokenSource.CreateLinkedTokenSource(cancellationToken).Token);
        }

        #endregion Public Methods
    }
}
