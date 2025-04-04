using BlazorBootstrap;
using EnergyAutomate.Components.Layout;
using EnergyAutomate.Definitions;
using Microsoft.AspNetCore.Components;
using OpenMeteo;
using System.Collections.Specialized;
using System.Diagnostics;

namespace EnergyAutomate.Components.Pages
{
    public partial class Weather
    {
        public WeatherForecast? WeatherForecast { get; set; }

        protected async override Task OnInitializedAsync()
        {
            WeatherForecast =  await ApiService.CurrentState.GetWeatherForecastAsync();

            await base.OnInitializedAsync();
        }

    }
}
