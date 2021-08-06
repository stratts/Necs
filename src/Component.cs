
namespace Necs
{
    public struct Empty { }

    public struct ComponentInfo
    {
        private static ulong _id = 0;

        public ulong Id;
        public ulong? ParentId;
        public ulong Priority;

        private ComponentInfo(ulong? parent = null)
        {
            Priority = 0;
            Id = _id;
            _id++;
            ParentId = parent;
        }

        public static ComponentInfo Create() => new ComponentInfo(null);

        public static ComponentInfo Create(ulong parent) => new ComponentInfo(parent);

        public static ComponentInfo Create(ComponentInfo parent) => new ComponentInfo(parent.Id);
    }
}