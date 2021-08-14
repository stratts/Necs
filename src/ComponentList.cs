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
        void SetTreePriority(ulong treeId, ulong priority);
        bool HasTree(ulong treeId);
    }

    public class InfoComparer : IComparer<ComponentInfo>
    {
        private Dictionary<ulong, ulong> _treePriority;

        public InfoComparer(Dictionary<ulong, ulong> treePriority) => _treePriority = treePriority;

        public int Compare(ComponentInfo x, ComponentInfo y)
        {
            var res = _treePriority[x.Tree].CompareTo(_treePriority[y.Tree]);
            if (res != 0) return res;
            res = x.Tree.CompareTo(y.Tree);
            if (res != 0) return res;
            res = (x.Branch).CompareTo(y.Branch);
            return res;
        }
    };

    public struct TreeComparable : IComparable<ComponentInfo>
    {
        private Dictionary<ulong, ulong> _treePriority;

        public ulong? Id { get; }
        public ulong Tree { get; }

        public TreeComparable(ulong tree, Dictionary<ulong, ulong> treePriority, ulong? id = null)
            => (Id, Tree, _treePriority) = (id, tree, treePriority);

        public int CompareTo(ComponentInfo other)
        {
            if (Id != null && other.Id == Id.Value) return 0;
            var res = _treePriority[Tree].CompareTo(_treePriority[other.Tree]);
            if (res != 0) return res;
            return Tree.CompareTo(other.Tree);
        }
    }

    public struct PriorityComparable : IComparable<ComponentInfo>
    {
        private Dictionary<ulong, ulong> _treePriority;
        private ulong _priority;

        public PriorityComparable(ulong priority, Dictionary<ulong, ulong> treePriority) => (_priority, _treePriority) = (priority, treePriority);

        public int CompareTo(ComponentInfo other) => _priority.CompareTo(_treePriority[other.Tree]);
    }

    public class ComponentList<T> : IComponentList
    {
        private ComponentInfo[] _tempInfo = new ComponentInfo[128];
        private T[] _tempData = new T[128];
        private ComponentInfo[] _info = new ComponentInfo[4];
        private T[] _data = new T[4];
        private int _count = 0;
        private Dictionary<ulong, ulong> _treePriority = new();
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

        public ref ComponentInfo GetInfo(ulong id)
        {
            var idx = GetIndexOfId(id);
            if (idx != null) return ref _info[idx.Value];
            throw new ArgumentException("Component with that ID not found in list");
        }

        public ref T GetData(ulong id)
        {
            var idx = GetIndexOfId(id);
            if (idx != null) return ref _data[idx.Value];
            throw new ArgumentException("Component with that ID not found in list");
        }

        public int? GetByParent(ulong parentId)
        {
            var present = _parentMap.TryGetValue(parentId, out var id);
            if (!present) return null;
            return GetIndexOfId(id);
        }

        private (int Start, int End) GetTreeSpan(ulong tree)
        {
            int start, end;
            var treeIdx = Infos.BinarySearch(new TreeComparable(tree, _treePriority));
            if (treeIdx < 0) throw new ArgumentException("Binary search for tree failed! Tree not found or list not sorted yet");
            for (start = treeIdx; start - 1 >= 0 && _info[start - 1].Tree == tree; start--) ;
            for (end = treeIdx; end + 1 < _count && _info[end + 1].Tree == tree; end++) ;
            return (start, end);
        }

        public int? GetIndexOfId(ulong id)
        {
            var tree = _treeMap[id];
            var treeIdx = Infos.BinarySearch(new TreeComparable(tree, _treePriority, id));
            if (treeIdx < 0) throw new ArgumentException("Binary search for tree failed! Tree not found or list not sorted yet");
            while (treeIdx - 1 >= 0)
            {
                if (_info[treeIdx - 1].Id == id) return treeIdx - 1;
                else if (_info[treeIdx - 1].Tree != tree) break;
                treeIdx--;
            }
            for (int i = treeIdx; i < _count; i++)
            {
                if (_info[i].Id == id) return i;
                else if (_info[i].Tree != tree) break;
            }

            return null;
        }

        public void Add(ComponentInfo info, T data)
        {
            _treeMap[info.Id] = info.Tree;
            if (!_treePriority.ContainsKey(info.Tree)) _treePriority[info.Tree] = 0;

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
            var idx = GetIndexOfId(id);
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
            var idx = GetIndexOfId(id);
            if (idx == null) throw new ArgumentException("ID not found");
            var oldIdx = idx.Value;

            var info = Infos[oldIdx];
            var data = Data[oldIdx];

            _treeMap[info.Id] = info.Tree;
            if (!_treePriority.ContainsKey(info.Tree)) _treePriority[info.Tree] = 0;

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

        public bool HasTree(ulong treeId) => _treePriority.ContainsKey(treeId);

        public void SetTreePriority(ulong treeId, ulong priority)
        {
            if (_treePriority[treeId] == priority) return;

            var span = GetTreeSpan(treeId);
            var count = span.End - span.Start + 1;
            var priorityIdx = Infos.BinarySearch(new PriorityComparable(priority, _treePriority));
            if (priorityIdx < 0) priorityIdx = ~priorityIdx;

            int target = priorityIdx;

            // Find start of priority
            while (target - 1 >= 0 && _treePriority[Infos[target - 1].Tree] == priority && Infos[target - 1].Tree >= treeId) target--;

            // Find first index with higher tree or different priority
            while (target < _count && _treePriority[Infos[target].Tree] == priority && Infos[target].Tree < treeId) target++;

            var len = Math.Abs(target - span.End);

            if (target == span.Start) return;

            var tempInfo = _tempInfo.AsSpan(0, count);
            var tempData = _tempData.AsSpan(0, count);

            Infos.Slice(span.Start, count).CopyTo(tempInfo);
            Data.Slice(span.Start, count).CopyTo(tempData);

            if (target > span.Start)
            {
                _info.AsSpan(span.End + 1, len).CopyTo(_info.AsSpan(span.Start, len));
                _data.AsSpan(span.End + 1, len).CopyTo(_data.AsSpan(span.Start, len));
                tempInfo.CopyTo(_info.AsSpan(target - count, count));
                tempData.CopyTo(_data.AsSpan(target - count, count));
            }
            else if (target < span.Start)
            {
                _info.AsSpan(target, len).CopyTo(_info.AsSpan(target + count, len));
                _data.AsSpan(target, len).CopyTo(_data.AsSpan(target + count, len));
                tempInfo.CopyTo(_info.AsSpan(target, count));
                tempData.CopyTo(_data.AsSpan(target, count));
            }
            else return;

            _treePriority[treeId] = priority;
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