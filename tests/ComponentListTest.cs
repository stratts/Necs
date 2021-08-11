using System;
using System.Collections.Generic;
using Xunit;
using Necs;

namespace Necs.Tests
{
    public class ComponentListTest
    {
        [Fact]
        public void Test_Add()
        {
            var list = CreateList();
            var info = AddInfoToList(list);

            Assert.Equal(1, list.Count);
            Assert.Equal(1, list.Infos.Length);
            Assert.Equal(1, list.Data.Length);
            Assert.Equal(info, list.Infos[0]);
        }

        [Fact]
        public void Test_Remove()
        {
            var list = CreateList();
            var info = AddInfoToList(list);

            list.Remove(info.Id);
            Assert.Equal(0, list.Count);
            Assert.Equal(0, list.Infos.Length);
            Assert.Equal(0, list.Data.Length);
        }

        [Fact]
        public void Test_AddMultiple()
        {
            var list = CreateList();
            for (int i = 0; i < 10; i++)
            {
                var info = AddInfoToList(list);
                Assert.Equal(i + 1, list.Count);
                Assert.Equal(i + 1, list.Infos.Length);
                Assert.Equal(i + 1, list.Data.Length);
                Assert.Equal(info, list.Infos[i]);
            }
        }

        [Fact]
        public void Test_RemoveMultiple()
        {
            var infos = new List<ComponentInfo>();
            var list = CreateList();
            for (int i = 0; i < 10; i++)
            {
                var info = AddInfoToList(list);
                infos.Add(info);
            }

            for (int i = 10 - 1; i >= 0; i -= 2)
            {
                list.Remove(infos[i].Id);
                infos.RemoveAt(i);
            }

            Assert.Equal(5, list.Count);
            Assert.Equal(5, list.Infos.Length);
            Assert.Equal(5, list.Data.Length);

            for (int i = 0; i < infos.Count; i++) Assert.Equal(infos[i], list.Infos[i]);
        }

        [Fact]
        public void Test_InsertSort()
        {
            int n = 10;
            var infos = new List<ComponentInfo>();
            var list = CreateList();

            for (int i = 0; i < n; i++)
            {
                var info = ComponentInfo.Create();
                info.Tree = (ulong)(n - i);
                infos.Add(info);
                list.Add(info, new());
            }

            for (int i = 0; i < n; i++) Assert.Equal(infos[n - i - 1], list.Infos[i]);
        }

        [Fact]
        public void Test_GetById()
        {
            var list = CreateList();
            AddInfosToList(list, 20);
            var info = list.Infos[15];

            Assert.Equal(info, list.GetInfo(info.Id));
        }

        [Fact]
        public void Test_GetByParent()
        {
            int n = 10;
            var infos = new List<ComponentInfo>();
            var list = CreateList();

            for (int i = 0; i < n; i++)
            {
                var info = ComponentInfo.Create();
                info.ParentId = (ulong)(i + n);
                infos.Add(info);
                list.Add(info, new());
            }

            foreach (var info in infos)
            {
                Assert.NotNull(info.ParentId);
                var idx = list.GetByParent(info.ParentId.Value);
                Assert.NotNull(idx);
                Assert.Equal(info, list.Infos[idx.Value]);
            }
        }

        [Fact]
        public void Test_Resort()
        {
            var list = CreateList();
            AddInfosToList(list, 100);
            var infos = list.Infos;

            var i1 = infos[0].Id;
            var i2 = infos[1].Id;
            var i3 = infos[2].Id;
            var n0 = infos[3].Id;

            infos[0].Tree = 998;
            infos[1].Tree = 999;
            infos[2].Tree = 1000;

            list.Resort(i1);
            list.Resort(i2);
            list.Resort(i3);

            Assert.Equal(i1, infos[97].Id);
            Assert.Equal(i2, infos[98].Id);
            Assert.Equal(i3, infos[99].Id);
            Assert.Equal(n0, infos[0].Id);
        }

        private ComponentList<Empty> CreateList() => new();

        private ComponentInfo AddInfoToList(ComponentList<Empty> list)
        {
            var info = ComponentInfo.Create();
            list.Add(info, new Empty());
            return info;
        }

        private void AddInfosToList(ComponentList<Empty> list, int count)
        {
            for (int i = 0; i < count; i++) AddInfoToList(list);
        }
    }
}
