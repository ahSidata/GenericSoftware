global using EnergyAutomate.Components;
global using EnergyAutomate.Components.Account;
global using EnergyAutomate.Data;
global using EnergyAutomate.Services;
global using EnergyAutomate.Watchdogs;
using CoordinateSharp;
using EnergyAutomate.Definitions;
using Growatt.Sdk;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Net.Http.Headers;
using Tibber.Sdk;

namespace EnergyAutomate;

public class Program
{
    #region Public Methods

    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Logger-Konfiguration hinzufügen
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.AddDebug();

        // Konfiguration laden
        var configuration = builder.Configuration;
        var TraceEnabled = configuration.GetSection("Trace").GetValue<bool>("TraceEnabled");

#if !DEBUG
        if (TraceEnabled)
            Trace.Listeners.Add(new ConsoleTraceListener());
#endif
        if (TraceEnabled)
        {
            // Erstellen und registrieren des benutzerdefinierten Trace-Listeners

            builder.Services.AddSingleton(sp =>
            {
                var customTraceListener = new CustomTraceListener(sp);
                Trace.Listeners.Add(customTraceListener);
                return customTraceListener;
            });
        }

        // Add services to the container.
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents()
            .AddInteractiveWebAssemblyComponents()
            .AddAuthenticationStateSerialization();

        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddScoped<IdentityUserAccessor>();
        builder.Services.AddScoped<IdentityRedirectManager>();
        builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

        builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            })
            .AddIdentityCookies();

        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString));
        builder.Services.AddDatabaseDeveloperPageExceptionFilter();

        builder.Services.AddIdentityCore<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

        builder.Services.AddTransient(sp => new Coordinate(double.Parse(builder.Configuration["ApiSettings:Latitude"] ?? "0,0"), double.Parse(builder.Configuration["ApiSettings:Longitude"] ?? "0,0"), DateTime.Now));
        builder.Services.AddTransient(sp => new GrowattApiClient("https://openapi.growatt.com", builder.Configuration["ApiSettings:GrowattApiToken"] ?? string.Empty));
        builder.Services.AddTransient(sp => new TibberApiClient(builder.Configuration["ApiSettings:TibberApiToken"] ?? string.Empty, new ProductInfoHeaderValue("EnergyAutomate", "1.0")));
        builder.Services.AddSingleton<ApiService>();
        builder.Services.AddSingleton<ApiService>();
        builder.Services.AddSingleton<ApiRealTimeMeasurementWatchdog>();
        builder.Services.AddSingleton<ApiQueueWatchdog<IDeviceQuery>>();

        // Registrieren des Hintergrunddienstes
        builder.Services.AddHostedService<ApiBackgroundService>();
        builder.Services.AddBlazorBootstrap();

        var app = builder.Build();

        // Configure the HTTP request pipeline.

        app.UseWebAssemblyDebugging();
        app.UseMigrationsEndPoint();

        //migrate database
        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            dbContext.Database.Migrate();
            var existingElements = dbContext.GrowattElements.ToList();
            var newElements = GrowattElements.GrowattDefaultElements().Where(e => !existingElements.Any(existing => existing.Id == e.Id)).ToList();

            if (newElements.Any())
            {
                dbContext.GrowattElements.AddRange(newElements);
                dbContext.SaveChanges();
            }
        }

        app.UseExceptionHandler("/Error");
        // The default HSTS value is 30 days. You may want to change this for production scenarios,
        // see https://aka.ms/aspnetcore-hsts.
        app.UseHsts();

        app.UseHttpsRedirection();

        app.UseAntiforgery();

        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode()
            .AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(typeof(Client._Imports).Assembly);

        // Add additional endpoints required by the Identity /Account Razor components.
        app.MapAdditionalIdentityEndpoints();

        app.Run();
    }

    #endregion Public Methods
}
