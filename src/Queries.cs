using System;

namespace Necs
{
    public delegate void ComponentAction<T>(ref T a);

    public delegate void ComponentAction<T1, T2>(ref T1 a, ref T2 b);

    public delegate void EntityAction<T>(EntityInfo entityId, ref T a);

    public delegate void ParentAction<T>(ref T a, ref T? parent, bool hasParent);

    public delegate void ParentAction<T1, T2>(ref T1 a, ref T2 b, ref T2? parent, bool hasParent);

    public struct EntityInfo
    {
        public ulong Id;
        public byte Depth;
    }

    public ref struct ComponentIterator<T>
    {
        private int _idx;
        private Span<ComponentInfo> _info;
        private Span<T> _data;
        private EntityInfo _entity;

        internal ComponentIterator(Span<ComponentInfo> info, Span<T> data)
        {
            _info = info;
            _data = data;
            _idx = -1;
            _entity = new();
        }

        public bool MoveNext() => ++_idx < _data.Length;

        public EntityInfo Entity
        {
            get
            {
                ref var info = ref _info[_idx];
                _entity.Id = info.ParentId!.Value;
                _entity.Depth = info.TreeDepth;
                return _entity;
            }
        }

        public ref T Component => ref _data[_idx];

        public bool HasParent => _info[_idx].ParentLoc != 0;

        public ref T Parent => ref _data[_idx - _info[_idx].ParentLoc];
    }

    public partial class EcsContext
    {
        public ComponentIterator<T> GetIterator<T>()
        {
            var list = GetList<T>();
            return new ComponentIterator<T>(list.Infos, list.Data);
        }

        public Span<T> GetSpan<T>() => GetList<T>().Data;

        public void Query<T>(ComponentAction<T> method)
        {
            var components = GetList<T>();

            foreach (ref var c in components.Data) method.Invoke(ref c);
        }

        public void Query<T1, T2>(ComponentAction<T1, T2> method, bool reverse = false)
        {
            var list1 = GetList<T1>();
            var list2 = GetList<T2>();

            var infos1 = list1.Infos;
            var infos2 = list2.Infos;

            Span<(int, int)> pairs = stackalloc (int, int)[Math.Min(list1.Count, list2.Count)];
            int count = 0;

            var offset = 0;

            for (int i = 0; i < list1.Count; i++)
            {
                ref var info1 = ref infos1[i];
                var tree = info1.Tree;
                var parent = info1.ParentId;
                var priority = info1.Priority;

                for (int j = offset; j < list2.Count; j++)
                {
                    ref var info2 = ref infos2[j];
                    var tree2 = info2.Tree;
                    var parent2 = info2.ParentId;
                    var priority2 = info2.Priority;

                    if (parent2 == parent)
                    {
                        pairs[count] = (i, j);
                        count++;
                        offset = j + 1;
                        break;
                    }
                    else if (priority < priority2 || (priority == priority2 && tree < tree2))
                    {
                        offset = j;
                        break;
                    }
                }
            }

            var data1 = list1.Data;
            var data2 = list2.Data;

            var (start, end, inc) = reverse ? (count - 1, -1, -1) : (0, count, 1);
            int cur = start;

            while (cur != end)
            {
                var (c1, c2) = pairs[cur];
                method?.Invoke(ref data1[c1], ref data2[c2]);
                cur += inc;
            }
        }

        public void Query<T>(EntityAction<T> method)
        {
            var components = GetList<T>();
            var infos = components.Infos;
            var data = components.Data;

            for (int i = 0; i < components.Count; i++)
            {
                ref var info = ref infos[i];
                method.Invoke(new EntityInfo() { Id = info.ParentId!.Value, Depth = info.TreeDepth }, ref data[i]);
            }
        }

        public void Query<T>(ParentAction<T> method, bool reverse = false)
        {
            var list = GetList<T>();
            var infos = list.Infos;

            if (infos.Length == 0) return;

            T? none = default;
            var data = list.Data;

            var (start, end, inc) = reverse ? (data.Length - 1, -1, -1) : (0, data.Length, 1);
            int i = start;

            while (i != end)
            {
                var parent = infos[i].ParentLoc;
                if (parent > 0) method.Invoke(ref data[i], ref data[i - parent]!, true);
                else method.Invoke(ref data[i], ref none, false);
                i += inc;
            }
        }

        public void QueryParent<T1, T2>(ParentAction<T1, T2> method)
        {
            var list1 = GetList<T1>();
            var list2 = GetList<T2>();

            var info1 = list1.Infos;
            var info2 = list2.Infos;

            var offset = 0;

            T2? empty = default;

            for (int i = 0; i < list1.Count; i++)
            {
                var tree = info1[i].Tree;
                var parent = info1[i].ParentId;

                for (int j = offset; j < list2.Count; j++)
                {
                    var tree2 = info2[j].Tree;
                    var parent2 = info2[j].ParentId;
                    if (parent2 == parent)
                    {
                        var desc = info2[j];
                        for (int k = j - 1; k >= 0; k--)
                        {
                            ref var prev = ref info2[k];
                            if (desc.IsDescendantOf(ref prev))
                            {
                                method.Invoke(ref list1.Data[i], ref list2.Data[j], ref list2.Data[k]!, true);
                                break;
                            }
                            else if (prev.Tree != desc.Tree)
                            {
                                method.Invoke(ref list1.Data[i], ref list2.Data[j], ref empty, false);
                                break;
                            }
                        }

                        offset = j + 1;
                        break;
                    }
                    else if (tree2 > tree)
                    {
                        offset = j;
                        break;
                    }
                }
            }
        }
    }
}