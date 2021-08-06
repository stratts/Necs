using System;

namespace Necs
{
    public interface IComponentList
    {
        ComponentList<T>? Cast<T>();
        Span<ComponentInfo> Info { get; }
        void CopyTo(IComponentList dest);
        Type Type { get; }
        ref ComponentInfo GetInfo(ulong id);
    }

    public class ComponentList<T> : IComponentList
    {
        private ComponentInfo[] _info = new ComponentInfo[4];
        private T[] _data = new T[4];
        private int _count = 0;

        public int Count => _count;
        public Span<ComponentInfo> Info => _info.AsSpan(0, _count);
        public Span<T> Data => _data.AsSpan(0, _count);
        public Type Type { get; } = typeof(T);

        ComponentList<T1>? IComponentList.Cast<T1>() => this is ComponentList<T1> l ? l : null;

        private int CompareInfo(ComponentInfo a, ComponentInfo b) => a.Priority.CompareTo(b.Priority);

        public ref ComponentInfo GetInfo(ulong id)
        {
            foreach (ref var info in Info)
            {
                if (info.Id == id) return ref info;
            }
            throw new ArgumentException("Entity with that ID is not found in this list");
        }

        public ref T GetData(ulong id)
        {
            for (int i = 0; i < _count; i++)
            {
                if (_info[i].Id == id) return ref _data[i];
            }
            throw new ArgumentException("Entity with that ID is not found in this list");
        }

        public void Add(ComponentInfo info, T data)
        {
            bool inserted = false;
            for (int i = 0; i < _count; i++)
            {
                var curr = _info[i];
                if (CompareInfo(curr, info) >= 0)
                {
                    var srcData = _data.AsSpan(i, _count - i);
                    var destData = _data.AsSpan(i + 1, _count - i);
                    srcData.CopyTo(destData);

                    var srcInfo = _info.AsSpan(i, _count - i);
                    var destInfo = _info.AsSpan(i + 1, _count - i);
                    srcInfo.CopyTo(destInfo);

                    _info[i] = info;
                    _data[i] = data;
                    inserted = true;
                    break;
                }
            }
            if (!inserted)
            {
                _info[_count] = info;
                _data[_count] = data;
            }
            _count++;

            if (_count >= _data.Length)
            {
                var tmpData = new T[_data.Length * 2];
                _data.CopyTo(tmpData, 0);
                _data = tmpData;

                var tmpInfo = new ComponentInfo[_info.Length * 2];
                _info.CopyTo(tmpInfo, 0);
                _info = tmpInfo;
            }
        }

        public void Remove(ulong id)
        {
            for (int i = 0; i < _count; i++)
            {
                if (_info[i].Id == id)
                {
                    for (int j = i; j < _count - 1; j++)
                    {
                        _data[j] = _data[j + 1];
                        _info[j] = _info[j + 1];
                    }
                    _count--;
                    break;
                }
            }
        }

        public void CopyTo(IComponentList dest)
        {
            if (dest is ComponentList<T> l)
            {
                for (int i = 0; i < _count; i++) l.Add(_info[i], _data[i]);
                return;
            }
            throw new ArgumentException("Cannot copy to list, incorrect type");
        }
    }
}