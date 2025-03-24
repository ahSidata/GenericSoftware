using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace EnergyAutomate.Client;

internal class Program
{
    #region Private Methods

    private static async Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);

        builder.Services.AddAuthorizationCore();
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddAuthenticationStateDeserialization();

        builder.Services.AddBlazorBootstrap();

        await builder.Build().RunAsync();
    }

    #endregion Private Methods
}
