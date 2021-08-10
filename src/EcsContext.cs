using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Necs.Debug"), InternalsVisibleTo("Necs.Benchmark")]

namespace Necs
{
    public class EcsContext
    {
        private Dictionary<ulong, IComponentList> _map = new();
        protected List<IComponentList> _lists = new();

        internal EcsContext() { }

        // Public methods

        public void AddEntity(Entity entity) => entity.SetContext(this);

        public void RemoveEntity(Entity entity)
        {
            RemoveComponentTree(entity.Id);
        }

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

        public void UpdatePriority(ulong entityId, ulong priority)
        {
            GetEntityInfo(entityId).Priority = priority;
            var entityData = GetEntityData(entityId);
            GetList<EntityData>().Resort(entityId);

            foreach (var child in entityData.Children)
            {
                var list = GetList(child);
                if (list.Type == typeof(EntityData)) continue;
                list.GetInfo(child).Priority = priority;
                list.Resort(child);
            }
        }

        public void CopyTo(EcsContext target)
        {
            foreach (var list in _lists) target.CopyFromList(list);
        }


        // Internal methods

        internal void CopyFromList(IComponentList src)
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

        internal void AddEntity(ComponentInfo info) => AddComponent(info, EntityData.Create());

        internal void AddComponentToEntity<T>(ulong entityId, T component)
        {
            var componentInfo = ComponentInfo.Create();
            AddComponent(componentInfo, component);
            AddComponentToEntity(entityId, componentInfo.Id);
        }

        internal void AddComponentToEntity(ulong entityId, ulong componentId)
        {
            ref var info = ref GetInfo(componentId);
            if (info.ParentId != null) throw new ArgumentException("Component already has a parent, cannot add to entity");

            ref var entityInfo = ref GetEntityInfo(entityId);

            info.ParentId = entityId;
            info.Tree = entityInfo.Tree;
            info.TreeDepth = info.IsEntity ? (byte)(entityInfo.TreeDepth + 1) : entityInfo.TreeDepth;
            info.Branch = entityInfo.Branch;

            var entityData = GetList<EntityData>().GetData(entityId);
            entityData.Children.Add(componentId);

            if (info.IsEntity)
            {
                for (byte i = 1; i <= byte.MaxValue; i++)
                {
                    if (!entityData.Branches.ContainsValue(i))
                    {
                        entityData.Branches[componentId] = i;
                        info.Branch |= ((ulong)i) << (8 * (7 - entityInfo.TreeDepth));
                        break;
                    }
                }
                UpdateTree(info);
            }

            GetList(componentId).Resort(componentId);
        }

        internal void UpdateTree(ComponentInfo entityInfo)
        {
            var data = GetEntityData(entityInfo.Id);
            data.Branches.Clear();

            foreach (var id in data.Children)
            {
                ref var child = ref GetInfo(id);

                child.TreeDepth = child.IsEntity ? (byte)(entityInfo.TreeDepth + 1) : entityInfo.TreeDepth;
                child.Tree = entityInfo.Tree;

                if (child.IsEntity)
                {
                    for (byte i = 1; i <= byte.MaxValue; i++)
                    {
                        if (!data.Branches.ContainsValue(i))
                        {
                            data.Branches[id] = i;
                            child.Branch |= (ulong)i << (8 * (7 - entityInfo.TreeDepth));
                            break;
                        }
                    }

                    UpdateTree(child);
                }
                else child.Branch = entityInfo.Branch;

                GetList(id).Resort(id);
            }
        }

        internal void RemoveComponentFromEntity(ulong entityId, ulong componentId)
        {
            var entityData = GetList<EntityData>().GetData(entityId);
            entityData.Children.Remove(componentId);
            entityData.Branches.Remove(componentId);

            ref var info = ref GetInfo(componentId);
            info.ParentId = null;
            info.Tree = info.Id;
            info.TreeDepth = 0;

            RemoveComponentTree(componentId);
        }

        internal ref T GetEntityComponent<T>(ulong entityId)
        {
            var list = GetList<T>();
            var idx = list.GetByParent(entityId);
            if (idx == null)
                throw new ArgumentException("Entity does not have component of that type");
            else return ref list.Data[idx.Value];
        }

        internal IEnumerable<ulong> GetTree(ulong id)
        {
            var info = GetInfo(id);
            if (info.IsEntity)
            {
                var c = GetEntityData(id).Children;
                yield return id;

                foreach (var child in c)
                {
                    foreach (var childDesc in GetTree(child)) yield return childDesc;
                }
            }
            else yield return id;
        }

        internal void AddComponent<T>(ComponentInfo info, T component)
        {
            var list = GetList<T>();
            list.Add(info, component);
            _map[info.Id] = list;
        }

        internal void RemoveComponent(ulong id)
        {
            GetList(id).Remove(id);
            _map.Remove(id);
        }

        internal void RemoveComponentTree(ulong id)
        {
            foreach (var treeId in GetTree(id)) RemoveComponent(treeId);
        }

