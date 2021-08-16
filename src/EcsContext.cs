using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

[assembly: InternalsVisibleTo("Necs.Debug"), InternalsVisibleTo("Necs.Benchmark"), InternalsVisibleTo("Necs.Tests")]

namespace Necs
{
    public interface IComponentSystem<TContext>
    {
        void BeforeProcess(TContext context) { }
        void Process(TContext context, IEcsContext ecs);
        void AfterProcess(TContext context) { }
    }

    public delegate void ComponentSystemAction<TContext>(TContext context, IEcsContext ecs);

    public partial class EcsContext
    {
        protected bool Lock = false;

        private Dictionary<ulong, IComponentList> _map = new();
        protected Queue<Action> _deferred = new();
        protected List<IComponentList> _lists = new();

        internal EcsContext() { }

        // Public methods

        public void AddEntity(Entity entity)
        {
            Do(() =>
            {
                entity.Context.CopyTo(this);
                entity.SetContext(this);
            });
        }

        public void RemoveEntity(Entity entity)
        {
            Do(() =>
            {
                var targetIds = new HashSet<ulong>(GetTree(entity.Id));
                var newCtx = new EcsContext();
                CopyTo(newCtx, targetIds);
                entity.SetContext(newCtx);
                RemoveComponentTree(entity.Id);
            });
        }

        public void SetTreePriority(ulong tree, ulong priority)
        {
            Do(() =>
            {
                foreach (var list in _lists)
                {
                    if (list.HasTree(tree)) list.ResortTree(tree, priority);
                }
                ComponentInfo.SetTreePriority(tree, priority);
            });
        }

        public int GetCount<T>() => GetList<T>().Count;

        // Internal methods
        private void AddList(IComponentList list) => _lists.Add(list);

        internal void CopyTo(EcsContext target, HashSet<ulong>? filter = null)
        {
            foreach (var list in _lists) target.CopyFromList(list, filter);
        }

        internal ref ComponentInfo GetEntityInfo(ulong entityId)
        {
            var list = GetList<EntityData>();
            return ref list.GetInfo(entityId);
        }

        internal EntityData GetEntityData(ulong entityId)
        {
            var list = GetList<EntityData>();
            return list.GetData(entityId);
        }

        internal void CopyFromList(IComponentList src, HashSet<ulong>? filter = null)
        {
            foreach (var list in _lists)
            {
                if (list.Type == src.Type)
                {
                    src.CopyTo(list, filter);
                    UpdateMapping(src.Infos, list);
                    return;
                }
            }
            var newList = (IComponentList)Activator.CreateInstance(src.GetType())!;
            src.CopyTo(newList, filter);
            _lists.Add(newList);
            UpdateMapping(newList.Infos, newList);
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
        private List<Action<TUpdateContext>> _systems = new();

        private void AddSystem(Action<TUpdateContext> action) => _systems.Add(action);

        public void AddSystem(IComponentSystem<TUpdateContext> system)
        {
            var action = new Action<TUpdateContext>(ctx =>
            {
                system.BeforeProcess(ctx);
                system.Process(ctx, this);
                system.AfterProcess(ctx);
            });
            AddSystem(action);
        }

        public void AddSystem(ComponentSystemAction<TUpdateContext> action) => AddSystem(new Action<TUpdateContext>(ctx => action.Invoke(ctx, this)));

        public void Update(TUpdateContext context)
        {
            while (_deferred.TryDequeue(out var action)) action?.Invoke();
            Lock = true;
            foreach (var system in _systems) system.Invoke(context);
            Lock = false;
        }
    }


    public class EcsContext<TUpdateContext, TRenderContext> : EcsContext<TUpdateContext>
    {
        private List<Action<TRenderContext>> _renderSystems = new();

        private void AddRenderSystem(Action<TRenderContext> action) => _renderSystems.Add(action);

        public void AddRenderSystem(IComponentSystem<TRenderContext> system)
        {
            var action = new Action<TRenderContext>(ctx =>
            {
                system.BeforeProcess(ctx);
                system.Process(ctx, this);
                system.AfterProcess(ctx);
            });
            AddRenderSystem(action);
        }

        public void AddRenderSystem(ComponentSystemAction<TRenderContext> action) => AddRenderSystem(new Action<TRenderContext>(ctx => action.Invoke(ctx, this)));

        public void Render(TRenderContext context)
        {
            foreach (var system in _renderSystems) system.Invoke(context);
        }
    }
}