using System;
using System.Collections.Generic;

namespace Necs
{
    interface IComponentList
    {
        ComponentList<T>? Cast<T>();
        Span<ComponentInfo> Infos { get; }
        void CopyTo(IComponentList dest, HashSet<ulong>? filter = null);
        Type Type { get; }
        ref ComponentInfo GetInfo(ulong id);
        void Remove(ulong id);
        void Resort(ulong id);
        void ResortTree(ulong treeId, ulong priority);
        bool HasTree(ulong treeId);
        int Count { get; }
        ulong TypeMask { get; }
        void SetTypeMask(ulong id, ulong mask);
    }

    static class TypeId
    {
        private static int _current = 0;
        private static Dictionary<Type, int> _types = new();

        public static int GetId<T>()
        {
            if (_types.TryGetValue(typeof(T), out var id)) return id;
            else
            {
                _types[typeof(T)] = _current;
                _current++;
                return _current - 1;
            }
        }

        public static ulong GetMask<T>() => (ulong)1 << GetId<T>();
    }

    struct TreeComparable : IComparable<ComponentInfo>
    {
        public ulong? Id { get; }
        public ulong Tree { get; }

        public TreeComparable(ulong tree, ulong? id = null)
            => (Id, Tree) = (id, tree);

        public int CompareTo(ComponentInfo other)
        {
            if (Id != null && other.Id == Id.Value) return 0;
            var res = ComponentInfo.GetTreePriority(Tree).CompareTo(other.Priority);
            if (res != 0) return res;
            return Tree.CompareTo(other.Tree);
        }
    }

    struct PriorityComparable : IComparable<ComponentInfo>
    {
        private ulong _priority;

        public PriorityComparable(ulong priority) => _priority = priority;

        public int CompareTo(ComponentInfo other) => _priority.CompareTo(other.Priority);
    }

    class ComponentList<T> : IComponentList
    {
        private ComponentInfo[] _tempInfo = new ComponentInfo[128];
        private T[] _tempData = new T[128];
        private ulong[] _tempMasks = new ulong[128];

        private ulong[] _masks = new ulong[4];
        private ComponentInfo[] _info = new ComponentInfo[4];
        private T[] _data = new T[4];
        private int _count = 0;
        private HashSet<ulong> _trees = new();
        private Dictionary<ulong, ulong> _treeMap = new();
        private Dictionary<ulong, ulong> _parentMap = new();

        public int Count => _count;
        public Span<ComponentInfo> Infos => _info.AsSpan(0, _count);
        public Span<T> Data => _data.AsSpan(0, _count);
        public ReadOnlySpan<ulong> Masks => _masks.AsSpan(0, _count);

        public Type Type { get; } = typeof(T);
        public ulong TypeMask { get; } = TypeId.GetMask<T>();

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
            var treeIdx = Infos.BinarySearch(new TreeComparable(tree));
            if (treeIdx < 0) throw new ArgumentException("Binary search for tree failed! Tree not found or list not sorted yet");
            return GetTreeSpan(tree, treeIdx);
        }

        private (int Start, int End) GetTreeSpan(ulong tree, int treeIdx)
        {
            int start, end;
            for (start = treeIdx; start - 1 >= 0 && _info[start - 1].Tree == tree; start--) ;
            for (end = treeIdx; end + 1 < _count && _info[end + 1].Tree == tree; end++) ;
            return (start, end);
        }

        public void SetTypeMask(ulong id, ulong mask)
        {
            var idx = GetIndexOfId(id);
            if (idx != null)
            {
                _masks[idx.Value] = mask;
                _info[idx.Value].TypeMask = mask;
            }
            else throw new ArgumentException("Component with that ID not found in list");
        }

