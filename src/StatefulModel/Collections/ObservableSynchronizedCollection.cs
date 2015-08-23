using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;

namespace StatefulModel
{
    public sealed class ObservableSynchronizedCollection<T> :ICollection, IReadOnlyList<T>, ISynchronizableNotifyChangedCollection<T>
    {
        private readonly IList<T> _list;
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        public ObservableSynchronizedCollection() : this(Enumerable.Empty<T>()) { }

        public ObservableSynchronizedCollection(IEnumerable<T> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            _list = new List<T>(source);
            Synchronizer = new Synchronizer<T>(this);
        }

        public int IndexOf(T item)
        {
            return ReadWithLockAction(() => _list.IndexOf(item));
        }

        public void Insert(int index, T item)
        {
            ReadAndWriteWithLockAction(() => _list.Insert(index, item),
                () =>
                {
                    OnPropertyChanged("Count");
                    OnPropertyChanged("Item[]");
                    OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
                });
        }

        public void RemoveAt(int index)
        {
            ReadAndWriteWithLockAction(() => _list[index],
                removeItem => _list.RemoveAt(index),
                removeItem =>
                {
                    OnPropertyChanged("Count");
                    OnPropertyChanged("Item[]");
                    OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removeItem, index));
                });
        }

        public T this[int index]
        {
            get
            {
                return ReadWithLockAction(() => _list[index]);
            }
            set
            {
                ReadAndWriteWithLockAction(() => _list[index],
                    oldItem =>
                    {
                        _list[index] = value;
                    },
                    oldItem =>
                    {
                        OnPropertyChanged("Item[]");
                        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, _list[index], oldItem, index));
                    });
            }
        }

        public void Add(T item)
        {
            ReadAndWriteWithLockAction(() => _list.Add(item),
                () =>
                {
                    OnPropertyChanged("Count");
                    OnPropertyChanged("Item[]");
                    OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, _list.Count - 1));
                });
        }

        public void Clear()
        {
            ReadAndWriteWithLockAction(() => _list.Count,
            count =>
            {
                if (count != 0)
                {
                    _list.Clear();
                }
            },
            count =>
            {
                if (count != 0)
                {
                    OnPropertyChanged("Count");
                    OnPropertyChanged("Item[]");
                    OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                }
            });
        }

        public bool Contains(T item) => ReadWithLockAction(() => _list.Contains(item));

        public void CopyTo(T[] array, int arrayIndex) => ReadWithLockAction(() => _list.CopyTo(array, arrayIndex));

        public int Count => ReadWithLockAction(() => _list.Count);

        public bool IsReadOnly => _list.IsReadOnly;

        public bool Remove(T item)
        {
            bool result = false;

            ReadAndWriteWithLockAction(() => _list.IndexOf(item),
                index =>
                {
                    result = _list.Remove(item);
                },
                index =>
                {
                    if (result)
                    {
                        OnPropertyChanged("Count");
                        OnPropertyChanged("Item[]");
                        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, index));
                    }
                });

            return result;
        }

        public void Move(int oldIndex, int newIndex)
        {
            ReadAndWriteWithLockAction(() => _list[oldIndex],
                item =>
                {
                    _list.RemoveAt(oldIndex);
                    _list.Insert(newIndex, item);
                },
                item =>
                {
                    OnPropertyChanged("Item[]");
                    OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, item, newIndex, oldIndex));
                });
        }

        public IEnumerator<T> GetEnumerator() => ReadWithLockAction(() => ((IEnumerable<T>)_list.ToArray()).GetEnumerator());

        IEnumerator IEnumerable.GetEnumerator() => ReadWithLockAction(() => ((IEnumerable<T>)_list.ToArray()).GetEnumerator());

        public void CopyTo(Array array, int index) => CopyTo(array.Cast<T>().ToArray(), index);

        public bool IsSynchronized => true;

        public object SyncRoot { get; } = new object();

        private void OnCollectionChanged(NotifyCollectionChangedEventArgs args) => CollectionChanged?.Invoke(this, args);


        private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));


        private void ReadWithLockAction(Action readAction)
        {
            if (!_lock.IsReadLockHeld)
            {
                _lock.EnterReadLock();
                try
                {
                    readAction();
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
            else
            {
                readAction();
            }
        }

        private TResult ReadWithLockAction<TResult>(Func<TResult> readAction)
        {
            if (!_lock.IsReadLockHeld)
            {
                _lock.EnterReadLock();
                try
                {
                    return readAction();
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }

            return readAction();
        }

        private void ReadAndWriteWithLockAction(Action writeAction, Action readAfterWriteAction)
        {
            lock (Synchronizer.LockObject)
            {
                _lock.EnterUpgradeableReadLock();
                try
                {
                    _lock.EnterWriteLock();
                    try
                    {
                        writeAction();
                    }
                    finally
                    {
                        _lock.ExitWriteLock();
                    }

                    _lock.EnterReadLock();

                    try
                    {
                        readAfterWriteAction();
                    }
                    finally
                    {
                        _lock.ExitReadLock();
                    }
                }
                finally
                {
                    _lock.ExitUpgradeableReadLock();
                }
            }
        }

        private void ReadAndWriteWithLockAction<TResult>(Func<TResult> readBeforeWriteAction, Action<TResult> writeAction, Action<TResult> readAfterWriteAction)
        {
            lock (Synchronizer.LockObject)
            {
                _lock.EnterUpgradeableReadLock();
                try
                {
                    TResult readActionResult = readBeforeWriteAction();

                    _lock.EnterWriteLock();

                    try
                    {
                        writeAction(readActionResult);
                    }
                    finally
                    {
                        _lock.ExitWriteLock();
                    }

                    _lock.EnterReadLock();

                    try
                    {
                        readAfterWriteAction(readActionResult);
                    }
                    finally
                    {
                        _lock.ExitReadLock();
                    }
                }
                finally
                {
                    _lock.ExitUpgradeableReadLock();
                }
            }
        
        }

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public event PropertyChangedEventHandler PropertyChanged;

        public Synchronizer<T> Synchronizer{get;}

        public void Dispose() => Synchronizer.Dispose();
    }

    public static class ObservableSynchronizedCollectionExtensions
    {
        public static ObservableSynchronizedCollection<T> ToSyncedObservableSynchronizedCollection<T>(
            this ISynchronizableNotifyChangedCollection<T> source) => ToSyncedObservableSynchronizedCollection(source, _ => _);

        public static ObservableSynchronizedCollection<TResult> ToSyncedObservableSynchronizedCollection<TSource, TResult>(
            this ISynchronizableNotifyChangedCollection<TSource> source,
            Func<TSource, TResult> converter)
        {
            lock (source.Synchronizer.LockObject)
            {
                var result = new ObservableSynchronizedCollection<TResult>();
                foreach (var item in source)
                {
                    result.Add(converter(item));
                }

                var collectionChangedListener =
                    SynchronizableNotifyChangedCollectionHelper.CreateSynchronizableCollectionChangedEventListener(
                        source, result,
                        converter);
                result.Synchronizer.EventListeners.Add(collectionChangedListener);
                return result;
            }
        }
    }
}
