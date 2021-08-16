using System;

namespace Necs
{
    public delegate void ComponentAction<TUpdateContext, T>(TUpdateContext context, ref T a);

    public delegate void ComponentAction<TUpdateContext, T1, T2>(TUpdateContext context, ref T1 a, ref T2 b);

    public delegate void EntityAction<TUpdateContext, T>(TUpdateContext context, ComponentInfo entity, ref T a);

    public delegate void ParentAction<TUpdateContext, T>(TUpdateContext context, ref T a, ref T? parent, bool hasParent);

    public delegate void ParentAction<TUpdateContext, T1, T2>(TUpdateContext context, ref T1 a, ref T2 b, ref T2? parent, bool hasParent);

    public delegate void SpanConsumer<TUpdateContext, T>(TUpdateContext context, Span<T> components);

    public interface IComponentSystem<TUpdateContext>
    {
        void BeforeProcess(TUpdateContext context) { }
        void AfterProcess(TUpdateContext context) { }
    }

    public interface IComponentSystem<TUpdateContext, T> : IComponentSystem<TUpdateContext>
    {
        void Process(TUpdateContext context, ref T component);
    }

    public interface IComponentSpanSystem<TUpdateContext, T> : IComponentSystem<TUpdateContext>
    {
        void Process(TUpdateContext context, Span<T> components);
    }

    public interface IComponentParentSystem<TUpdateContext, T> : IComponentSystem<TUpdateContext>
    {
        void Process(TUpdateContext context, ref T a, ref T? parent, bool hasParent);
    }

    public interface IComponentParentSystem<TUpdateContext, T1, T2> : IComponentSystem<TUpdateContext>
    {
        void Process(TUpdateContext context, ref T1 a, ref T2 b, ref T2? parent, bool hasParent);
    }

    public interface IComponentSystem<TUpdateContext, T1, T2> : IComponentSystem<TUpdateContext>
    {
        void Process(TUpdateContext context, ref T1 a, ref T2 b);
    }

    internal class SystemDef<TContext>
    {
        private Action<TContext>? _beforeProcess;
        private Action<TContext> _process;
        private Action<TContext>? _afterProcess;

        public SystemDef(Action<TContext> process, Action<TContext>? before = null, Action<TContext>? after = null)
        {
            _beforeProcess = before;
            _process = process;
            _afterProcess = after;
        }

        public void Process(TContext context)
        {
            _beforeProcess?.Invoke(context);
            _process.Invoke(context);
            _afterProcess?.Invoke(context);
        }
    }

    internal class SystemBuilder
    {
        private EcsContext _context;

        public SystemBuilder(EcsContext context) => _context = context;

        private ComponentList<T> GetList<T>() => _context.GetList<T>();

        public Action<TContext> MakeAction<TContext, T>(SpanConsumer<TContext, T> method)
        {
            return new(ctx =>
            {
                var components = GetList<T>();
                method?.Invoke(ctx, components.Data);
            });
        }

        public Action<TContext> MakeAction<TContext, T>(ComponentAction<TContext, T> method)
        {
            return new(ctx =>
            {
                var components = GetList<T>();

                foreach (ref var c in components.Data)
                {
                    method?.Invoke(ctx, ref c);
                }
            });
        }

        public Action<TContext> MakeAction<TContext, T1, T2>(ComponentAction<TContext, T1, T2> method)
        {
            return new(ctx =>
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
                        else if (tree2 > tree)
                        {
                            offset = j;
                            break;
                        }
                    }
                }
            });
        }

        public Action<TContext> MakeAction<TContext, T>(EntityAction<TContext, T> method)
        {
            return new(ctx =>
            {
                var list1 = GetList<EntityData>();
                var list2 = GetList<T>();

                var info1 = list1.Infos;
                var info2 = list2.Infos;

                var offset = 0;

                for (int i = 0; i < list1.Count; i++)
                {
                    var tree = info1[i].Tree;
                    var parent = info1[i].Id;

                    for (int j = offset; j < list2.Count; j++)
                    {
                        var tree2 = info2[j].Tree;
                        var parent2 = info2[j].ParentId;
                        if (parent2 == parent)
                        {
                            method.Invoke(ctx, info1[i], ref list2.Data[j]);
                            offset = j + 1;
                            break;
                        }
                        else if (tree2 > tree)
                        {
                            offset = j;
                            break;
                        }
                    }
                }
            });
        }

        public Action<TContext> MakeAction<TContext, T>(ParentAction<TContext, T> method)
        {
            return new(ctx =>
            {
                var list = GetList<T>();
                var infos = list.Infos;
                var data = list.Data;

                T? none = default;

                for (int i = 1; i < infos.Length; i++)
                {
                    ref var info = ref infos[i];
                    ref var d = ref data[i];

                    for (int j = i - 1; j >= 0; j--)
                    {
                        var prev = infos[j];
                        if (info.TreeDepth > prev.TreeDepth && info.IsDescendantOf(ref prev))
                        {
                            method.Invoke(ctx, ref d, ref data[j]!, true);
                            break;
                        }
                        else if (info.Tree != prev.Tree)
                        {
                            method.Invoke(ctx, ref d, ref none, false);
                            break;
                        }
                    }
                }
            });
        }

        public Action<TContext> MakeAction<TContext, T1, T2>(ParentAction<TContext, T1, T2> method)
        {
            return new(ctx =>
            {
                var list1 = GetList<T1>();
                var list2 = GetList<T2>();

                var info1 = list1.Infos;
                var info2 = list2.Infos;

                var offset = 0;

                T2? empty = default;

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
                            var desc = info2[j];
                            for (int k = j - 1; k >= 0; k--)
                            {
                                ref var prev = ref info2[k];
                                if (desc.IsDescendantOf(ref prev))
                                {
                                    method.Invoke(ctx, ref list1.Data[i], ref list2.Data[j], ref list2.Data[k]!, true);
                                    break;
                                }
                                else if (prev.Tree != desc.Tree)
                                {
                                    method.Invoke(ctx, ref list1.Data[i], ref list2.Data[j], ref empty, false);
                                    break;
                                }
                            }

                            offset = j + 1;
                            break;
                        }
                        else if (tree2 > tree)
                        {
                            offset = j;
                            break;
                        }
                    }
                }
            });
        }
    }
}