        internal Span<T> GetComponents<T>() => GetList<T>().Data;

        internal ref ComponentInfo GetInfo(ulong componentId)
        {
            var list = GetList(componentId);
            return ref list.GetInfo(componentId);
        }

        // Protected and private methods

        internal ComponentList<T> GetList<T>()
        {
            foreach (var list in _lists)
            {
                if (list.Cast<T>() is ComponentList<T> l) return l;
            }
            var newList = new ComponentList<T>();
            _lists.Add(newList);
            return newList;
        }

        protected IComponentList GetList(ulong componentId)
        {
            var present = _map.TryGetValue(componentId, out var list);
            if (!present || list == null) throw new ArgumentException("Invalid component ID supplied");
            return list;
        }

        private void UpdateMapping(Span<ComponentInfo> infos, IComponentList list)
        {
            foreach (var info in infos) _map[info.Id] = list;
        }
    }

    public delegate void ComponentAction<TUpdateContext, T>(TUpdateContext context, ref T a);

    public delegate void ComponentAction<TUpdateContext, T1, T2>(TUpdateContext context, ref T1 a, ref T2 b);

    public delegate void ParentAction<TUpdateContext, T>(TUpdateContext context, ref T a, ref T parent, bool hasParent) where T : struct;

    public delegate void SpanConsumer<TUpdateContext, T>(TUpdateContext context, Span<T> components);

    public interface IComponentSystem<TUpdateContext, T>
    {
        void Process(TUpdateContext context, ref T component);
    }

    public interface IComponentSystem<TUpdateContext, T1, T2>
    {
        void Process(TUpdateContext context, ref T1 a, ref T2 b);
    }

    public interface IComponentIteratorSystem<TUpdateContext, T>
    {
        void Process(TUpdateContext context, ComponentIterator<T> components);
    }

    public class EcsContext<TUpdateContext> : EcsContext
    {
        private List<Action<TUpdateContext>> _systems = new();

        public void AddSystem<T>(IComponentSystem<TUpdateContext, T> system) => AddSystem<T>(system.Process);

        public void AddSystem<T1, T2>(IComponentSystem<TUpdateContext, T1, T2> system) => AddSystem<T1, T2>(system.Process);

        public void AddSystem<T>(IComponentIteratorSystem<TUpdateContext, T> system)
        {
            var action = new Action<TUpdateContext>(ctx =>
            {
                var components = GetList<T>();
                var iterator = new ComponentIterator<T>(this, components.Data);
                system.Process(ctx, iterator);
            });

            _systems.Add(action);
        }

        public void AddSystem<T>(SpanConsumer<TUpdateContext, T> method)
        {
            var action = new Action<TUpdateContext>(ctx =>
            {
                var components = GetList<T>();
                method?.Invoke(ctx, components.Data);
            });

            _systems.Add(action);
        }

        public void AddSystem<T>(ComponentAction<TUpdateContext, T> method)
        {
            var action = new Action<TUpdateContext>(ctx =>
            {
                var components = GetList<T>();

                foreach (ref var c in components.Data)
                {
                    method?.Invoke(ctx, ref c);
                }
            });

            _systems.Add(action);
        }

        public void AddSystem<T1, T2>(ComponentAction<TUpdateContext, T1, T2> method)
        {
            var action = new Action<TUpdateContext>(ctx =>
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
                            method.Invoke(ctx, ref list1.Data[i], ref list2.Data[j]);
                            offset = j + 1;
                            break;
                        }
                        else if (tree2 != tree)
                        {
                            offset = j;
                            break;
                        }
                    }
                }
            });

            _systems.Add(action);
        }

        public void AddSystem<T>(ParentAction<TUpdateContext, T> method) where T : struct
        {
            var action = new Action<TUpdateContext>(ctx =>
            {
                var list = GetList<T>();
                var infos = list.Infos;
                var data = list.Data;

                ref ComponentInfo prevParent = ref infos[0];
                ref T prevData = ref data[0];
                T none = default;

                for (int i = 1; i < infos.Length; i++)
                {
                    ref var info = ref infos[i];
                    ref var d = ref data[i];

                    if (info.Tree != prevParent.Tree) method.Invoke(ctx, ref d, ref none, false);
                    else if (info.IsDescendantOf(ref prevParent)) method.Invoke(ctx, ref d, ref prevData, true);
                    else
                    {
                        for (int j = i - 2; j >= 0; j--)
                        {
                            var prev = infos[j];
                            if (info.IsDescendantOf(ref prev))
                            {
                                method.Invoke(ctx, ref d, ref data[j], true);
                                break;
                            }
                            else if (info.Tree != prev.Tree)
                            {
                                method.Invoke(ctx, ref d, ref none, false);
                                break;
                            }
                        }
                    }

                    prevParent = ref info;
                    prevData = ref d;
                }
            });

            _systems.Add(action);
        }

        public void Update(TUpdateContext context)
        {
            foreach (var system in _systems) system.Invoke(context);
        }
    }
}