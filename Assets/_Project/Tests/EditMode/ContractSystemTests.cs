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
        const float MinPause = 5f;
        const float MaxPause = 20f;

        static readonly ResourceType[] OneType = { ResourceType.Board };

        Wallet wallet;
        ContractSystem contracts;
        List<string> log;

        [SetUp]
        public void SetUp()
        {
            wallet = new Wallet(0);
            contracts = new ContractSystem(wallet, OneType, Goal, Seconds, Reward, MinPause, MaxPause, seed: 1);
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

        /// <summary>Закрытые контракты считаются за партию: их показывает финальный экран.</summary>
        [Test]
        public void CompletedCount_CountsOnlyClosedContracts()
        {
            contracts.Issue();
            contracts.Count(ResourceType.Board);

            Assert.AreEqual(0, contracts.CompletedCount, "сданный ресурс — ещё не закрытый контракт");

            contracts.Count(ResourceType.Board);
            Assert.AreEqual(1, contracts.CompletedCount);

            // Следующий контракт приходит после паузы и проваливается по времени: провал
            // в счёт не идёт.
            contracts.Tick(MaxPause);
            contracts.Tick(Seconds);
            Assert.AreEqual(1, contracts.CompletedCount);

            contracts.Tick(MaxPause);
            contracts.Count(ResourceType.Board);
            contracts.Count(ResourceType.Board);
            Assert.AreEqual(2, contracts.CompletedCount);
        }

        [Test]
        public void Count_BeforeTheFirstContract_ChangesNothing()
        {
            contracts.Count(ResourceType.Board);

            Assert.AreEqual(0, contracts.Delivered);
            CollectionAssert.IsEmpty(log);
        }

        /// <summary>
        /// Сама по себе система партию не начинает: первый контракт выдаёт `Issue` (3.8).
        /// Иначе после конца партии тик продолжал бы плодить контракты в пустоту.
        /// </summary>
        [Test]
        public void Tick_BeforeTheFirstContract_IssuesNothing()
        {
            contracts.Tick(MaxPause * 2f);

            Assert.IsFalse(contracts.IsActive);
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

        /// <summary>
        /// Между контрактами Метрополия молчит: следующий приходит не в тот же кадр, а после
        /// случайной паузы. Сплошная очередь контрактов не оставляла игроку ни минуты, когда
        /// он играет в своё.
        /// </summary>
        [Test]
        public void CompletedContract_IsFollowedByAPauseAndThenAFreshOne()
        {
            contracts.Issue();
            contracts.Count(ResourceType.Board);
            contracts.Tick(4f);

            contracts.Count(ResourceType.Board);

            Assert.IsFalse(contracts.IsActive, "сразу за закрытым контрактом нового не бывает");
            Assert.GreaterOrEqual(contracts.SecondsToNext, MinPause);
            Assert.LessOrEqual(contracts.SecondsToNext, MaxPause);

            contracts.Tick(MinPause - 0.1f);
            Assert.IsFalse(contracts.IsActive, "пауза ещё идёт");

            contracts.Tick(MaxPause);

            Assert.IsTrue(contracts.IsActive, "пауза кончилась, контракт выдан");
            Assert.AreEqual(0, contracts.Delivered);
            Assert.AreEqual(Seconds, contracts.SecondsLeft, "таймер нового контракта полный");
        }

        /// <summary>Пауза случайная, но в своих границах, и на каждом круге считается заново.</summary>
        [Test]
        public void PauseBetweenContracts_StaysWithinItsBounds()
        {
            var pauses = new List<float>();

            for (var round = 0; round < 20; round++)
            {
                contracts.Issue();
                contracts.Count(ResourceType.Board);
                contracts.Count(ResourceType.Board);

                pauses.Add(contracts.SecondsToNext);
                Assert.GreaterOrEqual(contracts.SecondsToNext, MinPause);
                Assert.LessOrEqual(contracts.SecondsToNext, MaxPause);
            }

            CollectionAssert.AllItemsAreUnique(pauses, "пауза не случайная, а одна и та же");
        }

        [Test]
        public void Tick_PastTheDeadline_FailsWithoutTakingAnything()
        {
            contracts.Issue();
            contracts.Count(ResourceType.Board);

            contracts.Tick(Seconds + 0.1f);

            Assert.AreEqual(0, wallet.Points, "провал ничем не штрафует");
            CollectionAssert.Contains(log, "failed");
            Assert.IsFalse(contracts.IsActive, "после провала Метрополия молчит паузу");
            Assert.GreaterOrEqual(contracts.SecondsToNext, MinPause);

            contracts.Tick(MaxPause);

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
            var system = new ContractSystem(wallet, types, Goal, Seconds, Reward, MinPause, MaxPause, seed: 7);

            for (var i = 0; i < 50; i++)
            {
                system.Issue();
                CollectionAssert.Contains(types, system.Type);
            }
        }
    }
}
