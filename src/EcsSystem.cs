using System;
using System.Collections.Generic;

namespace Necs
{
    public delegate void ComponentAction<T>(ComponentInfo entity, ref T a);

    public delegate void ComponentAction<T1, T2>(ComponentInfo entity, ref T1 a, ref T2 b);

    public delegate void SpanConsumer<T>(Span<T> components);

    public interface IComponentSystem<T>
    {
        void Process(ComponentInfo entity, ref T component);
    }

    public interface IComponentSystem<T1, T2>
    {
        void Process(ComponentInfo entity, ref T1 a, ref T2 b);
    }

    public interface IComponentIteratorSystem<T>
    {
        void Process(ComponentIterator<T> components);
    }


    public class EcsContext
    {
        private Dictionary<ulong, IComponentList> _map = new();
        private List<IComponentList> _lists = new();
        private List<Action<float>> _systems = new();

        private ComponentList<T> GetList<T>()
        {
            foreach (var list in _lists)
            {
                if (list.Cast<T>() is ComponentList<T> l) return l;
            }
            var newList = new ComponentList<T>();
            _lists.Add(newList);
            return newList;
        }

        private IComponentList GetList(ulong componentId)
        {
            var present = _map.TryGetValue(componentId, out var list);
            if (!present || list == null) throw new ArgumentException("Invalid component ID supplied");
            return list;
        }

        private void UpdateMapping(Span<ComponentInfo> infos, IComponentList list)
        {
            foreach (var info in infos) _map[info.Id] = list;
        }

        public void CopyTo(EcsContext target)
        {
            foreach (var list in _lists) target.CopyFromList(list);
        }

        public void CopyFromList(IComponentList src)
        {
            foreach (var list in _lists)
            {
                if (list.Type == src.Type)
                {
                    src.CopyTo(list);
                    UpdateMapping(src.Infos, list);
                    return;
                }
            }
            _lists.Add(src);
            UpdateMapping(src.Infos, src);
        }

        internal void AddEntity(ComponentInfo info)
        {
            var list = GetList<EntityData>();
            list.Add(info, EntityData.Create());
        }

        public void AddEntity(Entity entity) => entity.SetContext(this);

        public void AddComponent<T>(ComponentInfo info, T component)
        {
            if (info.ParentId == null) throw new ArgumentException("Component added without parent");
            var list = GetList<T>();
            list.Add(info, component);
            _map[info.Id] = list;
            var data = GetEntityData(info.ParentId.Value);
            data.Children.Add(info.Id);
        }

        public ref T GetComponent<T>(ulong entityId)
        {
            var list = GetList<T>();
            var info = list.Infos;
            for (int i = 0; i < list.Count; i++)
            {
                if (info[i].ParentId == entityId) return ref list.Data[i];
            }
            throw new ArgumentException("Entity does not have component of that type");
        }

        public Span<T> GetComponents<T>() => GetList<T>().Data;

        public ref ComponentInfo GetEntityInfo(ulong entityId)
        {
            var list = GetList<EntityData>();
            try
            {
                return ref list.GetInfo(entityId);
            }
            catch (ArgumentException)
            {
                throw new ArgumentException("Entity with that ID has not been added to system");
            }
        }

        public EntityData GetEntityData(ulong entityId)
        {
            var list = GetList<EntityData>();
            try
            {
                return list.GetData(entityId);
            }
            catch (ArgumentException)
            {
                throw new ArgumentException("Entity with that ID has not been added to system");
            }
        }

        private ref ComponentInfo GetInfo(ulong componentId)
        {
            var list = GetList(componentId);
            return ref list.GetInfo(componentId);
        }

        public void UpdatePriority(ulong entityId, ulong priority)
        {
            GetEntityInfo(entityId).Priority = priority;
            var entityData = GetEntityData(entityId);

            foreach (var child in entityData.Children)
            {
                var list = GetList(child);
                if (list.Type == typeof(EntityData)) continue;
                list.GetInfo(child).Priority = priority;
            }
        }

        public void AddSystem<T>(IComponentSystem<T> system) => AddSystem<T>(system.Process);

        public void AddSystem<T1, T2>(IComponentSystem<T1, T2> system) => AddSystem<T1, T2>(system.Process);

        public void AddSystem<T>(IComponentIteratorSystem<T> system)
        {
            var action = new Action<float>(elapsed =>
            {
                var components = GetList<T>();
                var iterator = new ComponentIterator<T>(this, components.Infos, components.Data);
                system.Process(iterator);
            });

            _systems.Add(action);
        }

        public void AddSystem<T>(SpanConsumer<T> method)
        {
            var action = new Action<float>(elapsed =>
            {
                var components = GetList<T>();
                method?.Invoke(components.Data);
            });

            _systems.Add(action);
        }

        public void AddSystem<T>(ComponentAction<T> method)
        {
            var action = new Action<float>(elapsed =>
            {
                var components = GetList<T>();

                for (int i = 0; i < components.Infos.Length; i++)
                {
                    var info = components.Infos[i];
                    var entity = GetEntityInfo(info.ParentId!.Value);

                    method?.Invoke(entity, ref components.Data[i]);
                }
            });

            _systems.Add(action);
        }

        public void AddSystem<T1, T2>(ComponentAction<T1, T2> method)
        {
            var action = new Action<float>(elapsed =>
            {
                var list1 = GetList<T1>();
                var list2 = GetList<T2>();

                var info1 = list1.Infos;
                var info2 = list2.Infos;

                var offset = 0;

                for (int i = 0; i < list1.Count; i++)
                {
                    var parent = info1[i].ParentId;

                    for (int j = offset; j < list2.Count; j++)
                    {
                        var parent2 = info2[j].ParentId;
                        if (parent2 == parent) method?.Invoke(GetEntityInfo(parent!.Value), ref list1.Data[i], ref list2.Data[j]);
                        else if (parent2 > parent)
                        {
                            offset = j;
                            break;
                        }
                    }
                }
            });

            _systems.Add(action);
        }

        public void Update(float elapsed)
        {
            foreach (var system in _systems) system.Invoke(elapsed);
        }
    }

}