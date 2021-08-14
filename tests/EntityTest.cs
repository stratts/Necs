using System;
using System.Collections.Generic;
using Xunit;
using Necs;

namespace Necs.Tests
{
    public class EntityTest
    {
        [Fact]
        public void Test_CreateEntity()
        {
            var entity = new Entity();
            Assert.True(entity.Info.IsEntity);
        }

        [Fact]
        public void Test_AddChain()
        {
            var ctx = new EcsContext();

            var e1 = new Entity();
            var e2 = new Entity();
            var e3 = new Entity();
            var e4 = new Entity();
            var e5 = new Entity();

            e2.AddChild(e1);
            e3.AddChild(e2);
            e4.AddChild(e3);
            e5.AddChild(e4);

            ctx.AddEntity(e1);

            var eList = ctx.GetList<EntityData>();
            Assert.Equal(5, eList.Count);
        }

        [Fact]
        public void Test_AddMultiple()
        {
            var e1 = new Entity();

            e1.AddChild(new Entity());
            e1.AddChild(new Entity());
            e1.AddChild(new Entity());
            e1.AddChild(new Entity());
            e1.AddChild(new Entity());
        }

        [Fact]
        public void Test_AddMultipleNested()
        {
            var e1 = new Entity();

            e1.AddChild(new Entity());
            e1.AddChild(new Entity());
            e1.AddChild(new Entity());
            e1.AddChild(new Entity());
            e1.AddChild(new Entity());

            var e2 = new Entity();
            e2.AddChild(e1);
            var e3 = new Entity();
            e3.AddChild(e2);
        }

        [Fact]
        public void Test_ComplexEntity()
        {
            var e1 = new Entity();

            var e2 = CreateEntityWithChildren();
            var e3 = CreateEntityWithChildren();
            e2.AddChild(e3);
            var e4 = CreateEntityWithChildren();
            e3.AddChild(e4);

            var e5 = CreateEntityWithChildren();
            var e6 = CreateEntityWithChildren();
            var e7 = CreateEntityWithChildren();
            e5.AddChild(e6);
            e6.AddChild(e7);

            e1.AddChild(e2);
            e1.AddChild(e5);

        }

        [Fact]
        public void Test_AddNested()
        {
            var c = new Entity();
            var d = new Entity();
            var e = new Entity();

            c.AddChild(d);
            e.AddChild(c);

            Assert.Equal(e.Info.Tree, d.Info.Tree);
        }

        [Fact]
        public void Test_TreeOrder()
        {
            var e1 = new Entity();

            var c1 = new Entity();

            var d1 = new Entity();
            c1.AddChild(d1);

            var d2 = new Entity();
            c1.AddChild(d2);

            e1.AddChild(c1);

            var infos = e1.Context.GetList<EntityData>().Infos;

            Assert.Equal(e1.Id, infos[0].Id);
            Assert.Equal(c1.Id, infos[1].Id);
            Assert.Equal(d1.Id, infos[2].Id);
            Assert.Equal(d2.Id, infos[3].Id);
        }

        [Fact]
        public void Test_TreeSet()
        {
            var a = new Entity();
            var b = new Entity();
            var c = new Entity();
            var d = new Entity();
            var e = new Entity();

            a.AddChild(b);
            Assert.Equal(a.Info.Tree, b.Info.Tree);

            c.AddChild(d);
            Assert.Equal(c.Info.Tree, d.Info.Tree);
            Assert.Equal(1, c.Data.Children.Count);

            e.AddChild(c);
            Assert.Equal(1, e.Data.Children.Count);
            Assert.Equal(e.Context, c.Context);
            Assert.Equal(e.Context, d.Context);
            Assert.Equal(e.Info.Tree, c.Info.Tree);
            Assert.Equal(e.Info.Tree, d.Info.Tree);

            b.AddChild(e);

            Assert.Equal(a.Info.Tree, b.Info.Tree);
            Assert.Equal(a.Info.Tree, c.Info.Tree);
            Assert.Equal(a.Info.Tree, d.Info.Tree);
            Assert.Equal(a.Info.Tree, e.Info.Tree);
        }

        [Fact]
        public void Test_Descendents()
        {
            var e1 = new Entity();

            var c1 = new Entity();

            var d1 = new Entity();
            c1.AddChild(d1);

            var d2 = new Entity();
            c1.AddChild(d2);

            e1.AddChild(c1);

            Assert.True(d2.Info.IsDescendantOf(ref c1.Info));
            Assert.True(d2.Info.IsDescendantOf(ref e1.Info));
            Assert.True(d1.Info.IsDescendantOf(ref c1.Info));
            Assert.True(d1.Info.IsDescendantOf(ref e1.Info));
            Assert.True(c1.Info.IsDescendantOf(ref e1.Info));

            Assert.False(d2.Info.IsDescendantOf(ref d1.Info));
            Assert.False(d1.Info.IsDescendantOf(ref d2.Info));
            Assert.False(c1.Info.IsDescendantOf(ref d1.Info));
            Assert.False(c1.Info.IsDescendantOf(ref d2.Info));
            Assert.False(e1.Info.IsDescendantOf(ref c1.Info));
        }

        private Entity CreateEntityWithChildren()
        {
            var e1 = new Entity();
            e1.AddChild(new Entity());
            e1.AddChild(new Entity());
            e1.AddChild(new Entity());
            e1.AddChild(new Entity());
            e1.AddChild(new Entity());
            return e1;
        }
    }
}