using System.Collections.Generic;

namespace Necs
{
    public class Entity
    {
        private IEcsContext _context = new EcsContext();
        public ComponentInfo Info => _context.GetEntityInfo(Id);

        public ulong Id { get; }

        public Entity()
        {
            var info = ComponentInfo.Create();
            Id = info.Id;
            _context.AddEntity(info);
        }

        public void AddChild(Entity entity)
        {
            _context.AddEntity(entity);
            _context.GetEntityInfo(entity.Id).ParentId = Id;
            _context.GetEntityData(Id).Children.Add(entity.Id);
        }

        public void AddComponent<T>(T component)
        {
            var info = ComponentInfo.Create(Id);
            info.Priority = Info.Priority;
            _context.AddComponent(info, component);
        }

        public ref T GetComponent<T>() => ref _context.GetComponent<T>(Id);

        public void SetPriority(ulong priority) => _context.UpdatePriority(Id, priority);

        public void SetContext(IEcsContext context)
        {
            _context.CopyTo(context);
            _context = context;
        }
    }

    public struct EntityData
    {
        public HashSet<ulong> Children { get; set; }

        public static EntityData Create() => new EntityData() { Children = new() };
    }
}