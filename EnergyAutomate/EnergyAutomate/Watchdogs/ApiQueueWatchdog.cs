using Growatt.OSS;
using Growatt.Sdk;
using System.Collections.ObjectModel;
using System.Net.Http.Headers;
using Tibber.Sdk;

namespace EnergyAutomate.Watchdogs
{
    public class ApiQueueWatchdog<T> where T : class, IDevice
    {

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

        public T Dequeue()
        {
            if (Collection.Count == 0)
                throw new InvalidOperationException("Die Warteschlange ist leer.");

            var item = Collection[0];
            Collection.RemoveAt(0);
            return item;
        }

        public T Peek()
        {
            if (Collection.Count == 0)
                throw new InvalidOperationException("Die Warteschlange ist leer.");

            return Collection[0];
        }

        public int Count => Collection.Count;

        public ApiQueueWatchdog(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
            Collection.CollectionChanged += async (sender, e) => await ProceedingAsync();                    
        }

        private int penaltyFrequentlyAccess = 0;

        private int TotalDelay => ServiceProvider.GetRequiredService<ApiServiceInfo>().SettingLockSeconds + penaltyFrequentlyAccess;


        private IServiceProvider ServiceProvider { get; init; }

        private ILogger<ApiQueueWatchdog<T>> Logger => ServiceProvider.GetRequiredService<ILogger<ApiQueueWatchdog<T>>>();

        public ObservableCollection<T> Collection { get; set; } = new();

        //Action delegate T item
        public event Func<T, GrowattApiClient, Task<ApiException?>>? OnItemDequeued;

        private bool IsProceeding { get; set; }

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

                    if (penaltyFrequentlyAccess >= 100)
                        penaltyFrequentlyAccess -= 100;

                    await Task.Delay(TotalDelay);
                }
                catch (ApiException ex)
                {
                    if (ex.ErrorCode == 5) ServiceProvider.GetRequiredService<ApiService>().GetDeviceNoahInfo();

                    if (item != null && !item.Force)
                        item = null;

                    penaltyFrequentlyAccess += 100;

                    Logger.LogError(ex, ex.Message);
                }
            }

            IsProceeding = false;
        }
    }

}
