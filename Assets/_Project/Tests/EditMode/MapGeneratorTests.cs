using System.Collections.Generic;
using Game.Economy;
using Game.Grid;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class MapGeneratorTests
    {
        const int Rows = 14;

        static MapGenerationSettings Settings(int seed) => new(Rows, seed, 30f, 45f, 20f, 5f, 8, 20);

        static MapGenerationSettings SettingsWithWeights(int seed, float empty, float single, float two, float three) =>
            new(Rows, seed, empty, single, two, three, 8, 20);

        [Test]
        public void Generates105TilesWithMetropolisAtTheBottom()
        {
            var map = MapGenerator.Generate(Settings(1));

            Assert.AreEqual(105, map.Count);
            Assert.AreEqual(0, map.Metropolis.Coord.R);
            Assert.IsTrue(map.Metropolis.IsMetropolis);
            Assert.IsEmpty(map.Metropolis.Deposits);
        }

        /// <summary>
        /// Форму поля стережёт `HexMapTests`, здесь важно, что генератор выкладывает её целиком:
        /// пропущенная плитка отрезала бы кусок карты от Метрополии.
        /// </summary>
        [Test]
        public void EveryGeneratedTile_IsReachableFromTheMetropolis()
        {
            var map = MapGenerator.Generate(Settings(11));
            var visited = new HashSet<HexCoord> { HexCoord.Zero };
            var frontier = new Queue<HexCoord>();
            frontier.Enqueue(HexCoord.Zero);

            while (frontier.Count > 0)
                foreach (var neighbor in map.NeighborsOf(frontier.Dequeue()))
                    if (visited.Add(neighbor.Coord))
                        frontier.Enqueue(neighbor.Coord);

            Assert.AreEqual(map.Count, visited.Count, "часть плиток оторвана от Метрополии");
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

        /// <summary>Камень за горой бесполезен: гарантия обязана лечь на проходимую соседку.</summary>
        [Test]
        public void StoneAlwaysSitsOnAPassableNeighbourOfTheMetropolis()
        {
            for (var seed = 1; seed <= 200; seed++)
            {
                var map = MapGenerator.Generate(Settings(seed));
                var hasStone = false;

                foreach (var neighbor in map.NeighborsOf(HexCoord.Zero))
                {
                    if (!neighbor.IsPassable)
                        continue;

                    foreach (var deposit in neighbor.Deposits)
                        hasStone |= deposit.Type == ResourceType.Stone;
                }

                Assert.IsTrue(hasStone, $"seed {seed}: рядом с Метрополией нет доступного камня");
            }
        }

        // --- M9: препятствия ---

        /// <summary>
        /// Главное обещание стадии: ни один сид не даёт партию с отрезанной Метрополией.
        /// Генератор перебирает смещения шума, пока обход по проходимым плиткам не покроет 85%.
        /// </summary>
        [Test]
        public void PassableFieldFromTheMetropolis_CoversAtLeast85PercentOnEverySeed()
        {
            for (var seed = 1; seed <= 200; seed++)
            {
                var map = MapGenerator.Generate(Settings(seed));
                var visited = new HashSet<HexCoord> { HexCoord.Zero };
                var frontier = new Queue<HexCoord>();
                frontier.Enqueue(HexCoord.Zero);

                while (frontier.Count > 0)
                    foreach (var neighbor in map.NeighborsOf(frontier.Dequeue()))
                        if (neighbor.IsPassable && visited.Add(neighbor.Coord))
                            frontier.Enqueue(neighbor.Coord);

                var share = visited.Count / (float)map.Count;
                Assert.GreaterOrEqual(share, 0.85f, $"seed {seed}: горы отрезали {1f - share:P0} поля");
            }
        }

        [Test]
        public void Metropolis_IsNeverAMountain()
        {
            for (var seed = 1; seed <= 200; seed++)
            {
                var biome = MapGenerator.Generate(Settings(seed)).Metropolis.Biome;

                Assert.AreNotEqual(BiomeType.Mountains, biome, $"seed {seed}");
            }
        }

        [Test]
        public void Mountains_CarryNoDeposits()
        {
            for (var seed = 1; seed <= 50; seed++)
            foreach (var tile in MapGenerator.Generate(Settings(seed)).Tiles.Values)
                if (tile.Biome == BiomeType.Mountains)
                    Assert.IsEmpty(tile.Deposits, $"seed {seed}: у горы {tile.Coord} есть месторождение");
        }

        /// <summary>Река лежит на ребре, а ребро общее: бит обязан стоять с обеих сторон.</summary>
        [Test]
        public void RiverMask_IsSymmetricAcrossTheEdge()
        {
            for (var seed = 1; seed <= 50; seed++)
            {
                var map = MapGenerator.Generate(Settings(seed));

                foreach (var tile in map.Tiles.Values)
                for (var direction = 0; direction < HexCoord.Directions.Count; direction++)
                {
                    if (!tile.HasRiver(direction))
                        continue;

                    if (!map.TryGetTile(tile.Coord.Neighbor(direction), out var other))
                        continue;

                    Assert.IsTrue(
                        other.HasRiver((direction + 3) % 6),
                        $"seed {seed}: у {tile.Coord} река в сторону {direction}, а у соседа её нет");
                }
            }
        }

        [Test]
        public void MapsWithMountains_GetRivers()
        {
            var withMountains = 0;
            var withRivers = 0;

            for (var seed = 1; seed <= 60; seed++)
            {
                var map = MapGenerator.Generate(Settings(seed));
                var mountains = false;
                var rivers = false;

                foreach (var tile in map.Tiles.Values)
                {
                    mountains |= tile.Biome == BiomeType.Mountains;
                    rivers |= tile.RiverMask != 0;
                }

                if (!mountains)
                    continue;

                withMountains++;
                if (rivers)
                    withRivers++;
            }

            Assert.Greater(withMountains, 0, "на шестидесяти сидах не выпало ни одной горы");
            Assert.AreEqual(withMountains, withRivers, "русло берёт начало в горах: есть горы — есть и река");
        }

        /// <summary>
        /// Русло — цепочка, а не россыпь: каждое ребро с рекой делит вершину с другим таким же,
        /// кроме концов русла. Иначе река не была бы преградой, её обходили бы вплотную.
        /// </summary>
        [Test]
        public void River_RunsAsAConnectedChain()
        {
            for (var seed = 1; seed <= 40; seed++)
            {
                var map = MapGenerator.Generate(Settings(seed));
                var edges = new HashSet<(HexCoord Tile, int Direction)>();

                foreach (var tile in map.Tiles.Values)
                for (var direction = 0; direction < HexCoord.Directions.Count; direction++)
                    if (tile.HasRiver(direction))
                        edges.Add(Canonical(tile.Coord, direction));

                var loose = 0;
                foreach (var edge in edges)
                {
                    var touching = 0;
                    foreach (var candidate in NeighborEdges(edge))
                        if (edges.Contains(Canonical(candidate.Tile, candidate.Direction)))
                            touching++;

                    if (touching == 0)
                        loose++;
                }

                Assert.AreEqual(0, loose, $"seed {seed}: {loose} рёбер русла висят в отрыве от остальных");
            }
        }

        static (HexCoord Tile, int Direction) Canonical(HexCoord tile, int direction)
        {
            var other = tile.Neighbor(direction);
            if (tile.Q < other.Q || (tile.Q == other.Q && tile.R < other.R))
                return (tile, direction);

            return (other, (direction + 3) % 6);
        }

        static IEnumerable<(HexCoord Tile, int Direction)> NeighborEdges((HexCoord Tile, int Direction) edge)
        {
            var far = edge.Tile.Neighbor(edge.Direction);
            yield return (edge.Tile, (edge.Direction + 1) % 6);
            yield return (edge.Tile, (edge.Direction + 5) % 6);
            yield return (far, (edge.Direction + 2) % 6);
            yield return (far, (edge.Direction + 4) % 6);
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
                // Гора месторождений не несёт вовсе — веса до неё не доходят.
                if (tile.IsMetropolis || tile.Biome == BiomeType.Mountains)
                    continue;

                Assert.AreEqual(expected, tile.Deposits.Count, $"плитка {tile.Coord}");
            }
        }

        [Test]
        public void EveryTile_GetsALandscape()
        {
            var biomes = new HashSet<BiomeType>();

            foreach (var tile in MapGenerator.Generate(Settings(5)).Tiles.Values)
            {
                biomes.Add(tile.Biome);
                Assert.GreaterOrEqual(tile.Shade, -1f);
                Assert.LessOrEqual(tile.Shade, 1f);
            }

            Assert.Greater(biomes.Count, 1, "поле из одного биома выглядит однотонным пятном");
        }

        /// <summary>
        /// Шум должен давать пятна, а не рябь. При случайной раскладке пяти биомов соседи совпадали
        /// бы примерно в четверти пар; порог берём с запасом и усредняем по сидам, чтобы проверка
        /// не цеплялась за одну неудачную карту.
        /// </summary>
        [Test]
        public void Landscape_IsCoherent_NeighboursUsuallyShareTheBiome()
        {
            var pairs = 0;
            var same = 0;

            for (var seed = 1; seed <= 12; seed++)
            {
                var map = MapGenerator.Generate(Settings(seed));
                foreach (var tile in map.Tiles.Values)
                foreach (var neighbor in map.NeighborsOf(tile.Coord))
                {
                    pairs++;
                    if (neighbor.Biome == tile.Biome)
                        same++;
                }
            }

            Assert.Greater(same / (float)pairs, 0.4f, "биомы рассыпались в мозаику вместо пятен");
        }

        [Test]
        public void Landscape_FollowsTheSeed()
        {
            var first = MapGenerator.Generate(Settings(3));
            var second = MapGenerator.Generate(Settings(3));
            var other = MapGenerator.Generate(Settings(4));

            var sameSeedDiffers = 0;
            var otherSeedDiffers = 0;
            foreach (var coord in HexMap.CoordsInFlare(Rows))
            {
                first.TryGetTile(coord, out var a);
                second.TryGetTile(coord, out var b);
                other.TryGetTile(coord, out var c);
                if (a.Biome != b.Biome) sameSeedDiffers++;
                if (a.Biome != c.Biome) otherSeedDiffers++;
            }

            Assert.AreEqual(0, sameSeedDiffers, "один и тот же seed должен давать тот же ландшафт");
            Assert.Greater(otherSeedDiffers, 0, "разные seed должны давать разный ландшафт");
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
                builder.Append(coord).Append(':').Append(tile.Biome).Append(':');
                foreach (var deposit in tile.Deposits)
                    builder.Append(deposit.Type).Append(deposit.Reserve).Append(',');
                builder.Append('|');
            }

            return builder.ToString();
        }
    }
}
