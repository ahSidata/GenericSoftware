using EnergyAutomate.Definitions;
using Tibber.Sdk;

namespace EnergyAutomate
{
    public static class ApiServiceExtensions
    {
        #region Growatt

        public static void AddOrUpdate(this List<APiTraceValue> list, APiTraceValue value)
        {
            var existing = list.FirstOrDefault(x => x.Index == value.Index);
            if (existing != null)
            {
                existing.Key = value.Key;
                existing.Value = value.Value;
            }
            else
            {
                list.Add(value);
            }
        }

        #endregion Growatt

        #region Timbber

        /// <summary>Builds a query for home consumption.</summary>
        /// <param name="homeQueryBuilder"></param>
        /// <param name="resolution"></param>
        /// <param name="lastEntries">how many last entries to fetch</param>
        /// <returns></returns>
        public static HomeQueryBuilder WithConsumption(this HomeQueryBuilder homeQueryBuilder, EnergyResolution resolution, int lastEntries) =>
            homeQueryBuilder.WithConsumption(
                new HomeConsumptionConnectionQueryBuilder().WithNodes(new ConsumptionEntryQueryBuilder().WithAllFields()),
                resolution,
                last: lastEntries);

        /// <summary>Builds a query for home consumption.</summary>
        /// <param name="builder"></param>
        /// <param name="homeId"></param>
        /// <param name="resolution"></param>
        /// <param name="lastEntries">how many last entries to fetch</param>
        /// <returns></returns>
        public static TibberQueryBuilder WithHomeConsumption(this TibberQueryBuilder builder, Guid homeId, EnergyResolution resolution, int lastEntries) =>
            builder.WithAllScalarFields()
                .WithViewer(
                    new ViewerQueryBuilder()
                        .WithHome(
                            new HomeQueryBuilder().WithConsumption(resolution, lastEntries),
                            homeId
                        )
                );

        #endregion Timbber
    }
}
