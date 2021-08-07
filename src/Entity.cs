using System.Collections.Generic;

namespace Necs
{
    public class Entity
    {
        private EcsContext _context = new EcsContext();
        public ComponentInfo Info => _context.GetEntityInfo(Id);

        internal EcsContext Context => _context;

        public ulong Id { get; }

        public Entity()
        {
            var info = ComponentInfo.Create();
            info.IsEntity = true;
            Id = info.Id;
            _context.AddEntity(info);
        }

        public void AddChild(Entity child)
        {
            _context.AddEntity(child);
            _context.AddComponentToEntity(Id, child.Id);
        }

        public void RemoveChild(Entity child) => _context.RemoveComponentFromEntity(Id, child.Id);

        public void AddComponent<T>(T component) => _context.AddComponentToEntity(Id, component);

        public ref T GetComponent<T>() => ref _context.GetEntityComponent<T>(Id);

        public void SetPriority(ulong priority) => _context.UpdatePriority(Id, priority);

        public void SetContext(EcsContext context, bool copy = true)
        {
            if (_context == context) return;
            if (copy) _context.CopyTo(context);
            _context = context;
        }
    }

    public struct EntityData
    {
        public HashSet<ulong> Children { get; set; }

        public static EntityData Create() => new EntityData() { Children = new() };
    }
}