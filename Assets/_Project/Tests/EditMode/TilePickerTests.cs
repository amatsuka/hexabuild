using System.Collections.Generic;
using Game.Grid;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Клик по рельефу. Наклонённая камера сдвигает попадание луча тем сильнее, чем выше плитка:
    /// промах равен «высота / tg(pitch)» и на 0.44 юнита высоты составлял 0.31 — почти две трети
    /// инрадиуса гекса. С рельефом по шуму такой промах перестал быть мелочью.
    /// </summary>
    public sealed class TilePickerTests
    {
        /// <summary>Сдвиг попадания на юнит высоты: `1 / tg(pitch)`, при 55° это примерно 0.7.</summary>
        const float Parallax = 0.7f;

        const float Ceiling = 0.44f;

        [Test]
        public void HighTile_IsPickedAcrossItsWholeCap_NotOnlyAtTheCentre()
        {
            var tall = new HexCoord(0, -3);
            var heights = Flat();
            heights[tall] = Ceiling;

            // Дальний край крышки — та самая точка, где клик уходил на соседа: луч по земле
            // приходит на 0.31 юнита дальше и вываливается за границу плитки.
            var ground = tall.ToPlane() + new Vector2(0f, 0.45f + Parallax * Ceiling);

            Assert.AreEqual(tall, Pick(ground, heights), "клик по дальнему краю высокой плитки ушёл на соседа");
        }

        /// <summary>
        /// Высокая плитка закрывает собой подножие той, что стоит за ней: клик по закрытому куску
        /// обязан достаться высокой. Луч спускается от вершины поля и останавливается на первой
        /// крышке, которая его догнала, — то есть на ближней к камере.
        /// </summary>
        [Test]
        public void HighTile_TakesTheClick_OnThePieceItHides()
        {
            var tall = new HexCoord(0, -3);
            var behind = tall.Neighbor(5);
            var heights = Flat();
            heights[tall] = Ceiling;

            var ground = behind.ToPlane() - new Vector2(0f, 0.4f);

            Assert.AreEqual(tall, Pick(ground, heights), "клик прошёл сквозь высокую плитку на низкую за ней");
        }

        [Test]
        public void FlatField_IsPickedTheSameWayAsBefore()
        {
            var heights = Flat();

            foreach (var coord in heights.Keys)
                Assert.AreEqual(coord, Pick(coord.ToPlane(), heights));
        }

        /// <summary>Клик за краем поля: уточнять нечем, берётся попадание по земле.</summary>
        [Test]
        public void OutsideTheField_TheGroundHitIsKept()
        {
            var outside = new HexCoord(9, -9);

            Assert.AreEqual(outside, Pick(outside.ToPlane(), Flat()));
        }

        static Dictionary<HexCoord, float> Flat()
        {
            var heights = new Dictionary<HexCoord, float>();
            foreach (var coord in HexMap.CoordsInFlare(8))
                heights[coord] = 0f;

            return heights;
        }

        /// <summary>
        /// Плитка под лучом, пришедшим по земле в точку <paramref name="ground"/>. Камера
        /// наклонена: на плоскости высоты `h` тот же луч стоит на `Parallax * h` ближе к камере,
        /// то есть спуск по высоте — это ход вдоль луча от камеры вглубь поля.
        /// </summary>
        static HexCoord Pick(Vector2 ground, IReadOnlyDictionary<HexCoord, float> heights) =>
            TilePicker.Resolve(
                height => HexCoord.FromPlane(ground - new Vector2(0f, Parallax * height)),
                coord => heights.TryGetValue(coord, out var height) ? height : null,
                Ceiling);
    }
}
