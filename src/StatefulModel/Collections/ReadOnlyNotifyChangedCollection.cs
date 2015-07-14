using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection;
using System.Threading;
using StatefulModel.EventListeners;

namespace StatefulModel
{
    public class ReadOnlyNotifyChangedCollection<T> : ReadOnlyCollection<T>, INotifyCollectionChanged, INotifyPropertyChanged, IDisposable
    {
        private bool _disposed;

        private bool _isDisposableType;

        public ReadOnlyNotifyChangedCollection(ISynchronizableNotifyChangedCollection<T> collection)
            : base(collection)
        {
            if (collection == null)
            {
                throw new ArgumentNullException("collection");
            }

            EventListeners = new CompositeDisposable();

            _isDisposableType = typeof(IDisposable).GetTypeInfo().IsAssignableFrom(typeof(T).GetTypeInfo());

            lock (collection.Synchronizer.LockObject)
            {
                SourceCollection = collection;

                EventListeners.Add(new PropertyChangedEventListener(SourceCollection, (sender, e) => OnPropertyChanged(e)));
                EventListeners.Add(new CollectionChangedEventListener(SourceCollection, (sender, e) => OnCollectionChanged(e)));
            }
        }

        public ISynchronizableNotifyChangedCollection SourceCollection { get; private set; }

        public CompositeDisposable EventListeners { get; private set; }

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnCollectionChanged(NotifyCollectionChangedEventArgs args)
        {
            var threadSafeHandler = Interlocked.CompareExchange(ref CollectionChanged, null, null);

            if (threadSafeHandler != null)
            {
                threadSafeHandler(this, args);
            }
        }

        protected void OnPropertyChanged(PropertyChangedEventArgs args)
        {
            var threadSafeHandler = Interlocked.CompareExchange(ref PropertyChanged, null, null);

            if (threadSafeHandler != null)
            {
                threadSafeHandler(this, args);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                if (EventListeners.Count != 0)
                {
                    EventListeners.Dispose();

                    if (_isDisposableType)
                    {
                        foreach (var unknown in this)
                        {
                            var i = (IDisposable) unknown;
                            i.Dispose();
                        }
                    }
                }
            }
            _disposed = true;
        }
    }

    public static class ReadOnlyNotifyChangedCollectionExtensions
    {
        public static ReadOnlyNotifyChangedCollection<T> ToSyncedReadOnlyNotifyChangedCollection<T>(
            this ISynchronizableNotifyChangedCollection<T> source)
        {
            return new ReadOnlyNotifyChangedCollection<T>(source);
        }
    }
}
