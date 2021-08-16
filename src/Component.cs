using System;
using System.Collections.Generic;

namespace Necs
{
    public struct Empty { }

    public struct ComponentInfo : IComparable<ComponentInfo>
    {
        private static ulong _id = 0;
        private static Dictionary<ulong, ulong?> _priority = new();

        public string Name;
        public ulong Id;
        public ulong? ParentId;
        public bool IsEntity;
        public ulong Tree;
        public byte TreeDepth;
        public ulong Branch;
        public ulong Priority => GetTreePriority(Tree);

        public static ComponentInfo Create()
        {
            var i = new ComponentInfo()
            {
                Name = "",
                Id = _id,
                Tree = _id,
                IsEntity = false,
                Branch = 0,
            };

            _id++;
            return i;
        }

        public bool IsDescendantOf(ref ComponentInfo other)
        {
            if (other.Tree != Tree || other.TreeDepth >= TreeDepth) return false;
            ulong mask = other.TreeDepth == 0 ? 0 : ulong.MaxValue << (8 * (8 - other.TreeDepth));
            return (Branch & mask) == (other.Branch & mask);
        }

        public int CompareTo(ComponentInfo other)
        {
            var res = Priority.CompareTo(other.Priority);
            if (res != 0) return res;
            res = Tree.CompareTo(other.Tree);
            if (res != 0) return res;
            res = Branch.CompareTo(other.Branch);
            return res;
        }

        public static void SetTreePriority(ulong tree, ulong priority) => _priority[tree] = priority;

        public static ulong GetTreePriority(ulong tree) => _priority.GetValueOrDefault(tree) is ulong p ? p : ulong.MaxValue;
    }
}