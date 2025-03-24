using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

public class ThreadSafeObservableCollection<T> : ObservableCollection<T>
{
    private readonly object _syncRoot = new object();

    public ThreadSafeObservableCollection() : base() { }

    public ThreadSafeObservableCollection(IEnumerable<T> collection) : base(collection) { }

    public ThreadSafeObservableCollection(List<T> list) : base(list) { }

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        lock (_syncRoot)
        {
            base.OnCollectionChanged(e);
        }
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        lock (_syncRoot)
        {
            base.OnPropertyChanged(e);
        }
    }

    public new void Add(T item)
    {
        lock (_syncRoot)
        {
            base.Add(item);
        }
    }

    public new void Remove(T item)
    {
        lock (_syncRoot)
        {
            base.Remove(item);
        }
    }

    public new void Clear()
    {
        lock (_syncRoot)
        {
            base.Clear();
        }
    }

    public new void Insert(int index, T item)
    {
        lock (_syncRoot)
        {
            base.Insert(index, item);
        }
    }

    public new void RemoveAt(int index)
    {
        lock (_syncRoot)
        {
            base.RemoveAt(index);
        }
    }
}
