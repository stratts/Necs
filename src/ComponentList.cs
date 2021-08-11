using System;
using System.Collections.Generic;

namespace Necs
{
    public interface IComponentList
    {
        ComponentList<T>? Cast<T>();
        Span<ComponentInfo> Infos { get; }
        void CopyTo(IComponentList dest);
        Type Type { get; }
        ref ComponentInfo GetInfo(ulong id);
        void Remove(ulong id);
        void Resort(ulong id);
    }

    public class InfoComparer : IComparer<ComponentInfo>
    {
        private Dictionary<ulong, int> _treePriority;

        public InfoComparer(Dictionary<ulong, int> treePriority) => _treePriority = treePriority;

        public int Compare(ComponentInfo x, ComponentInfo y)
        {
            var res = x.Tree.CompareTo(y.Tree);
            if (res != 0) return res;
            res = (x.Branch).CompareTo(y.Branch);
            return res;
        }
    };

    public record TreeComparable(ulong Tree) : IComparable<ComponentInfo>
    {
        public int CompareTo(ComponentInfo other) => Tree.CompareTo(other.Tree);
    }

    public class ComponentList<T> : IComponentList
    {
        private ComponentInfo[] _info = new ComponentInfo[4];
        private T[] _data = new T[4];
        private int _count = 0;
        private Dictionary<ulong, int> _treePriority = new();
        private IComparer<ComponentInfo> _comparer;
        private Dictionary<ulong, ulong> _treeMap = new();
        private Dictionary<ulong, ulong> _parentMap = new();

        public int Count => _count;
        public Span<ComponentInfo> Infos => _info.AsSpan(0, _count);
        public Span<T> Data => _data.AsSpan(0, _count);
        public Type Type { get; } = typeof(T);

        public ComponentList()
        {
            _comparer = new InfoComparer(_treePriority);
        }

        ComponentList<T1>? IComponentList.Cast<T1>() => this is ComponentList<T1> l ? l : null;

        private int CompareInfo(ComponentInfo a, ComponentInfo b) => a.Priority.CompareTo(b.Priority);

        public int? GetIndexOf(ulong id) => GetIndexOf(_treeMap[id], info => info.Id == id);

        public ref ComponentInfo GetInfo(ulong id)
        {
            var idx = GetIndexOf(id);
            if (idx != null) return ref _info[idx.Value];
            throw new ArgumentException("Component with that ID not found in list");
        }

        public ref T GetData(ulong id)
        {
            var idx = GetIndexOf(id);
            if (idx != null) return ref _data[idx.Value];
            throw new ArgumentException("Component with that ID not found in list");
        }

        public int? GetByParent(ulong parentId)
        {
            var present = _parentMap.TryGetValue(parentId, out var id);
            if (!present) return null;
            return GetIndexOf(_treeMap[id], info => info.Id == id);
        }

        private int? GetIndexOf(ulong tree, Predicate<ComponentInfo> match)
        {
            var treeIdx = Infos.BinarySearch(new TreeComparable(tree));
            if (treeIdx < 0) throw new ArgumentException("Binary search for tree failed! Tree not found or list not sorted yet");
            while (treeIdx - 1 >= 0 && _info[treeIdx - 1].Tree == tree) treeIdx--;
            for (int i = treeIdx; i < _count; i++)
            {
                if (match(_info[i])) return i;
                else if (_info[i].Tree != tree) break;
            }

            return null;
        }

        public void Add(ComponentInfo info, T data)
        {
            _treeMap[info.Id] = info.Tree;
            if (info.ParentId != null) _parentMap[info.ParentId.Value] = info.Id;

            var idx = Infos.BinarySearch(info, _comparer);

            if (idx < 0) idx = ~idx;
            if (idx < _count)
            {
                Array.Copy(_info, idx, _info, idx + 1, _count - idx);
                Array.Copy(_data, idx, _data, idx + 1, _count - idx);
            }

            _info[idx] = info;
            _data[idx] = data;

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
            var idx = GetIndexOf(id);
            if (idx == null) return;
            var i = idx.Value;

            _treeMap.Remove(id);
            if (_info[i].ParentId != null) _parentMap.Remove(_info[i].Id);

            Array.Copy(_info, i + 1, _info, i, _count - i);
            Array.Copy(_data, i + 1, _data, i, _count - i);
            _count--;
        }

        public void Resort(ulong id)
        {
            var oldIdx = Infos.IndexOf(new ComponentInfo() { Id = id });
            if (oldIdx < 0) throw new ArgumentException("ID not found");

            var info = Infos[oldIdx];
            var data = Data[oldIdx];

            _treeMap[info.Id] = info.Tree;
            if (info.ParentId != null) _parentMap[info.ParentId.Value] = info.Id;

            var lower = Infos.Slice(0, oldIdx);
            var upper = Infos.Slice(oldIdx + 1);

            var lowerIdx = lower.BinarySearch(info, _comparer);
            var upperIdx = upper.BinarySearch(info, _comparer);

            int newIdx;

            if (~lowerIdx == lower.Length && ~upperIdx == 0) return; // Already in correct spot
            else if (~lowerIdx == lower.Length && upperIdx > 0) newIdx = lower.Length + upperIdx;
            else if (~lowerIdx == lower.Length && upperIdx < 0) newIdx = Math.Min(lower.Length + ~upperIdx, _count - 1);
            else newIdx = ~lowerIdx;

            if (newIdx < 0) newIdx = ~newIdx;

            int start;
            int shift;
            int length = (int)Math.Abs(oldIdx - newIdx);

            if (oldIdx < newIdx)
            {
                start = oldIdx + 1;
                shift = -1;
            }
            else if (oldIdx > newIdx)
            {
                start = newIdx;
                shift = 1;
            }
            else
            {
                return;
            }

            Array.Copy(_info, start, _info, start + shift, length);
            Array.Copy(_data, start, _data, start + shift, length);

            _info[newIdx] = info;
            _data[newIdx] = data;
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