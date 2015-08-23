using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;

namespace StatefulModel
{
    public sealed class SynchronizationContextCollection<T> : ObservableCollection<T>,ISynchronizableNotifyChangedCollection<T>
    {
        public SynchronizationContextCollection(SynchronizationContext context) : this(Enumerable.Empty<T>(), context) { }

        public SynchronizationContextCollection(IEnumerable<T> collection, SynchronizationContext context) : base(collection)
        {
            if (collection == null) throw new ArgumentNullException(nameof(collection));
            if (context == null) throw new ArgumentNullException(nameof(context));
            Synchronizer = new Synchronizer<T>(this);
            Context = context;
        }
        public SynchronizationContext Context { get; }

        protected override void InsertItem(int index, T item)
        {
            lock (Synchronizer.LockObject)
            {
                DoOnContext(() => base.InsertItem(index, item));
            }
        }

        protected override void RemoveItem(int index)
        {
            lock (Synchronizer.LockObject)
            {
                DoOnContext(() => base.RemoveItem(index));
            }
        }

        protected override void SetItem(int index, T item)
        {
            lock (Synchronizer.LockObject)
            {
                DoOnContext(() => base.SetItem(index, item));
            }
        }

        protected override void ClearItems()
        {
            lock (Synchronizer.LockObject)
            {
                DoOnContext(() => base.ClearItems());
            }
        }

        private void DoOnContext(Action action) => Context.Send(_ => action(), null);

        public Synchronizer<T> Synchronizer { get; }

        public void Dispose() => Synchronizer.Dispose();
    }

    public static class SynchronizationContextCollectionExtensions
    {
        public static SynchronizationContextCollection<T> ToSyncedSynchronizationContextCollection<T>(
            this ISynchronizableNotifyChangedCollection<T> source,
            SynchronizationContext context) => ToSyncedSynchronizationContextCollection(source, _ => _, context);

        public static SynchronizationContextCollection<TResult> ToSyncedSynchronizationContextCollection<TSource, TResult>(
            this ISynchronizableNotifyChangedCollection<TSource> source, 
            Func<TSource, TResult> converter,
            SynchronizationContext context)
        {
            lock (source.Synchronizer.LockObject)
            {
                var result = new SynchronizationContextCollection<TResult>(context);
                foreach (var item in source)
                {
                    result.Add(converter(item));
                }

                var collectionChangedListener = SynchronizableNotifyChangedCollectionHelper.CreateSynchronizableCollectionChangedEventListener(source, result,
                    converter);
                result.Synchronizer.EventListeners.Add(collectionChangedListener);
                return result;
            }
        }
    }
}
