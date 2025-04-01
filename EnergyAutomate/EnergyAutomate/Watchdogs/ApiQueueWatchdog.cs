using Growatt.OSS;
using Growatt.Sdk;
using Newtonsoft.Json;
using System.Collections.ObjectModel;

namespace EnergyAutomate.Watchdogs
{
    public class ApiQueueWatchdog<T> where T : class, IDeviceQuery
    {
        #region Public Constructors

        public ApiQueueWatchdog(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
            Collection.CollectionChanged += async (sender, e) => await ProceedingAsync();
        }

        #endregion Public Constructors

        #region Events

        //Action delegate T item
        public event Func<T, GrowattApiClient, Task<ApiException?>>? OnItemDequeued;

        #endregion Events

        #region Properties

        public int PenaltyFrequentlyAccess { get; set; } = 0;

        public ObservableCollection<T> Collection { get; set; } = new();

        public int Count => Collection.Count;

        private bool IsProceeding { get; set; }

        private ILogger<ApiQueueWatchdog<T>> Logger => ServiceProvider.GetRequiredService<ILogger<ApiQueueWatchdog<T>>>();

        private IServiceProvider ServiceProvider { get; init; }

        #endregion Properties

        #region Public Methods

        public void Clear()
        {
            Collection.Clear();
        }

        public T Dequeue()
        {
            if (Collection.Count == 0)
                throw new InvalidOperationException("Die Warteschlange ist leer.");

            var item = Collection[0];
            Collection.RemoveAt(0);
            return item;
        }

        public void Enqueue(T item)
        {
            Collection.Add(item);
        }

        public void Enqueue(Queue<T> item)
        {
            while (item.Count > 0)
            {
                Collection.Add(item.Dequeue());
            }
        }

        public T Peek()
        {
            if (Collection.Count == 0)
                throw new InvalidOperationException("Die Warteschlange ist leer.");

            return Collection[0];
        }

        private async Task ProceedingAsync()
        {
            if (IsProceeding)
                return;

            IsProceeding = true;

            T? item = default;

            while (Collection.Count > 0)
            {
                try
                {
                    if (Collection.Count > 10)
                    {
                        for (int i = 0; i < 8; i++)
                        {
                            Collection.RemoveAt(0);
                        }
                    }

                    item ??= Dequeue();
                    if (OnItemDequeued != null)
                    {
                        ApiException? result = await OnItemDequeued.Invoke(item, ServiceProvider.GetRequiredService<GrowattApiClient>());
                        if (result != default)
                        {
                            throw result;
                        }
                    }

                    item = null;

                    if (PenaltyFrequentlyAccess >= 100)
                        PenaltyFrequentlyAccess -= 100;

                    await Task.Delay(PenaltyFrequentlyAccess);
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

                    PenaltyFrequentlyAccess += 100;

                    await Task.Delay(PenaltyFrequentlyAccess);
                }
            }

            IsProceeding = false;
        }

        #endregion Public Methods
    }
}
