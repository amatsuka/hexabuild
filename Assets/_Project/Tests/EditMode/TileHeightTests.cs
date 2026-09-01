using Game.Grid;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Рельеф поля. Высота идёт из того же шума, что и биом: пока она была ступенькой по биому,
    /// весь лес стоял на одном уровне — это были пять плато, а не рельеф.
    /// </summary>
    public sealed class TileHeightTests
    {
        static TileData Tile(float elevation) =>
            new(new HexCoord(0, 0), false, null, BiomeType.Forest, 0f, 0, elevation);

        [Test]
        public void Height_NeverFalls_AsTheNoiseRises()
        {
            var previous = float.MinValue;

            for (var step = 0; step <= 100; step++)
            {
                var height = TileView.HeightOf(Tile(step * 0.01f));

                Assert.GreaterOrEqual(height, previous, "высота просела там, где шум вырос");
                previous = height;
            }
        }

        /// <summary>Числа спеки: поле лежит в диапазоне 0…0.44 юнита, дном служит песок низин.</summary>
        [Test]
        public void Height_StaysWithinTheFieldRange()
        {
            Assert.AreEqual(0f, TileView.HeightOf(Tile(0f)), 1e-4f);
            Assert.AreEqual(0.44f, TileView.HeightOf(Tile(1f)), 1e-4f);
        }

        /// <summary>Порядок биомов по высоте прежний: гора выше скал, скалы выше леса, низины внизу.</summary>
        [Test]
        public void Biomes_KeepTheirOrderOfHeight()
        {
            var sand = TileView.HeightOf(Tile(0.20f));
            var meadow = TileView.HeightOf(Tile(0.43f));
            var forest = TileView.HeightOf(Tile(0.55f));
            var rocks = TileView.HeightOf(Tile(0.62f));
            var mountains = TileView.HeightOf(Tile(0.80f));

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
                TileView.HeightOf(Tile(MapGenerator.MeadowCeiling + 0.01f)),
                TileView.HeightOf(Tile(MapGenerator.ForestCeiling - 0.01f)),
                "лес встал на одно плато");
        }
    }
}
