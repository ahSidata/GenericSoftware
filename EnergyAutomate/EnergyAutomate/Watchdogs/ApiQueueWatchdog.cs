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
            { nameof(DeviceMinInfoDataQuery), 60 * 5 } ,
            { nameof(DeviceMinLastDataQuery), 60 * 5 } ,
            { nameof(DeviceNoahInfoDataQuery), 60 * 5 } ,
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

        public T? Dequeue()
        {
            lock (_lock)
            {
                if (Collection.Count != 0)
                {
                    var item = Collection[0];
                    Collection.RemoveAt(0);
                    return item;
                }
                else
                    return default;
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
            lock (_lock)
            {
                if (IsProceeding)
                    return;

                IsProceeding = true;
            }

            do
            {
                if (Collection.Count == 0)
                    break;

                var item = Dequeue();

                if(item == null)
                    break;

                var queryType = item.GetType().Name;
                var delay = ApiSettingDataReadsDelaySec[queryType];

                var isNotLocked = !GrowattDataReads.Where(x => x.MethodeName == queryType && x.TS.UtcDateTime >= DateTimeOffset.UtcNow.AddSeconds(-delay)).Any();

                if (item != null && isNotLocked)
                {
                    var ts = DateTimeOffset.UtcNow;
                    Logger.LogTrace("ApiQueueWatchdog<{Type}>: {queryType} - {delay} sec >> {TS}", typeof(T).Name, queryType, delay, ts);
                    var entry = new ApiCallLog()
                    {
                        MethodeName = queryType,
                        TS = ts,
                        RaisedError = false
                    };

                    //Add log entry
                    GrowattDataReads.Add(entry);

                    // Starten des Tasks ohne await und Verarbeitung des Ergebnisses im Hintergrund
                    _ = OnItemDequeued?.Invoke(item, ServiceProvider.GetRequiredService<GrowattApiClient>(), Logger)
                        .ContinueWith(task =>
                        {
                            if (task.IsFaulted)
                            {
                                Logger.LogError("ApiQueueWatchdog<{Type}>: Fehler bei der Verarbeitung von {ItemType}",
                                    typeof(T).Name, item.GetType().Name);
                                Logger.LogError("Exception: {Exception}", task.Exception?.InnerException);

                                if (item.Force)
                                {
                                    lock (_lock)
                                    {
                                        Collection.Add(item);
                                    }
                                }
                            }
                            else if (task.Result != default)
                            {
                                Logger.LogError("ApiQueueWatchdog<{Type}> ErrorCode: {ErrorCode}",
                                    typeof(T).Name, task.Result.ErrorCode);
                                Logger.LogError("Error ItemType: {itemType}", item.GetType().Name);
                                Logger.LogError(JsonConvert.SerializeObject(item).ToString());

                                if (item.Force)
                                {
                                    lock (_lock)
                                    {
                                        Collection.Add(item);
                                    }
                                }
                            }
                        }, TaskScheduler.Current);
                }
                else if (item != null && item.Force)
                {
                    Collection.Add(item);
                }

            } while (Collection.Count > 0);

            lock (_lock)
            {
                IsProceeding = false;
            }
        }

        #endregion Public Methods
    }
}
