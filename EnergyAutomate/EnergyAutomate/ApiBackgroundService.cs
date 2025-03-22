using EnergyAutomate.Components.Pages;
using Growatt.OSS;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using Tibber.Sdk;

namespace EnergyAutomate
{
    public class ApiBackgroundService : IHostedService, IDisposable
    {
        private ApiService ApiService { get; init; }

        private ApiRealTimeMeasurementWatchdog ApiRealTimeMeasurementWatchdog { get; init; }

        private IConfiguration Configuration { get; init; }

        public ApiBackgroundService(ApiService apiService, IConfiguration configuration, ApiRealTimeMeasurementWatchdog apiRealTimeMeasurementWatchdog)
        {
            ApiService = apiService;
            Configuration = configuration;
            ApiRealTimeMeasurementWatchdog = apiRealTimeMeasurementWatchdog;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await ApiRealTimeMeasurementWatchdog.StartAsync(CancellationTokenSource.CreateLinkedTokenSource(cancellationToken).Token);
            await ApiService.StartAsync(CancellationTokenSource.CreateLinkedTokenSource(cancellationToken).Token);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await ApiService.StopAsync(CancellationTokenSource.CreateLinkedTokenSource(cancellationToken).Token);
            await ApiRealTimeMeasurementWatchdog.StopAsync(CancellationTokenSource.CreateLinkedTokenSource(cancellationToken).Token);
        }

        public void Dispose()
        {
        }
    }
}