        public int? GetIndexOfId(ulong id)
        {
            var tree = _treeMap[id];
            var treeIdx = Infos.BinarySearch(new TreeComparable(tree, id));
            if (treeIdx < 0)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (_info[i].Id == id) throw new Exception($"ID found at index {i}, but binary search failed. List not sorted properly!");
                }
                throw new ArgumentException("ID not found in list");
            }
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
            _trees.Add(info.Tree);
            if (info.ParentId != null) _parentMap[info.ParentId.Value] = info.Id;

            var idx = Infos.BinarySearch(info);

            if (idx < 0) idx = ~idx;
            if (idx < _count) CopyData(idx, idx + 1, _count - idx);

            _info[idx] = info;
            _data[idx] = data;
            _masks[idx] = info.TypeMask;

            _count++;

            if (_count >= _data.Length)
            {
                var tmpData = new T[_data.Length * 2];
                _data.CopyTo(tmpData, 0);
                _data = tmpData;

                var tmpInfo = new ComponentInfo[_info.Length * 2];
                _info.CopyTo(tmpInfo, 0);
                _info = tmpInfo;

                var tmpMasks = new ulong[_masks.Length * 2];
                _masks.CopyTo(tmpMasks, 0);
                _masks = tmpMasks;
            }

            UpdateTreeInfo(idx);
        }

        public void Remove(ulong id)
        {
            var idx = GetIndexOfId(id);
            if (idx == null) return;
            var i = idx.Value;

            var (treeStart, treeEnd) = GetTreeSpan(_treeMap[id], i);
            if (treeStart == treeEnd) _trees.Remove(_treeMap[id]);
            _treeMap.Remove(id);
            if (_info[i].ParentId != null) _parentMap.Remove(_info[i].Id);
            _info[i].ParentLoc = 0;

            CopyData(i + 1, i, _count - i);
            _count--;

            UpdateTreeInfo(i);
            if (i > 0) UpdateTreeInfo(i - 1);
        }

        public void Resort(ulong id)
        {
            var idx = GetIndexOfId(id);
            if (idx == null) throw new ArgumentException("ID not found");
            var oldIdx = idx.Value;

            var info = Infos[oldIdx];
            var data = Data[oldIdx];
            var mask = _masks[oldIdx];

            _treeMap[info.Id] = info.Tree;

            if (info.ParentId != null) _parentMap[info.ParentId.Value] = info.Id;

            var lower = Infos.Slice(0, oldIdx);
            var upper = Infos.Slice(oldIdx + 1);

            var lowerIdx = lower.BinarySearch(info);
            var upperIdx = upper.BinarySearch(info);

            int newIdx;

            if (~lowerIdx == lower.Length && ~upperIdx == 0)
            {
                UpdateTreeInfo(oldIdx);
                return; // Already in correct spot
            }
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
                UpdateTreeInfo(oldIdx);
                return;
            }

            CopyData(start, start + shift, length);

            _info[newIdx] = info;
            _data[newIdx] = data;
            _masks[newIdx] = mask;

            UpdateTreeInfo(newIdx);
        }

        private void CopyData(int oldPos, int newPos, int length)
        {
            Array.Copy(_info, oldPos, _info, newPos, length);
            Array.Copy(_data, oldPos, _data, newPos, length);
            Array.Copy(_masks, oldPos, _masks, newPos, length);
        }

        public void UpdateTreeInfo(int idx)
        {
            var tree = _info[idx].Tree;
            var (start, end) = GetTreeSpan(tree, idx);

            for (int i = start; i < end + 1; i++)
            {
                ref var info = ref _info[i];
                var depth = info.TreeDepth;

                for (int j = i - 1; j >= 0; j--)
                {
                    ref var prev = ref _info[j];
                    if (tree != prev.Tree)
                    {
                        _info[i].ParentLoc = 0;
                        break;
                    }
                    else if (depth > prev.TreeDepth && info.IsDescendantOf(ref prev))
                    {
                        info.ParentLoc = (byte)(i - j);
                        break;
                    }
                }
            }
        }

        public bool HasTree(ulong treeId) => _trees.Contains(treeId);

        public void ResortTree(ulong treeId, ulong priority)
        {
            var span = GetTreeSpan(treeId);
            var count = span.End - span.Start + 1;
            var priorityIdx = Infos.BinarySearch(new PriorityComparable(priority));
            if (priorityIdx < 0) priorityIdx = ~priorityIdx;

            int target = priorityIdx;

            // Find start of priority
            while (target - 1 >= 0 && Infos[target - 1].Priority == priority && Infos[target - 1].Tree >= treeId) target--;

            // Find first index with higher tree or different priority
            while (target < _count && Infos[target].Priority == priority && Infos[target].Tree < treeId) target++;

            for (int i = span.Start; i < span.End + 1; i++) _info[i].Priority = priority;

            if (ComponentInfo.GetTreePriority(treeId) == priority) return;

            if (target == span.Start) return;

            var tempInfo = _tempInfo.AsSpan(0, count);
            var tempData = _tempData.AsSpan(0, count);
            var tempMasks = _tempMasks.AsSpan(0, count);

            Infos.Slice(span.Start, count).CopyTo(tempInfo);
            Data.Slice(span.Start, count).CopyTo(tempData);
            _masks.AsSpan(span.Start, count).CopyTo(tempMasks);

            if (target > span.Start)
            {
                if (span.End + 1 < _count)
                {
                    var len = Math.Abs(target - (span.End + 1));
                    if (len == 0) return;
                    CopyData(span.End + 1, span.Start, len);
                    tempInfo.CopyTo(Infos.Slice(target - count, count));
                    tempData.CopyTo(Data.Slice(target - count, count));
                    tempMasks.CopyTo(_masks.AsSpan(target - count, count));
                }
            }
            else if (target < span.Start)
            {
                var len = Math.Abs(target - span.Start);
                if (len == 0) return;
                CopyData(target, target + count, len);
                tempInfo.CopyTo(Infos.Slice(target, count));
                tempData.CopyTo(Data.Slice(target, count));
                tempMasks.CopyTo(_masks.AsSpan(target, count));
            }
            else return;
        }

        public void CopyTo(IComponentList dest, HashSet<ulong>? filter = null)
        {
            if (dest is ComponentList<T> l)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (filter == null || filter.Contains(_info[i].Id)) l.Add(_info[i], _data[i]);
                }
                return;
            }
            throw new ArgumentException("Cannot copy to list, incorrect type");
        }
    }
}