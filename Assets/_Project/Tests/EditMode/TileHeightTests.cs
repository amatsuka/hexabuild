using Game.Grid;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Рельеф поля. Высота идёт из того же шума, что и биом: пока она была ступенькой по биому,
    /// весь лес стоял на одном уровне — это были пять плато, а не рельеф. С M17 та же кривая
    /// разводит сушу и дно по урезу воды.
    /// </summary>
    public sealed class TileHeightTests
    {
        static TileData Tile(float elevation, BiomeType biome = BiomeType.Forest, int riverMask = 0) =>
            new(new HexCoord(0, 0), false, null, biome, 0f, riverMask, elevation);

        [Test]
        public void Height_NeverFalls_AsTheNoiseRises()
        {
            var previous = float.MinValue;

            for (var step = 0; step <= 100; step++)
            {
                var height = TileView.TerrainHeight(step * 0.01f);

                Assert.GreaterOrEqual(height, previous, "высота просела там, где шум вырос");
                previous = height;
            }
        }

        /// <summary>Числа спеки: рельеф лежит между дном моря и вершиной горы.</summary>
        [Test]
        public void Height_StaysWithinTheFieldRange()
        {
            Assert.AreEqual(TileView.SeaFloor, TileView.TerrainHeight(0f), 1e-4f);
            Assert.AreEqual(0.48f, TileView.TerrainHeight(1f), 1e-4f);
        }

        /// <summary>
        /// Урез делит поле надвое: вода лежит под ним, любая суша — над. На этом стоит и картинка,
        /// и правило — по отмели под водой предлагалось бы строить дорогу.
        /// </summary>
        [Test]
        public void TheWaterline_SplitsTheFieldInTwo()
        {
            for (var step = 0; step <= 100; step++)
            {
                var elevation = step * 0.01f;

                Assert.Less(
                    TileView.HeightOf(Tile(elevation, BiomeType.Water)), TileView.WaterSurface,
                    $"вода на {elevation} вышла над урезом");
                Assert.GreaterOrEqual(
                    TileView.HeightOf(Tile(elevation, BiomeType.Meadow)), TileView.WaterSurface,
                    $"суша на {elevation} ушла под урез");
            }
        }

        /// <summary>
        /// Перевал, пробитый сквозь залив, выходит отмелью над водой. Биом ему меняет генератор,
        /// а высота остаётся низинной — без всплытия он был бы проходимым и невидимым.
        /// </summary>
        [Test]
        public void APassageThroughTheBay_SurfacesAsAShoal()
        {
            var shoal = TileView.HeightOf(Tile(0.02f, BiomeType.Sand));

            Assert.AreEqual(TileView.ShoreHeight, shoal, 1e-4f);
        }

        /// <summary>
        /// Плитка с рекой садится ниже своего рельефа: ленту русла в непрозрачную крышку не
        /// утопить, тонет вся плитка. Но не ниже уреза — река течёт над водой, а не под ней.
        /// </summary>
        [Test]
        public void ARiverTile_SitsBelowItsOwnTerrain_ButAboveTheWaterline()
        {
            const float elevation = 0.55f;
            var dry = TileView.HeightOf(Tile(elevation));
            var wet = TileView.HeightOf(Tile(elevation, BiomeType.Forest, 1));

            Assert.Less(wet, dry, "русло не утоплено: лента ляжет вровень с соседями");
            Assert.Greater(wet, TileView.WaterSurface, "русло ушло под урез");
            Assert.Greater(TileView.HeightOf(Tile(0.28f, BiomeType.Sand, 1)), TileView.WaterSurface);
        }

        /// <summary>
        /// Канавка фаски между двумя соседними плитками остаётся сухой. Борта соседей сходятся
        /// на глубину `BevelDrop` от крышки, и стоит самой низкой суше сесть ниже этой глубины
        /// над урезом — вода поднимается в канавку и обводит пеной каждую плитку поля. Ровно ту
        /// обводку M16 и убирала: в 3D она ложится поперёк объёма.
        /// </summary>
        [Test]
        public void TheBevelGroove_StaysDryOnEveryLandTile()
        {
            for (var step = 0; step <= 100; step++)
            {
                var elevation = step * 0.01f;

                foreach (var biome in new[] { BiomeType.Sand, BiomeType.Meadow, BiomeType.Forest })
                foreach (var river in new[] { 0, 1 })
                {
                    var groove = TileView.HeightOf(Tile(elevation, biome, river)) - HexMeshBuilder.BevelDrop;

                    Assert.Greater(
                        groove, TileView.WaterSurface,
                        $"{biome} на {elevation} (река: {river != 0}): вода поднялась в канавку фаски");
                }
            }
        }

        /// <summary>Порядок биомов по высоте прежний: гора выше скал, скалы выше леса, низины внизу.</summary>
        [Test]
        public void Biomes_KeepTheirOrderOfHeight()
        {
            var water = TileView.TerrainHeight(0.10f);
            var sand = TileView.TerrainHeight(0.30f);
            var meadow = TileView.TerrainHeight(0.43f);
            var forest = TileView.TerrainHeight(0.55f);
            var rocks = TileView.TerrainHeight(0.62f);
            var mountains = TileView.TerrainHeight(0.80f);

            Assert.Less(water, sand);
            Assert.Less(sand, meadow);
            Assert.Less(meadow, forest);
            Assert.Less(forest, rocks);
            Assert.Less(rocks, mountains);
        }

        /// <summary>
        /// Внутри одного биома высота идёт непрерывно. Это и есть весь смысл стадии: пять
        /// ступеней давали пять плато, а рельеф начинается там, где соседний лес разной высоты.
        /// </summary>
        [Test]
        public void OneBiome_IsNotAPlateau()
        {
            Assert.AreNotEqual(
                TileView.TerrainHeight(MapGenerator.MeadowCeiling + 0.01f),
                TileView.TerrainHeight(MapGenerator.ForestCeiling - 0.01f),
                "лес встал на одно плато");
        }
    }
}
