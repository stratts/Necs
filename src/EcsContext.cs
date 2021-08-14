using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

[assembly: InternalsVisibleTo("Necs.Debug"), InternalsVisibleTo("Necs.Benchmark"), InternalsVisibleTo("Necs.Tests")]

namespace Necs
{
    public class EcsContext
    {
        protected bool Lock = false;

        private Dictionary<ulong, IComponentList> _map = new();
        protected Queue<Action> _deferred = new();
        protected List<IComponentList> _lists = new();

        internal EcsContext() { }

        // Public methods

        public void AddEntity(Entity entity)
        {
            Do(() => entity.SetContext(this));
        }

        public void RemoveEntity(Entity entity)
        {
            Do(() => RemoveComponentTree(entity.Id));
        }

        public ref ComponentInfo GetEntityInfo(ulong entityId)
        {
            var list = GetList<EntityData>();
            return ref list.GetInfo(entityId);
        }

        public EntityData GetEntityData(ulong entityId)
        {
            var list = GetList<EntityData>();
            return list.GetData(entityId);
        }

        public void SetTreePriority(ulong tree, ulong priority)
        {
            Do(() =>
            {
                foreach (var list in _lists)
                {
                    if (list.HasTree(tree)) list.SetTreePriority(tree, priority);
                }
            });
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
            Do(() =>
            {
                ref var info = ref GetInfo(componentId);
                if (info.ParentId != null) throw new ArgumentException("Component already has a parent, cannot add to entity");

                ref var entityInfo = ref GetEntityInfo(entityId);

                var entityData = GetList<EntityData>().GetData(entityId);
                entityData.Children.Add(componentId);

                info.ParentId = entityId;
                info.Tree = entityInfo.Tree;
                info.TreeDepth = info.IsEntity ? (byte)(entityInfo.TreeDepth + 1) : entityInfo.TreeDepth;
                info.Branch = entityInfo.Branch;

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
                    GetList(componentId).Resort(componentId);
                    UpdateTree(componentId);
                }
                else GetList(componentId).Resort(componentId);
            });
        }

