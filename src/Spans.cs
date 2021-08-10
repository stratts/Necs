using System;

namespace Necs
{
    public static class SpanExtensions
    {
        public static ReversedSpan<T> Reversed<T>(this Span<T> span) => new ReversedSpan<T>(span);
    }

    public ref struct ReversedSpan<T>
    {
        private Span<T> _data;
        private int _idx;

        public int Length => _data.Length;

        public ReversedSpan(Span<T> span)
        {
            _data = span;
            _idx = span.Length;
        }

        public ref T Current => ref _data[_idx];

        public ref T this[int idx] => ref _data[_data.Length - 1 - idx];

        public bool MoveNext()
        {
            _idx--;
            return _idx >= 0 ? true : false;
        }

        public ReversedSpan<T> GetEnumerator() => new(_data);
    }

    public ref struct MappedSpan<T>
    {
        private Span<int> _map;
        private Span<T> _data;

        private int _idx;

        public int Length => _data.Length;

        public ref T Current => ref _data[_map[_idx]];

        public MappedSpan(Span<T> src, Span<int> map)
        {
            _idx = -1;
            _map = map;
            _data = src;
        }

        public ref T this[int idx] => ref _data[_map[idx]];

        public MappedSpan<T> GetEnumerator() => new(_data, _map);

        public bool MoveNext()
        {
            _idx++;
            if (_idx < _data.Length) return true;
            _idx = _data.Length - 1;
            return false;
        }
    }
}