using System;
using System.Collections.Generic;
using System.Linq;
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

            list.GetInfo(i1).Tree = 998;
            list.Resort(i1);

            list.GetInfo(i2).Tree = 999;
            list.Resort(i2);

            list.GetInfo(i3).Tree = 1000;
            list.Resort(i3);

            Assert.Equal(i1, infos[97].Id);
            Assert.Equal(i2, infos[98].Id);
            Assert.Equal(i3, infos[99].Id);
            Assert.Equal(n0, infos[0].Id);
        }

        [Fact]
        public void Test_TreePriority()
        {
            var list = CreateList();
            var infos = new List<ComponentInfo>();

            int n = 3;
            int size = 5;

            for (int i = 0; i < n; i++) infos.AddRange(CreateInfosWithTree(size, (ulong)i));
            foreach (var info in infos) list.Add(info, new());

            infos.Reverse();

            for (int i = 0; i < n; i++) list.SetTreePriority((ulong)i, (ulong)(n - i));

            for (int i = 0; i < n * size; i++)
            {
                Assert.Equal(infos[i].Tree, list.Infos[i].Tree);
            }

            for (int i = 0; i < n; i++) list.SetTreePriority((ulong)i, (ulong)(n - i) * 10);

            for (int i = 0; i < n * size; i++)
            {
                Assert.Equal(infos[i].Tree, list.Infos[i].Tree);
            }
        }

        [Fact]
        public void Test_OrderedByTreeStable()
        {
            var list = CreateList();

            for (int i = 0; i < 3; i++)
            {
                var info = AddInfoToList(list);
                list.SetTreePriority(info.Tree, 0);
            }

            for (int i = 1; i < 3; i++) Assert.True(list.Infos[i - 1].Tree < list.Infos[i].Tree);
        }

        private ComponentList<Empty> CreateList() => new();

        private ComponentInfo AddInfoToList(ComponentList<Empty> list)
        {
            var info = ComponentInfo.Create();
            list.Add(info, new Empty());
            return info;
        }

        private List<ComponentInfo> CreateInfosWithTree(int count, ulong tree)
        {
            var list = new List<ComponentInfo>();
            for (int i = 0; i < count; i++)
            {
                var info = ComponentInfo.Create();
                info.Tree = tree;
                list.Add(info);
            }
            return list;
        }

        private void AddInfosToList(ComponentList<Empty> list, int count)
        {
            for (int i = 0; i < count; i++) AddInfoToList(list);
        }
    }
}
