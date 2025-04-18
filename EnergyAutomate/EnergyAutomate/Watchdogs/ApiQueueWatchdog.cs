using EnergyAutomate.Definitions;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using static EnergyAutomate.Growatt.DeviceNoahHistoricalDataQuery;

namespace EnergyAutomate.Watchdogs
{
    public class ApiQueueWatchdog<T> where T : class, IDeviceQuery
    {
        private readonly Lock _lock = new();

        #region Public Constructors

        public ApiQueueWatchdog(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }

        #endregion Public Constructors

        #region Events

        //Action delegate T item
        public event Func<T, GrowattApiClient, ILogger, Task<ApiException?>>? OnItemDequeued;

        #endregion Events

        #region Properties

        private List<T> Collection { get; set; } = new();

        public int Count => Collection.Count;

        private bool IsProceeding { get; set; }

        private ILogger<ApiQueueWatchdog<T>> Logger => ServiceProvider.GetRequiredService<ILogger<ApiQueueWatchdog<T>>>();

        private IServiceProvider ServiceProvider { get; init; }

        private List<ApiCallLog> GrowattDataReads { get; set; } = [];

        public Dictionary<string, int> ApiSettingDataReadsDelaySec { get; set; } = new Dictionary<string, int>() {
            { nameof(DeviceMinInfoDataQuery), 60 * 5 + 1 } ,
            { nameof(DeviceMinLastDataQuery), 60 * 5 + 1 } ,
            { nameof(DeviceNoahInfoDataQuery), 60 * 5 + 1 } ,
            { nameof(DeviceNoahLastDataQuery), 61 } ,
            { nameof(DeviceNoahSetLowLimitSocQuery), 5 } ,
            { nameof(DeviceNoahSetPowerQuery), 5 } ,
            { nameof(DeviceNoahSetTimeSegmentQuery), 5 } ,
            { nameof(DeviceListQuery), 60 * 5 + 1 }
        };

        #endregion Properties

        #region Public Methods

        public void Clear()
        {
            lock (_lock)
            {
                Collection.Clear();
            }
        }

        public T Dequeue()
        {
            lock (_lock)
            {
                if (Collection.Count == 0)
                    throw new InvalidOperationException("Die Warteschlange ist leer.");

                var item = Collection[0];
                Collection.RemoveAt(0);
                return item;
            }
        }

        public void Enqueue(T item)
        {
            lock (_lock)
            {
                Collection.Add(item);
            }

            ProceedingAsync();
        }

        public bool CheckLimits(T item, bool writelog = false)
        {
            var queryType = item.GetType().Name;
            var delay = ApiSettingDataReadsDelaySec[queryType];

            var result = GrowattDataReads.Where(x => x.MethodeName == queryType && x.TS > DateTimeOffset.UtcNow.AddSeconds(-delay)).Any();

            if (result)
                return false;

            if (writelog)
            {
                Logger.LogTrace("ApiQueueWatchdog<{Type}>: {queryType} - {delay} sec", typeof(T).Name, queryType, delay);
                var entry = new ApiCallLog()
                {
                    MethodeName = queryType,
                    TS = DateTimeOffset.UtcNow,
                    RaisedError = false
                };

                //Add log entry
                GrowattDataReads.Add(entry);
            }

            return true;
        }

        public void Enqueue(Queue<T> items)
        {
            lock (_lock)
            {
                while (items.Count > 0)
                {
                    var item = items.Dequeue();
                    Collection.Add(item);
                }
            }

            ProceedingAsync();
        }

        private async void ProceedingAsync()
        {
            if (IsProceeding)
                return;

            IsProceeding = true;

            T? item = default;
            var count = 0;
            
            do
            {
                if (Collection.Count == 0 && item == null)
                    break;

                item ??= Dequeue();

                if (CheckLimits(item, true))
                {
                    try
                    {
                        if (OnItemDequeued != null && item != null)
                        {
                            ApiException? result = await OnItemDequeued.Invoke(item, ServiceProvider.GetRequiredService<GrowattApiClient>(), Logger);
                            if (result != default)
                            {
                                throw result;
                            }
                            item = null;
                        }
                    }
                    catch (ApiException ex)
                    {
                        Logger.LogError("ApiQueueWatchdog<{Type}> ErrorCode: {ErrorCode}", typeof(T).Name, ex.ErrorCode);
                        if (item != null)
                        {
                            Logger.LogError("ItemType: {itemType}", item.GetType().Name);
                            Logger.LogError(JsonConvert.SerializeObject(item).ToString());

                            if (item.Force)
                            {
                                Collection.Add(item);
                            }
                        }
                    }
                }
                else if(!item.Force)
                {
                    item = null;
                }                

                count = Collection.Count;

            } while (count > 0);

            IsProceeding = false;
        }

        #endregion Public Methods
    }
}
