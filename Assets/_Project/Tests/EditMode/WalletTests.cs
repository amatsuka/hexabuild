using Game.Economy;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class WalletTests
    {
        [Test]
        public void StartsWithConfiguredPoints()
        {
            Assert.AreEqual(40, new Wallet(40).Points);
        }

        [Test]
        public void TrySpendPoints_WithEnoughPoints_SpendsAndReportsSuccess()
        {
            var wallet = new Wallet(40);

            Assert.IsTrue(wallet.TrySpendPoints(20));
            Assert.AreEqual(20, wallet.Points);
        }

        [Test]
        public void TrySpendPoints_WithoutEnoughPoints_LeavesWalletUntouched()
        {
            var wallet = new Wallet(19);

            Assert.IsFalse(wallet.TrySpendPoints(20));
            Assert.AreEqual(19, wallet.Points);
        }

        [Test]
        public void Changed_FiresOnlyOnSuccessfulOperations()
        {
            var wallet = new Wallet(40);
            var changes = 0;
            wallet.Changed += () => changes++;

            wallet.AddPoints(10);
            wallet.TrySpendPoints(5);
            wallet.TrySpendPoints(1000);

            Assert.AreEqual(2, changes);
        }
    }
}
