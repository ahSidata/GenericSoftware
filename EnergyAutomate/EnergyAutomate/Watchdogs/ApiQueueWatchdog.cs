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
        public event Func<T, GrowattApiClient, Task<ApiException?>>? OnItemDequeued;

        #endregion Events

        #region Properties

        private List<T> Collection { get; set; } = new();

        public int Count => Collection.Count;

        private bool IsProceeding { get; set; }

        private ILogger<ApiQueueWatchdog<T>> Logger => ServiceProvider.GetRequiredService<ILogger<ApiQueueWatchdog<T>>>();

        private IServiceProvider ServiceProvider { get; init; }

        private List<ApiCallLog> GrowattDataReads { get; set; } = [];

        public Dictionary<string, int> ApiSettingDataReadsDelaySec { get; set; } = new Dictionary<string, int>() {
            { nameof(DeviceMinInfoDataQuery), 60 * 5 } ,
            { nameof(DeviceMinLastDataQuery), 60 * 5 } ,
            { nameof(DeviceNoahInfoDataQuery), 60 * 5} ,
            { nameof(DeviceNoahLastDataQuery), 60 } ,
            { nameof(DeviceNoahSetPowerQuery), 5 } ,
            { nameof(DeviceNoahTimeSegmentQuery), 5 } ,
            { nameof(DeviceListQuery), 60 * 5 }
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

        public void Enqueue(Queue<T> item)
        {
            lock (_lock)
            {
                while (item.Count > 0)
                {
                    Collection.Add(item.Dequeue());
                }
            }

            ProceedingAsync();
        }

        private async void ProceedingAsync()
        {
            if(IsProceeding)
                return;

            IsProceeding = true;

            var count = 0;
            do
            {
                T? item = default;
                bool blocked = false;

                lock (_lock)
                {
                    if (Collection.Count > 10)
                    {
                        Collection.Where(x => !x.Force).ToList().ForEach(x => Collection.Remove(x));
                    }

                    if (Collection.Count == 0)
                        break;
                    item ??= Dequeue();

                    var queryType = item.GetType().Name;

                    var delay = ApiSettingDataReadsDelaySec[queryType];

                    blocked = GrowattDataReads.Where(x => x.MethodeName == queryType && x.TS > DateTimeOffset.UtcNow.AddSeconds(-delay)).Any();
                    if (!blocked)
                    {
                        var entry = new ApiCallLog()
                        {
                            MethodeName = queryType,
                            TS = DateTimeOffset.UtcNow,
                            RaisedError = false
                        };

                        //Add log entry
                        GrowattDataReads.Add(entry);
                    }
                }

                if (!blocked)
                {
                    item = await InvokeQueryAsync(item);
                }

                lock (_lock)
                {
                    if (item != null && item.Force)
                    {
                        Collection.Add(item);
                    }

                    count = Collection.Count;
                }
            } while (count > 0);

            IsProceeding = false;
        }

        private async Task<T?> InvokeQueryAsync(T? item)
        {
            try
            {
                if (OnItemDequeued != null && item != null)
                {
                    ApiException? result = await OnItemDequeued.Invoke(item, ServiceProvider.GetRequiredService<GrowattApiClient>());
                    if (result != default)
                    {
                        throw result;
                    }
                }

                item = null;
            }
            catch (ApiException ex)
            {
                Logger.LogError("ApiQueueWatchdog<{Type}> ErrorCode: {ErrorCode}", typeof(T).Name, ex.ErrorCode);
                if (item != null)
                {
                    Logger.LogError("ItemType: {itemType}", item.GetType().Name);
                    Logger.LogError(JsonConvert.SerializeObject(item).ToString());
                }

                if (item != null && !item.Force)
                    item = null;
            }

            return item;
        }


        #endregion Public Methods
    }
}
