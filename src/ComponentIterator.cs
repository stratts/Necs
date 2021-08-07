using System;

namespace Necs
{
    public ref struct ComponentRef<T>
    {
        private Span<T> _data;
        private int _idx;

        public ComponentInfo Entity { get; }
        public ref T Component => ref _data[_idx];

        public ComponentRef(ComponentInfo parent, Span<T> data, int idx)
        {
            Entity = parent;
            _data = data;
            _idx = idx;
        }
    }

    public ref struct ComponentIterator<T>
    {
        private EcsContext _context;
        private Span<ComponentInfo> _info;
        private Span<T> _data;

        private ComponentInfo _currentInfo;
        private int _idx;

        public ComponentRef<T> Current => new ComponentRef<T>(_currentInfo, _data, _idx);

        public ComponentIterator(EcsContext context, Span<ComponentInfo> info, Span<T> data)
        {
            _context = context;
            _currentInfo = default;
            _data = data;
            _info = info;
            _idx = -1;
        }

        public bool MoveNext()
        {
            _idx++;
            if (_idx < _info.Length && _idx < _data.Length)
            {
                _currentInfo = _context.GetEntityInfo(_info[_idx].ParentId!.Value);
                return true;
            }
            return false;
        }

        public ComponentIterator<T> GetEnumerator() => this;
    }
}