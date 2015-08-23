using System;
using System.Collections.Generic;

namespace StatefulModel
{
    public class AnonymousComparer<T> : IComparer<T>
    {
        private readonly Func<T, T, int> _comparer;
        public AnonymousComparer(Func<T, T, int> comparer)
        {
            _comparer = comparer;
        }

        public int Compare(T x, T y)
        {
            return _comparer(x, y);
        }
    }
}
