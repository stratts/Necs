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

        private ComponentInfo(ulong? parent = null)
        {
            Id = _id;
            _id++;
            ParentId = parent;
            Priority = parent != null ? parent.Value : Id;
            IsEntity = false;
        }

        public static ComponentInfo Create() => new ComponentInfo(null);

        public static ComponentInfo Create(ulong parent) => new ComponentInfo(parent);

        public static ComponentInfo Create(ComponentInfo parent) => new ComponentInfo(parent.Id);

        public bool Equals(ComponentInfo other) => Id == other.Id;
    }
}