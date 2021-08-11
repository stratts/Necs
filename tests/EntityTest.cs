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