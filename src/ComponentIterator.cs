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
        private Span<ComponentInfo> _info;
        private Span<T> _data;
        private ComponentRef<T> _current;

        private ComponentInfo _currentInfo;
        private int _idx;

        public ComponentRef<T> Current => _current;

        public ComponentIterator(EcsContext context, Span<ComponentInfo> info, Span<T> data)
        {
            _context = context;
            _currentInfo = default;
            _data = data;
            _info = info;
            _idx = -1;
            _current = default;
        }

        public bool MoveNext()
        {
            _idx++;
            if (_idx < _info.Length && _idx < _data.Length)
            {
                _currentInfo = default;
                _current = new ComponentRef<T>(_data, _idx);
                return true;
            }
            return false;
        }

        public ComponentIterator<T> GetEnumerator() => this;
    }
}