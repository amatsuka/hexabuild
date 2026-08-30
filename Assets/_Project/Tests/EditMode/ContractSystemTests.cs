using System.Collections.Generic;
using Game.Economy;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class ContractSystemTests
    {
        const int Goal = 2;
        const float Seconds = 10f;
        const int Reward = 40;

        static readonly ResourceType[] OneType = { ResourceType.Board };

        Wallet wallet;
        ContractSystem contracts;
        List<string> log;

        [SetUp]
        public void SetUp()
        {
            wallet = new Wallet(0);
            contracts = new ContractSystem(wallet, OneType, Goal, Seconds, Reward, seed: 1);
            log = new List<string>();
            contracts.Issued += () => log.Add("issued");
            contracts.Progressed += () => log.Add("progressed");
            contracts.Completed += reward => log.Add($"completed {reward}");
            contracts.Failed += () => log.Add("failed");
        }

        [Test]
        public void Issue_StartsTheContractWithAFullTimer()
        {
            contracts.Issue();

            Assert.IsTrue(contracts.IsActive);
            Assert.AreEqual(ResourceType.Board, contracts.Type);
            Assert.AreEqual(0, contracts.Delivered);
            Assert.AreEqual(Seconds, contracts.SecondsLeft);
            CollectionAssert.AreEqual(new[] { "issued" }, log);
        }

        [Test]
        public void Count_BeforeTheFirstContract_ChangesNothing()
        {
            contracts.Count(ResourceType.Board);

            Assert.AreEqual(0, contracts.Delivered);
            CollectionAssert.IsEmpty(log);
        }

        [Test]
        public void Count_OfTheAskedType_CompletesTheContractAndPaysTheReward()
        {
            contracts.Issue();

            contracts.Count(ResourceType.Board);
            Assert.AreEqual(1, contracts.Delivered);
            Assert.AreEqual(0, wallet.Points, "награда приходит только за весь контракт");

            contracts.Count(ResourceType.Board);

            Assert.AreEqual(Reward, wallet.Points);
            Assert.AreEqual(Reward, wallet.TotalEarned, "награда идёт и в финальный счёт");
            CollectionAssert.Contains(log, $"completed {Reward}");
        }

        [Test]
        public void Count_OfAnotherType_IsIgnored()
        {
            contracts.Issue();

            contracts.Count(ResourceType.Ingot);

            Assert.AreEqual(0, contracts.Delivered);
            Assert.AreEqual(0, wallet.Points);
            CollectionAssert.DoesNotContain(log, "progressed");
        }

        [Test]
        public void CompletedContract_IsFollowedByAFreshOne()
        {
            contracts.Issue();
            contracts.Count(ResourceType.Board);
            contracts.Tick(4f);

            contracts.Count(ResourceType.Board);

            Assert.IsTrue(contracts.IsActive, "активный контракт всегда один, и он есть");
            Assert.AreEqual(0, contracts.Delivered);
            Assert.AreEqual(Seconds, contracts.SecondsLeft, "таймер нового контракта полный");
        }

        [Test]
        public void Tick_PastTheDeadline_FailsWithoutTakingAnything()
        {
            contracts.Issue();
            contracts.Count(ResourceType.Board);

            contracts.Tick(Seconds + 0.1f);

            Assert.AreEqual(0, wallet.Points, "провал ничем не штрафует");
            CollectionAssert.Contains(log, "failed");
            Assert.IsTrue(contracts.IsActive, "следом выдан новый контракт");
            Assert.AreEqual(0, contracts.Delivered);
        }

        [Test]
        public void Tick_BeforeTheDeadline_KeepsTheContractRunning()
        {
            contracts.Issue();

            contracts.Tick(Seconds - 0.5f);

            Assert.IsTrue(contracts.IsActive);
            Assert.AreEqual(0.5f, contracts.SecondsLeft, 1e-4f);
            CollectionAssert.DoesNotContain(log, "failed");
        }

        [Test]
        public void Type_IsAlwaysOneOfTheCraftedTypes()
        {
            var types = new[] { ResourceType.Board, ResourceType.Gravel, ResourceType.Ingot };
            var system = new ContractSystem(wallet, types, Goal, Seconds, Reward, seed: 7);

            for (var i = 0; i < 50; i++)
            {
                system.Issue();
                CollectionAssert.Contains(types, system.Type);
            }
        }
    }
}
