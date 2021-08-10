using System;

namespace Necs
{
    public struct Empty { }

    public struct ComponentInfo : IEquatable<ComponentInfo>
    {
        private static ulong _id = 0;

        public ulong Id;
        public ulong? ParentId;
        public ulong Priority;
        public bool IsEntity;
        public ulong Tree;
        public byte TreeDepth;
        public ulong Branch;

        public static ComponentInfo Create()
        {
            var i = new ComponentInfo()
            {
                Id = _id,
                Priority = _id,
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

        public bool Equals(ComponentInfo other) => Id == other.Id;
    }
}