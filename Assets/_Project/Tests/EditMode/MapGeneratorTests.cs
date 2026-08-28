using System.Collections.Generic;
using Game.Economy;
using Game.Grid;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class MapGeneratorTests
    {
        const int Rows = 10;

        static MapGenerationSettings Settings(int seed) => new(Rows, seed, 30f, 45f, 20f, 5f, 8, 20);

        static MapGenerationSettings SettingsWithWeights(int seed, float empty, float single, float two, float three) =>
            new(Rows, seed, empty, single, two, three, 8, 20);

        [Test]
        public void Generates100TilesWithMetropolisAtTheBottom()
        {
            var map = MapGenerator.Generate(Settings(1));

            Assert.AreEqual(100, map.Count);
            Assert.AreEqual(0, map.Metropolis.Coord.R);
            Assert.IsTrue(map.Metropolis.IsMetropolis);
            Assert.IsEmpty(map.Metropolis.Deposits);
        }

        [Test]
        public void EveryTile_HoldsAtMostThreeDepositsOfDistinctTypes()
        {
            for (var seed = 1; seed <= 50; seed++)
            foreach (var tile in MapGenerator.Generate(Settings(seed)).Tiles.Values)
            {
                Assert.LessOrEqual(tile.Deposits.Count, 3);

                var seen = new HashSet<ResourceType>();
                foreach (var deposit in tile.Deposits)
                    Assert.IsTrue(seen.Add(deposit.Type), $"повтор типа {deposit.Type} на плитке {tile.Coord}");
            }
        }

        [Test]
        public void EveryDeposit_HoldsReserveWithinConfiguredRange()
        {
            for (var seed = 1; seed <= 50; seed++)
            foreach (var tile in MapGenerator.Generate(Settings(seed)).Tiles.Values)
            foreach (var deposit in tile.Deposits)
            {
                Assert.GreaterOrEqual(deposit.Reserve, 8);
                Assert.LessOrEqual(deposit.Reserve, 20);
                Assert.IsFalse(deposit.IsExhausted);
            }
        }

        [Test]
        public void StoneAlwaysSitsNextToMetropolis()
        {
            for (var seed = 1; seed <= 200; seed++)
            {
                var map = MapGenerator.Generate(Settings(seed));
                var hasStone = false;

                foreach (var neighbor in map.NeighborsOf(HexCoord.Zero))
                foreach (var deposit in neighbor.Deposits)
                    hasStone |= deposit.Type == ResourceType.Stone;

                Assert.IsTrue(hasStone, $"seed {seed}: рядом с Метрополией нет камня");
            }
        }

        [Test]
        public void StoneGuarantee_HoldsEvenWhenGeneratorRollsOnlyEmptyTiles()
        {
            var map = MapGenerator.Generate(SettingsWithWeights(7, 1f, 0f, 0f, 0f));
            var stoneCount = 0;

            foreach (var tile in map.Tiles.Values)
                stoneCount += tile.Deposits.Count;

            Assert.AreEqual(1, stoneCount, "гарантия должна добавить ровно одно месторождение");
            foreach (var neighbor in map.NeighborsOf(HexCoord.Zero))
            foreach (var deposit in neighbor.Deposits)
                Assert.AreEqual(ResourceType.Stone, deposit.Type);
        }

        [TestCase(0f, 1f, 0f, 0f, 1)]
        [TestCase(0f, 0f, 1f, 0f, 2)]
        [TestCase(0f, 0f, 0f, 1f, 3)]
        public void Weights_DecideDepositCountPerTile(float empty, float single, float two, float three, int expected)
        {
            var map = MapGenerator.Generate(SettingsWithWeights(3, empty, single, two, three));

            foreach (var tile in map.Tiles.Values)
            {
                if (tile.IsMetropolis)
                    continue;

                Assert.AreEqual(expected, tile.Deposits.Count, $"плитка {tile.Coord}");
            }
        }

        [Test]
        public void SameSeed_ProducesSameMap()
        {
            Assert.AreEqual(Signature(MapGenerator.Generate(Settings(42))), Signature(MapGenerator.Generate(Settings(42))));
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentMaps()
        {
            var signatures = new HashSet<string>();
            for (var seed = 1; seed <= 5; seed++)
                signatures.Add(Signature(MapGenerator.Generate(Settings(seed))));

            Assert.Greater(signatures.Count, 1);
        }

        static string Signature(HexMap map)
        {
            var builder = new System.Text.StringBuilder();
            foreach (var coord in HexMap.CoordsInFlare(Rows))
            {
                map.TryGetTile(coord, out var tile);
                builder.Append(coord).Append(':');
                foreach (var deposit in tile.Deposits)
                    builder.Append(deposit.Type).Append(deposit.Reserve).Append(',');
                builder.Append('|');
            }

            return builder.ToString();
        }
    }
}
