using System;

namespace Necs
{
    public ref struct SortedSpan<T>
    {
        private Span<int> _map;
        private Span<T> _data;

        private int _idx;

        public ref T Current => ref _data[_map[_idx]];

        private SortedSpan(Span<T> src, Span<int> map)
        {
            _idx = -1;
            _map = map;
            _data = src;
            for (int i = 0; i < src.Length; i++) _map[i] = i;
        }

        public SortedSpan(Span<T> src, Span<int> map, Comparison<T> comparison) : this(src, map)
        {
            _data.Sort(_map, comparison);
        }

        public ref T this[int idx] => ref _data[_map[idx]];

        public SortedSpan<T> GetEnumerator() => new(_data, _map);

        public bool MoveNext()
        {
            _idx++;
            if (_idx < _data.Length) return true;
            return false;
        }
    }
}