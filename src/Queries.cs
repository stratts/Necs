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
        private int _length;
        private EntityInfo _entity;

        public int Length => _length;

        public ComponentIterator(Span<ComponentInfo> info, Span<T> data)
        {
            _info = info;
            _data = data;
            _idx = -1;
            _length = data.Length;
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
    }

    public interface IEcsContext
    {
        Span<T> GetSpan<T>();
        ComponentIterator<T> GetIterator<T>();
        void Query<T>(ComponentAction<T> callback);
        void Query<T1, T2>(ComponentAction<T1, T2> callback);
        void Query<T>(EntityAction<T> callback);
        void Query<T>(ParentAction<T> callback);
        void QueryParent<T1, T2>(ParentAction<T1, T2> callback);
    }

    public partial class EcsContext : IEcsContext
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

            foreach (ref var c in components.Data)
            {
                method.Invoke(ref c);
            }
        }

        public void Query<T1, T2>(ComponentAction<T1, T2> method)
        {
            var list1 = GetList<T1>();
            var list2 = GetList<T2>();

            var info1 = list1.Infos;
            var info2 = list2.Infos;

            var offset = 0;

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
                        method.Invoke(ref list1.Data[i], ref list2.Data[j]);
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

        public void Query<T>(ParentAction<T> method)
        {
            var list = GetList<T>();
            var infos = list.Infos;
            var data = list.Data;

            T? none = default;

            if (infos.Length == 0) return;

            ulong prevTree = infos[0].Tree;
            ulong prevDepth = infos[0].TreeDepth;
            ref var prevData = ref data[0];

            method.Invoke(ref prevData, ref none, false);

            for (int i = 1; i < infos.Length; i++)
            {
                ref var info = ref infos[i];
                ref var d = ref data[i];

                if (info.Tree != prevTree) method.Invoke(ref d, ref none, false);
                else if (info.TreeDepth > prevDepth) method.Invoke(ref d, ref prevData, true);
                else
                {
                    for (int j = i - 2; j >= 0; j--)
                    {
                        var prev = infos[j];
                        if (info.TreeDepth > prev.TreeDepth && info.IsDescendantOf(ref prev))
                        {
                            method.Invoke(ref d, ref data[j]!, true);
                            break;
                        }
                        else if (info.Tree != prev.Tree)
                        {
                            method.Invoke(ref d, ref none, false);
                            break;
                    }
                }
            }

                prevTree = info.Tree;
                prevDepth = info.TreeDepth;
                prevData = ref d;
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