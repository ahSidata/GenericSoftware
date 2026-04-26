using BlazorBootstrap;
using EnergyAutomate.Definitions;
using Microsoft.AspNetCore.Components;
using System.Diagnostics.CodeAnalysis;

namespace EnergyAutomate.Components.Pages
{
    public partial class Growatt
    {
        private readonly IEnumerable<TickMark> ApiPowerTickList = ApiService.GenerateTickTickMarks(0, 900, 50);

        #region Properties

        [Inject]
        [AllowNull]
        private ApplicationDbContext ApplicationDbContext { get; set; }

        [Inject]
        [AllowNull]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        [AllowNull]
        private ILogger<Growatt> Logger { get; set; }

        #endregion Properties
    }
}
