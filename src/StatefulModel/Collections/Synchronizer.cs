using System;
using System.Collections.Generic;
using System.Reflection;

namespace StatefulModel
{
    public class Synchronizer<T>
    {
        private object _lockObject = new object();
        private CompositeDisposable _listeners = new CompositeDisposable();
        protected bool Disposed;

        private bool _isDisposableType;

        public Synchronizer(IList<T> currentCollection)
        {
            CurrentCollection = currentCollection;
            _isDisposableType = typeof(IDisposable).GetTypeInfo().IsAssignableFrom(typeof(T).GetTypeInfo());
        }

        public IList<T> CurrentCollection { get; private set; }

        public CompositeDisposable EventListeners { get { return _listeners; } }

        public object LockObject { get { return _lockObject; } }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (Disposed) return;

            if (disposing)
            {
                if (EventListeners.Count != 0)
                {
                    EventListeners.Dispose();
                    CurrentCollection.Clear();
                    if (_isDisposableType)
                    {
                        foreach (var unknown in CurrentCollection)
                        {
                            var i = (IDisposable) unknown;
                            i.Dispose();
                        }
                    }
                }
            }
            Disposed = true;
        }

    }
}