        internal void UpdateTree(ulong entityId)
        {
            var entityInfo = GetEntityInfo(entityId);
            var data = GetEntityData(entityInfo.Id);
            data.Branches.Clear();

            foreach (var id in data.Children)
            {
                ref var child = ref GetInfo(id);

                child.TreeDepth = child.IsEntity ? (byte)(entityInfo.TreeDepth + 1) : entityInfo.TreeDepth;
                child.Tree = entityInfo.Tree;
                child.Branch = entityInfo.Branch;

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

                    GetList(id).Resort(id);
                    UpdateTree(id);
                }
                else
                {
                    GetList(id).Resort(id);
                }
            }
        }

        internal void RemoveComponentFromEntity(ulong entityId, ulong componentId)
        {
            Do(() =>
            {
                var entityData = GetList<EntityData>().GetData(entityId);
                entityData.Children.Remove(componentId);
                entityData.Branches.Remove(componentId);

                ref var info = ref GetInfo(componentId);
                info.ParentId = null;
                info.Tree = info.Id;
                info.TreeDepth = 0;

                RemoveComponentTree(componentId);
            });
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
            Do(() =>
            {
                var list = GetList<T>();
                list.Add(info, component);
                _map[info.Id] = list;
            });
        }

        internal void RemoveComponent(ulong id)
        {
            Do(() =>
            {
                GetList(id).Remove(id);
                _map.Remove(id);
            });
        }

        internal void RemoveComponentTree(ulong id)
        {
            Do(() =>
            {
                foreach (var treeId in GetTree(id)) RemoveComponent(treeId);
            });
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

        protected void Do(Action action)
        {
            if (!Lock) action();
            else _deferred.Enqueue(action);
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


    public class EcsContext<TUpdateContext> : EcsContext
    {
        private List<SystemDef<TUpdateContext>> _systems = new();
        private protected SystemBuilder _builder;

        public EcsContext() => _builder = new SystemBuilder(this);

        private void AddSystem(Action<TUpdateContext> action, Action<TUpdateContext>? before, Action<TUpdateContext>? after) => _systems.Add(new(action, before, after));

        private void AddSystem(Action<TUpdateContext> action) => _systems.Add(new(action));

        public void AddSystem<T>(IComponentSpanSystem<TUpdateContext, T> system) =>
          AddSystem(_builder.MakeAction<TUpdateContext, T>(system.Process), system.BeforeProcess, system.AfterProcess);

        public void AddSystem<T>(IComponentSystem<TUpdateContext, T> system) =>
            AddSystem(_builder.MakeAction<TUpdateContext, T>(system.Process), system.BeforeProcess, system.AfterProcess);

        public void AddSystem<T>(IComponentParentSystem<TUpdateContext, T> system) =>
            AddSystem(_builder.MakeAction<TUpdateContext, T>(system.Process), system.BeforeProcess, system.AfterProcess);

        public void AddSystem<T1, T2>(IComponentParentSystem<TUpdateContext, T1, T2> system) =>
            AddSystem(_builder.MakeAction<TUpdateContext, T1, T2>(system.Process), system.BeforeProcess, system.AfterProcess);

        public void AddSystem<T1, T2>(IComponentSystem<TUpdateContext, T1, T2> system) =>
            AddSystem(_builder.MakeAction<TUpdateContext, T1, T2>(system.Process), system.BeforeProcess, system.AfterProcess);

        public void AddSystem<T>(SpanConsumer<TUpdateContext, T> method) => AddSystem(_builder.MakeAction(method));

        public void AddSystem<T>(ComponentAction<TUpdateContext, T> method) => AddSystem(_builder.MakeAction(method));

        public void AddSystem<T1, T2>(ComponentAction<TUpdateContext, T1, T2> method) => AddSystem(_builder.MakeAction(method));

        public void AddSystem<T>(ParentAction<TUpdateContext, T> method) => AddSystem(_builder.MakeAction(method));

        public void Update(TUpdateContext context)
        {
            while (_deferred.TryDequeue(out var action)) action?.Invoke();
            Lock = true;
            foreach (var system in _systems) system.Process(context);
            Lock = false;
        }
    }


    public class EcsContext<TUpdateContext, TRenderContext> : EcsContext<TUpdateContext>
    {
        private List<SystemDef<TRenderContext>> _renderSystems = new();


        private void AddSystem(Action<TRenderContext> action, Action<TRenderContext>? before, Action<TRenderContext>? after) => _renderSystems.Add(new(action, before, after));

        private void AddSystem(Action<TRenderContext> action) => _renderSystems.Add(new(action));


        public void AddRenderSystem<T>(IComponentSpanSystem<TRenderContext, T> system) =>
          AddSystem(_builder.MakeAction<TRenderContext, T>(system.Process), system.BeforeProcess, system.AfterProcess);

        public void AddRenderSystem<T>(IComponentSystem<TRenderContext, T> system) =>
            AddSystem(_builder.MakeAction<TRenderContext, T>(system.Process), system.BeforeProcess, system.AfterProcess);

        public void AddRenderSystem<T>(IComponentParentSystem<TRenderContext, T> system) =>
            AddSystem(_builder.MakeAction<TRenderContext, T>(system.Process), system.BeforeProcess, system.AfterProcess);

        public void AddRenderSystem<T1, T2>(IComponentSystem<TRenderContext, T1, T2> system) =>
            AddSystem(_builder.MakeAction<TRenderContext, T1, T2>(system.Process), system.BeforeProcess, system.AfterProcess);

        public void AddRenderSystem<T>(SpanConsumer<TRenderContext, T> method) => AddSystem(_builder.MakeAction(method));

        public void AddRenderSystem<T>(ComponentAction<TRenderContext, T> method) => AddSystem(_builder.MakeAction(method));

        public void AddRenderSystem<T1, T2>(ComponentAction<TRenderContext, T1, T2> method) => AddSystem(_builder.MakeAction(method));

        public void AddRenderSystem<T>(ParentAction<TRenderContext, T> method) => AddSystem(_builder.MakeAction(method));


        public void Render(TRenderContext context)
        {
            foreach (var system in _renderSystems) system.Process(context);
        }
    }
}