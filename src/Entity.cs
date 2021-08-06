using System.Collections.Generic;

namespace Necs
{
    public class Entity
    {
        private EcsSystem _system = new();
        public ComponentInfo Info => _system.GetEntityInfo(Id);

        public ulong Id { get; }

        public Entity()
        {
            var info = ComponentInfo.Create();
            Id = info.Id;
            _system.AddEntity(info);
        }

        public void AddChild(Entity entity)
        {
            _system.AddEntity(entity);
            _system.GetEntityInfo(entity.Id).ParentId = Id;
            _system.GetEntityData(Id).Children.Add(entity.Id);
        }

        public void AddComponent<T>(T component)
        {
            var info = ComponentInfo.Create(Id);
            _system.AddComponent(info, component);
        }

        public ref T GetComponent<T>() => ref _system.GetComponent<T>(Id);

        public void SetSystem(EcsSystem system)
        {
            _system.CopyTo(system);
            _system = system;
        }
    }

    public struct EntityData
    {
        public HashSet<ulong> Children { get; set; }

        public static EntityData Create() => new EntityData() { Children = new() };
    }
}