using OpenMeteo;

namespace EnergyAutomate.Components.Pages
{
    public partial class Weather
    {
        #region Properties

        public WeatherForecast? WeatherForecast { get; set; }

        #endregion Properties

        #region Protected Methods

        protected override async Task OnInitializedAsync()
        {
            WeatherForecast = await ApiService.CurrentState.GetWeatherForecastAsync();

            await base.OnInitializedAsync();
        }

        #endregion Protected Methods
    }
}
