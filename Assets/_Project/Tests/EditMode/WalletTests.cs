using Game.Economy;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class WalletTests
    {
        [Test]
        public void StartsWithConfiguredPointsAndGravel()
        {
            var wallet = new Wallet(40, 3);

            Assert.AreEqual(40, wallet.Points);
            Assert.AreEqual(3, wallet.GetMaterial(ResourceType.Gravel));
            Assert.AreEqual(0, wallet.GetMaterial(ResourceType.Wood));
        }

        [Test]
        public void TrySpendPoints_WithEnoughPoints_SpendsAndReportsSuccess()
        {
            var wallet = new Wallet(40, 3);

            Assert.IsTrue(wallet.TrySpendPoints(20));
            Assert.AreEqual(20, wallet.Points);
        }

        [Test]
        public void TrySpendPoints_WithoutEnoughPoints_LeavesWalletUntouched()
        {
            var wallet = new Wallet(19, 0);

            Assert.IsFalse(wallet.TrySpendPoints(20));
            Assert.AreEqual(19, wallet.Points);
        }

        [Test]
        public void TrySpendMaterial_ChecksExactType()
        {
            var wallet = new Wallet(0, 3);

            Assert.IsFalse(wallet.TrySpendMaterial(ResourceType.Wood, 1));
            Assert.IsTrue(wallet.TrySpendMaterial(ResourceType.Gravel, 3));
            Assert.AreEqual(0, wallet.GetMaterial(ResourceType.Gravel));
        }

        [Test]
        public void Changed_FiresOnEverySuccessfulOperation()
        {
            var wallet = new Wallet(40, 0);
            var changes = 0;
            wallet.Changed += () => changes++;

            wallet.AddPoints(10);
            wallet.TrySpendPoints(5);
            wallet.AddMaterial(ResourceType.Gravel, 2);
            wallet.TrySpendMaterial(ResourceType.Gravel, 1);
            wallet.TrySpendPoints(1000);

            Assert.AreEqual(4, changes);
        }
    }
}
