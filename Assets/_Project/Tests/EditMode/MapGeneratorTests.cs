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

        /// <summary>
        /// Поле обязано быть проходимо целиком: гряда, замкнувшая кусок поля, вычёркивает его из
        /// партии навсегда — открыть такие плитки нельзя никогда. Генератор пробивает в грядах
        /// перевалы, и после этого непроходимыми остаются только сами горы.
        /// </summary>
        [Test]
        public void EveryPassableTile_IsReachableFromTheMetropolis_OnEverySeed()
        {
            for (var seed = 1; seed <= 200; seed++)
            {
                var map = MapGenerator.Generate(Settings(seed));
                var reachable = new HashSet<HexCoord> { HexCoord.Zero };
                var frontier = new Queue<HexCoord>();
                frontier.Enqueue(HexCoord.Zero);

                while (frontier.Count > 0)
                    foreach (var neighbor in map.NeighborsOf(frontier.Dequeue()))
                        if (neighbor.IsPassable && reachable.Add(neighbor.Coord))
                            frontier.Enqueue(neighbor.Coord);

                foreach (var tile in map.Tiles.Values)
                    if (tile.IsPassable)
                        Assert.IsTrue(reachable.Contains(tile.Coord), $"seed {seed}: {tile.Coord} отрезана грядой");
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

        /// <summary>
        /// Русло переходит с плитки на плитку: бит в сторону соседа обязан иметь ответный бит.
        /// Бит наружу поля ответного не имеет — это устье.
        /// </summary>
        [Test]
        public void RiverMask_IsSymmetricBetweenNeighbours()
        {
            for (var seed = 1; seed <= 50; seed++)
            {
                var map = MapGenerator.Generate(Settings(seed));

                foreach (var tile in map.Tiles.Values)
                for (var direction = 0; direction < HexCoord.Directions.Count; direction++)
                {
                    if ((tile.RiverMask & (1 << direction)) == 0)
                        continue;

                    if (!map.TryGetTile(tile.Coord.Neighbor(direction), out var other))
                        continue;

                    Assert.AreNotEqual(
                        0,
                        other.RiverMask & (1 << ((direction + 3) % 6)),
                        $"seed {seed}: у {tile.Coord} русло уходит в сторону {direction}, а у соседа его нет");
                }
            }
        }

        /// <summary>Горы обязаны быть: без них у реки нет истока, а у поля — препятствий.</summary>
        [Test]
        public void EveryMap_HasMountainsAndARiver()
        {
            for (var seed = 1; seed <= 200; seed++)
            {
                var map = MapGenerator.Generate(Settings(seed));
                var mountains = 0;
                var riverTiles = 0;

                foreach (var tile in map.Tiles.Values)
                {
                    if (tile.Biome == BiomeType.Mountains)
                        mountains++;
                    if (tile.HasRiver)
                        riverTiles++;
                }

                Assert.Greater(mountains, 0, $"seed {seed}: карта без единой горы");
                Assert.Greater(riverTiles, 0, $"seed {seed}: горы есть, а реки нет");
            }
        }

        [Test]
        public void Rivers_StartInTheMountains()
        {
            for (var seed = 1; seed <= 40; seed++)
            {
                var map = MapGenerator.Generate(Settings(seed));

                foreach (var component in RiverComponents(map))
                {
                    var fromMountains = false;
                    foreach (var tile in component)
                        fromMountains |= tile.Biome == BiomeType.Mountains;

                    Assert.IsTrue(fromMountains, $"seed {seed}: русло у {component[0].Coord} не берёт начала в горах");
                }
            }
        }

        /// <summary>Река идёт по поверхности плитки: месторождению там уже не место.</summary>
        [Test]
        public void RiverTiles_CarryNoDeposits()
        {
            for (var seed = 1; seed <= 50; seed++)
            foreach (var tile in MapGenerator.Generate(Settings(seed)).Tiles.Values)
                if (tile.HasRiver)
                    Assert.IsEmpty(tile.Deposits, $"seed {seed}: у речной плитки {tile.Coord} есть месторождение");
        }

        /// <summary>Русло уходит за границу поля: река, кончающаяся в чистом поле, читается сломанной.</summary>
        [Test]
        public void Rivers_ReachTheEdgeOfTheField()
        {
            for (var seed = 1; seed <= 40; seed++)
            {
                var map = MapGenerator.Generate(Settings(seed));

                foreach (var component in RiverComponents(map))
                {
                    var mouths = 0;
                    foreach (var tile in component)
                    for (var direction = 0; direction < HexCoord.Directions.Count; direction++)
                        if ((tile.RiverMask & (1 << direction)) != 0 && !map.Contains(tile.Coord.Neighbor(direction)))
                            mouths++;

                    Assert.Greater(mouths, 0, $"seed {seed}: русло у {component[0].Coord} не дошло до края поля");
                }
            }
        }

        /// <summary>
        /// Кромка — только устье. Русло, свернувшее вдоль края, шло бы по границе карты и резало
        /// бы от поля целую полосу плиток.
        /// </summary>
        [Test]
        public void Rivers_NeverRunAlongTheEdgeOfTheField()
        {
            for (var seed = 1; seed <= 60; seed++)
            {
                var map = MapGenerator.Generate(Settings(seed));

                foreach (var tile in map.Tiles.Values)
                {
                    if (!tile.HasRiver || !IsBorder(map, tile.Coord))
                        continue;

                    for (var direction = 0; direction < HexCoord.Directions.Count; direction++)
                    {
                        if ((tile.RiverMask & (1 << direction)) == 0)
                            continue;

                        if (!map.TryGetTile(tile.Coord.Neighbor(direction), out var other))
                            continue;

                        Assert.IsFalse(
                            IsBorder(map, other.Coord),
                            $"seed {seed}: русло идёт по кромке от {tile.Coord} к {other.Coord}");
                    }
                }
            }
        }

        [Test]
        public void Rivers_SometimesBranch()
        {
            var branched = 0;

            for (var seed = 1; seed <= 40; seed++)
            foreach (var tile in MapGenerator.Generate(Settings(seed)).Tiles.Values)
                if (BitCount(tile.RiverMask) >= 3)
                {
                    branched++;
                    break;
                }

            Assert.Greater(branched, 0, "на сорока сидах река ни разу не разветвилась");
        }

        [Test]
        public void Metropolis_IsNeverCrossedByARiver()
        {
            for (var seed = 1; seed <= 200; seed++)
                Assert.IsFalse(MapGenerator.Generate(Settings(seed)).Metropolis.HasRiver, $"seed {seed}");
        }

        /// <summary>
        /// Плитка с рекой не несёт месторождений и стоит трёх щебней под дорогу, поэтому их
        /// число — вопрос баланса. Замер держит бюджет в тех рамках, из которых считался M10.
        /// </summary>
        [Test]
        public void Rivers_CoverAModestShareOfTheField()
        {
            var total = 0;

            for (var seed = 1; seed <= 200; seed++)
            foreach (var tile in MapGenerator.Generate(Settings(seed)).Tiles.Values)
                if (tile.HasRiver)
                    total++;

            var average = total / 200f;

            Assert.GreaterOrEqual(average, 8f, $"рек стало мало: {average:F1} плитки на карту");
            Assert.LessOrEqual(average, 18f, $"река съедает поле: {average:F1} плитки на карту");
        }

        /// <summary>Связные куски русла: обход по битам маски от каждой ещё не пройденной плитки.</summary>
        static List<List<TileData>> RiverComponents(HexMap map)
        {
            var components = new List<List<TileData>>();
            var visited = new HashSet<HexCoord>();

            foreach (var tile in map.Tiles.Values)
            {
                if (!tile.HasRiver || !visited.Add(tile.Coord))
                    continue;

                var component = new List<TileData> { tile };
                var frontier = new Queue<TileData>();
                frontier.Enqueue(tile);

                while (frontier.Count > 0)
                {
                    var current = frontier.Dequeue();
                    for (var direction = 0; direction < HexCoord.Directions.Count; direction++)
                    {
                        if ((current.RiverMask & (1 << direction)) == 0)
                            continue;

                        if (!map.TryGetTile(current.Coord.Neighbor(direction), out var next) || !visited.Add(next.Coord))
                            continue;

                        component.Add(next);
                        frontier.Enqueue(next);
                    }
                }

                components.Add(component);
            }

            return components;
        }

        static bool IsBorder(HexMap map, HexCoord coord)
        {
            for (var direction = 0; direction < HexCoord.Directions.Count; direction++)
                if (!map.Contains(coord.Neighbor(direction)))
                    return true;

            return false;
        }

        static int BitCount(int mask)
        {
            var count = 0;
            for (var bit = 0; bit < HexCoord.Directions.Count; bit++)
                if ((mask & (1 << bit)) != 0)
                    count++;

            return count;
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
                // Гора и плитка с рекой месторождений не несут вовсе — веса до них не доходят.
                if (tile.IsMetropolis || tile.Biome == BiomeType.Mountains || tile.HasRiver)
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
