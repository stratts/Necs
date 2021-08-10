using System;

namespace Necs
{
    public ref struct ComponentRef<T>
    {
        private Span<T> _data;
        private int _idx;

        public ref T Component => ref _data[_idx];

        public ComponentRef(Span<T> data, int idx)
        {
            _data = data;
            _idx = idx;
        }
    }

    public ref struct ComponentIterator<T>
    {
        private EcsContext _context;
        private Span<T> _data;
        private ComponentRef<T> _current;

        private int _idx;

        public ComponentRef<T> Current => _current;

        public ComponentIterator(EcsContext context, Span<T> data)
        {
            _context = context;
            _data = data;
            _idx = -1;
            _current = default;
        }

        public bool MoveNext()
        {
            _idx++;
            if (_idx < _data.Length)
            {
                _current = new ComponentRef<T>(_data, _idx);
                return true;
            }
            return false;
        }

        public ComponentIterator<T> GetEnumerator() => this;
    }
}