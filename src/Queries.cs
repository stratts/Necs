using System;

namespace Necs
{
    public delegate void ComponentAction<T>(ref T a);

    public delegate void ComponentAction<T1, T2>(ref T1 a, ref T2 b);

    public delegate void EntityAction<T>(EntityInfo entityId, ref T a);

    public delegate void ParentAction<T>(ref T a, ref T parent, bool hasParent);

    public delegate void ParentAction<T1, T2>(ref T1 a, ref T2 b, ref T2 parent, bool hasParent);

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

    static class TempArray<T>
    {
        private static T[] _data = new T[128];

        public static Span<T> Create(int count)
        {
            if (_data.Length < count) _data = new T[count * 2];
            return _data.AsSpan(0, count);
        }
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

        private Span<(int, int)> GetPairs<T1, T2>()
        {
            var list1 = GetList<T1>();
            var list2 = GetList<T2>();

            var mask = list1.TypeMask | list2.TypeMask;

            var masks1 = list1.Masks;
            var masks2 = list2.Masks;

            var pairs = TempArray<(int, int)>.Create(Math.Min(list1.Count, list2.Count));
            int count = 0;

            var offset = 0;

            for (int i = 0; i < list1.Count; i++)
            {
                if ((masks1[i] & mask) != mask) continue;

                for (int j = offset; j < list2.Count; j++)
                {
                    if ((masks2[j] & mask) != mask) continue;

                    pairs[count] = (i, j);
                    count++;
                    offset = j + 1;
                    break;
                }
            }

            pairs = pairs.Slice(0, count);
            return pairs;
        }

        public void Query<T1, T2>(ComponentAction<T1, T2> method, bool reverse = false)
        {
            var list1 = GetList<T1>();
            var list2 = GetList<T2>();

            bool flip = list1.Count > list2.Count ? true : false;

            var pairs = flip ? GetPairs<T2, T1>() : GetPairs<T1, T2>();

            var data1 = list1.Data;
            var data2 = list2.Data;

            var (start, end, inc) = reverse ? (pairs.Length - 1, -1, -1) : (0, pairs.Length, 1);
            int cur = start;

            while (cur != end)
            {
                var (c1, c2) = pairs[cur];
                if (flip) method.Invoke(ref data1[c2], ref data2[c1]);
                else method.Invoke(ref data1[c1], ref data2[c2]);
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
                else method.Invoke(ref data[i], ref none!, false);
                i += inc;
            }
        }

        public void Query<T1, T2>(ParentAction<T1, T2> method, bool reverse = false)
        {
            var list1 = GetList<T1>();
            var list2 = GetList<T2>();

            bool flip = list1.Count > list2.Count ? true : false;
            var pairs = flip ? GetPairs<T2, T1>() : GetPairs<T1, T2>();
            T2? none = default;

            var data1 = list1.Data;
            var data2 = list2.Data;

            var infos1 = list1.Infos;
            var infos2 = list2.Infos;

            var (start, end, inc) = reverse ? (pairs.Length - 1, -1, -1) : (0, pairs.Length, 1);
            int cur = start;

            while (cur != end)
            {
                var (c1, c2) = pairs[cur];
                if (flip)
                {
                    var parent = infos2[c1].ParentLoc;
                    if (parent > 0) method.Invoke(ref data1[c2], ref data2[c1], ref data2[c1 - parent]!, true);
                    else method.Invoke(ref data1[c2], ref data2[c1], ref none!, false);
                }
                else
                {
                    var parent = infos2[c2].ParentLoc;
                    if (parent > 0) method.Invoke(ref data1[c1], ref data2[c2], ref data2[c2 - parent]!, true);
                    else method.Invoke(ref data1[c1], ref data2[c2], ref none!, false);
                }
                cur += inc;
            }
        }
    }
}