using Game.Economy;
using Game.Merge;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class MergeRulesTests
    {
        MergeRules rules;

        [SetUp]
        public void SetUp() => rules = ScriptableObject.CreateInstance<MergeRules>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(rules);

        [Test]
        public void FiveResources_GiveTwoCraftedAndTwentyFivePoints()
        {
            Assert.IsTrue(rules.TryResolve(ResourceType.Wood, 5, out var outcome));

            Assert.AreEqual(5, outcome.Consumed);
            Assert.AreEqual(2, outcome.Produced);
            Assert.AreEqual(25, outcome.Points);
        }

        [Test]
        public void MoreThanFive_StillMergesExactlyFive()
        {
            rules.TryResolve(ResourceType.Wood, 10, out var outcome);

            Assert.AreEqual(5, outcome.Consumed);
            Assert.AreEqual(2, outcome.Produced);
            Assert.AreEqual(25, outcome.Points);
        }

        [TestCase(3)]
        [TestCase(4)]
        public void ThreeOrFour_GiveOneCraftedAndTenPoints(int available)
        {
            Assert.IsTrue(rules.TryResolve(ResourceType.Stone, available, out var outcome));

            Assert.AreEqual(3, outcome.Consumed);
            Assert.AreEqual(1, outcome.Produced);
            Assert.AreEqual(10, outcome.Points);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void FewerThanThree_DoNotMerge(int available)
        {
            Assert.IsFalse(rules.TryResolve(ResourceType.Ore, available, out _));
        }

        [TestCase(ResourceType.Wood, ResourceType.Board)]
        [TestCase(ResourceType.Stone, ResourceType.Gravel)]
        [TestCase(ResourceType.Ore, ResourceType.Ingot)]
        public void Recipes_FollowTheSpec(ResourceType source, ResourceType expected)
        {
            rules.TryResolve(source, 5, out var outcome);

            Assert.AreEqual(source, outcome.Source);
            Assert.AreEqual(expected, outcome.Result);
        }

        [TestCase(ResourceType.Board)]
        [TestCase(ResourceType.Gravel)]
        [TestCase(ResourceType.Ingot)]
        public void CraftedResources_NeverMerge(ResourceType crafted)
        {
            Assert.IsFalse(rules.CanMerge(crafted));
            Assert.IsFalse(rules.TryResolve(crafted, 10, out _));
        }

        [Test]
        public void BaseResources_CanMerge()
        {
            Assert.IsTrue(rules.CanMerge(ResourceType.Wood));
            Assert.IsTrue(rules.CanMerge(ResourceType.Stone));
            Assert.IsTrue(rules.CanMerge(ResourceType.Ore));
        }
    }
}
