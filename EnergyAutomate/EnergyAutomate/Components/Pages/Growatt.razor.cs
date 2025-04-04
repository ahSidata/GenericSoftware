using BlazorBootstrap;
using EnergyAutomate.Components.Layout;
using EnergyAutomate.Definitions;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace EnergyAutomate.Components.Pages
{
    public partial class Growatt
    {
        [Inject]
        [AllowNull]
        ApplicationDbContext ApplicationDbContext { get; set; }

        [Inject]
        [AllowNull]
        NavigationManager NavigationManager { get; set; }

        #region Growatt

        private Grid<GrowattElement> gridElements = default!;
        private HashSet<GrowattElement>? GrowattSelectedElements;
        private Tabs tabsGrowattRef = default!;


        public async Task GrowattSetElementActive(GrowattElement? growattElement)
        {
            if (growattElement != null)
            {
                await ApiService.GrowattSetElementActive(growattElement);
                GrowattSelectedElements = null;
                await gridElements.RefreshDataAsync();
                NavigationManager.NavigateTo("growatt", forceLoad: true); // Force page reload
            }
        }

        private async Task<GridDataProviderResult<GrowattElement>> GrowattElementDataProvider(GridDataProviderRequest<GrowattElement> request)
        {
            Console.WriteLine("GrowattElementDataProvider called...");

            var listElements = await ApplicationDbContext.GrowattElements
                .OrderBy(x => x.ElementType)
                .ThenBy(x => x.Name)
                .ToListAsync(); // call a service or an API to pull the employees

            return await Task.FromResult(request.ApplyTo(listElements.AsEnumerable()));
        }

        #endregion Growatt
    }
}
