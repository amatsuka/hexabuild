using Game.Economy;
using Game.Storage;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class StorageGridTests
    {
        static void Fill(StorageGrid storage, ResourceType type, int count)
        {
            for (var i = 0; i < count; i++)
                storage.TryStore(type);
        }

        [Test]
        public void TryStore_PutsResourceIntoFirstFreeCell()
        {
            var storage = new StorageGrid(25);

            storage.TryStore(ResourceType.Wood);
            storage.TryStore(ResourceType.Stone);

            Assert.AreEqual(ResourceType.Wood, storage[0]);
            Assert.AreEqual(ResourceType.Stone, storage[1]);
            Assert.AreEqual(2, storage.Count);
        }

        [Test]
        public void TryStore_ReusesCellFreedByRemoval()
        {
            var storage = new StorageGrid(25);
            storage.TryStore(ResourceType.Wood);
            storage.TryStore(ResourceType.Stone);
            storage.TryRemove(ResourceType.Wood, 1);

            storage.TryStore(ResourceType.Ore);

            Assert.AreEqual(ResourceType.Ore, storage[0]);
        }

        [Test]
        public void TryStore_OnFullStorage_DestroysResourceAndCountsTheLoss()
        {
            var storage = new StorageGrid(2);
            storage.TryStore(ResourceType.Wood);
            storage.TryStore(ResourceType.Wood);

            var lost = 0;
            storage.ResourceLost += _ => lost++;

            Assert.IsFalse(storage.TryStore(ResourceType.Ore));
            Assert.AreEqual(2, storage.Count);
            Assert.AreEqual(1, storage.LostCount);
            Assert.AreEqual(1, lost);
        }

        [Test]
        public void CountOf_CountsOnlyRequestedType()
        {
            var storage = new StorageGrid(25);
            storage.TryStore(ResourceType.Gravel);
            storage.TryStore(ResourceType.Gravel);
            storage.TryStore(ResourceType.Wood);

            Assert.AreEqual(2, storage.CountOf(ResourceType.Gravel));
            Assert.AreEqual(1, storage.CountOf(ResourceType.Wood));
            Assert.AreEqual(0, storage.CountOf(ResourceType.Ingot));
        }

        [Test]
        public void TryRemove_WithoutEnoughResources_ChangesNothing()
        {
            var storage = new StorageGrid(25);
            storage.TryStore(ResourceType.Gravel);

            Assert.IsFalse(storage.TryRemove(ResourceType.Gravel, 2));
            Assert.AreEqual(1, storage.CountOf(ResourceType.Gravel));
        }

        [Test]
        public void TryRemove_TakesRequestedAmountFromEarliestCells()
        {
            var storage = new StorageGrid(25);
            for (var i = 0; i < 3; i++)
                storage.TryStore(ResourceType.Wood);

            Assert.IsTrue(storage.TryRemove(ResourceType.Wood, 2));
            Assert.AreEqual(1, storage.Count);
            Assert.IsFalse(storage[0].HasValue);
            Assert.IsFalse(storage[1].HasValue);
            Assert.AreEqual(ResourceType.Wood, storage[2]);
        }

        [Test]
        public void TryStore_ReportsTheCellItUsed()
        {
            var storage = new StorageGrid(25);
            storage.TryStore(ResourceType.Wood, out var first);
            storage.TryStore(ResourceType.Stone, out var second);

            Assert.AreEqual(0, first);
            Assert.AreEqual(1, second);
        }

        [Test]
        public void TryStore_OnFullStorage_ReportsNoCell()
        {
            var storage = new StorageGrid(1);
            storage.TryStore(ResourceType.Wood);

            Assert.IsFalse(storage.TryStore(ResourceType.Wood, out var index));
            Assert.AreEqual(-1, index);
        }

        [Test]
        public void TryRemoveAt_EmptiesExactlyThatCell()
        {
            var storage = new StorageGrid(25);
            Fill(storage, ResourceType.Wood, 3);

            Assert.IsTrue(storage.TryRemoveAt(1));

            Assert.AreEqual(ResourceType.Wood, storage[0]);
            Assert.IsFalse(storage[1].HasValue);
            Assert.AreEqual(ResourceType.Wood, storage[2]);
            Assert.AreEqual(2, storage.Count);
        }

        [Test]
        public void TryRemoveAt_EmptyOrOutsideCell_ReturnsFalse()
        {
            var storage = new StorageGrid(25);

            Assert.IsFalse(storage.TryRemoveAt(0));
            Assert.IsFalse(storage.TryRemoveAt(-1));
            Assert.IsFalse(storage.TryRemoveAt(99));
        }

        [Test]
        public void Changed_FiresOnStoreRemoveAndLoss()
        {
            var storage = new StorageGrid(1);
            var changes = 0;
            storage.Changed += () => changes++;

            storage.TryStore(ResourceType.Wood);
            storage.TryStore(ResourceType.Wood);
            storage.TryRemove(ResourceType.Wood, 1);

            Assert.AreEqual(3, changes);
        }
    }
}
