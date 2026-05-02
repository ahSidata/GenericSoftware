using BlazorBootstrap;
using EnergyAutomate.Definitions;
using EnergyAutomate.Emulator;
using Microsoft.AspNetCore.Components;
using System.Diagnostics.CodeAnalysis;

namespace EnergyAutomate.Components.Pages
{
    public partial class Growatt
    {
        private readonly IEnumerable<TickMark> ApiPowerTickList = ApiService.GenerateTickTickMarks(0, 900, 50);

        #region Properties

        [Inject]
        public required ApplicationDbContext ApplicationDbContext { get; set; }

        [Inject]
        public required NavigationManager NavigationManager { get; set; }

        [Inject]
        public required ILogger<Growatt> Logger { get; set; }

        [Inject]
        public required PythonWrapper PythonWrapper { get; set; }

        private ushort SmartPowerValue { get; set; } = 500;
        private ushort DefaultPowerValue { get; set; } = 250;

        #endregion Properties

        private void SetSmartPowerAsync()
        {
            PythonWrapper.SetSmartPower(SmartPowerValue);
        }

        private void SetDefaultPowerAsync()
        {
            PythonWrapper.SetDefaultPower(DefaultPowerValue);
        }
    }
}
