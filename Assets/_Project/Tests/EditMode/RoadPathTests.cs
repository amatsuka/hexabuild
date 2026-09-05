using System;
using System.Collections.Generic;
using Game.Grid;
using Game.Roads;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Осевая линия маршрута: по ней в шаге 5 стадии поедет повозка, поэтому проверяется не
    /// картинка, а контракт — линия лежит на полотне насыпи, поворот скруглён, а расстояние
    /// вдоль неё честно параметризовано.
    /// </summary>
    public sealed class RoadPathTests
    {
        const float Flat = 0.12f;

        static readonly HexCoord Metropolis = HexCoord.Zero;
        static readonly HexCoord First = new(0, 1);
        static readonly HexCoord Straight = new(0, 2);

        /// <summary>Сосед (0,1) под углом: цепочка через него поворачивает, а не идёт насквозь.</summary>
        static readonly HexCoord Corner = new(1, 1);

        static Func<HexCoord, float> Heights(params (HexCoord Coord, float Height)[] raised)
        {
            var map = new Dictionary<HexCoord, float>();
            foreach (var (coord, height) in raised)
                map[coord] = height;

            return coord => map.TryGetValue(coord, out var height) ? height : Flat;
        }

        static Vector3 Center(HexCoord coord, float height)
        {
            var plane = coord.ToPlane();
            return new Vector3(plane.x, height + RoadMeshBuilder.SurfaceLift, plane.y);
        }

        [Test]
        public void SingleTile_HasNoLengthAndStaysPut()
        {
            var path = RoadPath.Along(new[] { First }, Heights());

            Assert.AreEqual(0f, path.Length, 1e-5f);
            Assert.AreEqual(0f, Vector3.Distance(Center(First, Flat), path.At(0f).Position), 1e-4f);
        }

        [Test]
        public void EmptyChain_IsRefused()
        {
            Assert.Throws<ArgumentException>(() => RoadPath.Along(Array.Empty<HexCoord>(), Heights()));
        }

        /// <summary>Концы линии — центры крайних плиток: груз выезжает из клетки и въезжает в город.</summary>
        [Test]
        public void Ends_SitOnTheTileCentres()
        {
            var path = RoadPath.Along(new[] { Straight, First, Metropolis }, Heights());

            Assert.AreEqual(0f, Vector3.Distance(Center(Straight, Flat), path.At(0f).Position), 1e-4f);
            Assert.AreEqual(0f, Vector3.Distance(Center(Metropolis, Flat), path.At(path.Length).Position), 1e-4f);
        }

        /// <summary>Прямая цепочка — прямая линия: длина равна расстоянию между центрами.</summary>
        [Test]
        public void StraightChain_IsAsLongAsTheDistanceBetweenCentres()
        {
            var path = RoadPath.Along(new[] { Straight, First, Metropolis }, Heights());

            Assert.AreEqual(2f, path.Length, 1e-3f);
        }

        /// <summary>
        /// Линия лежит на полотне, а не на земле: на шве полотно встаёт на высоту старшей плитки,
        /// и линия обязана повторить это — иначе груз поедет сквозь насыпь.
        /// </summary>
        [Test]
        public void Line_RidesOnTheRoadSurface()
        {
            const float raised = 0.33f;
            var path = RoadPath.Along(new[] { Straight, First, Metropolis }, Heights((Straight, raised)));

            var seam = path.At(0.5f);

            Assert.AreEqual(raised + RoadMeshBuilder.SurfaceLift, seam.Position.y, 1e-3f,
                "на шве линия села ниже полотна");
        }

        /// <summary>Уклон разворачивает нормаль: на нём повозке достанется крен.</summary>
        [Test]
        public void Slope_TiltsTheSurfaceNormal()
        {
            var path = RoadPath.Along(new[] { Straight, First, Metropolis }, Heights((Straight, 0.33f)));

            // Спуск лежит между швом с поднятой плиткой и центром (0,1): дальше поле снова ровное.
            var downhill = path.At(0.75f);

            Assert.Less(downhill.Forward.y, -0.05f, "спуск к Метрополии не читается в направлении движения");
            Assert.Less(downhill.Up.y, 1f - 1e-3f, "нормаль осталась строго вертикальной на уклоне");
            Assert.AreEqual(0f, Vector3.Dot(downhill.Forward, downhill.Up), 1e-3f, "нормаль не перпендикулярна ходу");
        }

        /// <summary>
        /// Поворот внутри плитки скруглён, а не сломан. Без этого повозка на узле разворачивалась
        /// бы на месте, а не проезжала дугу.
        /// </summary>
        [Test]
        public void Corner_IsRounded()
        {
            var path = RoadPath.Along(new[] { Corner, First, Metropolis }, Heights());
            var points = path.Points;

            var sharpest = 0f;
            for (var i = 1; i < points.Count - 1; i++)
                sharpest = Mathf.Max(sharpest, Vector3.Angle(points[i] - points[i - 1], points[i + 1] - points[i]));

            Assert.Less(sharpest, 45f, $"самый резкий излом линии — {sharpest:F0}°, это ещё угол, а не поворот");
        }

        /// <summary>Скруглённый поворот короче ломаной через центр плитки, но длиннее хорды.</summary>
        [Test]
        public void Corner_IsShorterThanTheKinkItReplaces()
        {
            var path = RoadPath.Along(new[] { Corner, First, Metropolis }, Heights());

            Assert.Less(path.Length, 2f, "линия не срезала угол — поворот остался ломаной");
            Assert.Greater(path.Length, Vector3.Distance(Center(Corner, Flat), Center(Metropolis, Flat)));
        }

        /// <summary>
        /// Параметризация по длине дуги: равные шаги расстояния дают равные шаги по земле.
        /// На этом стоит весь шаг 5 — груз едет с постоянной скоростью, а не рывками на поворотах.
        /// </summary>
        [Test]
        public void Sampling_IsEvenlySpacedAlongTheArc()
        {
            var path = RoadPath.Along(new[] { Corner, First, Metropolis }, Heights());
            const int steps = 20;
            var step = path.Length / steps;

            var previous = path.At(0f).Position;
            for (var i = 1; i <= steps; i++)
            {
                var next = path.At(step * i).Position;
                Assert.AreEqual(step, Vector3.Distance(previous, next), step * 0.05f,
                    $"шаг {i} по длине дуги вышел неровным");
                previous = next;
            }
        }

        /// <summary>За пределами линии берётся её конец: округление не должно ронять груз в никуда.</summary>
        [Test]
        public void OutsideTheLine_ClampsToItsEnds()
        {
            var path = RoadPath.Along(new[] { Straight, First, Metropolis }, Heights());

            Assert.AreEqual(0f, Vector3.Distance(path.At(0f).Position, path.At(-5f).Position), 1e-4f);
            Assert.AreEqual(0f, Vector3.Distance(path.At(path.Length).Position, path.At(99f).Position), 1e-4f);
        }
    }
}
