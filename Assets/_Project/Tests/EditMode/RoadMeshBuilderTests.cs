using Game.Grid;
using Game.Roads;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class RoadMeshBuilderTests
    {
        const float Width = 0.17f;

        /// <summary>Маска сквозного проезда: два противоположных направления.</summary>
        const int StraightMask = (1 << 0) | (1 << 3);

        /// <summary>Маска поворота: два направления под углом.</summary>
        const int TurnMask = (1 << 0) | (1 << 2);

        [Test]
        public void SameMaskAndWidth_ReuseTheSameMesh()
        {
            Assert.AreSame(RoadMeshBuilder.Get(TurnMask, Width), RoadMeshBuilder.Get(TurnMask, Width));
        }

        [Test]
        public void DifferentWidth_GetsItsOwnMesh()
        {
            Assert.AreNotSame(RoadMeshBuilder.Get(TurnMask, Width), RoadMeshBuilder.Get(TurnMask, Width + 0.08f));
        }

        [Test]
        public void EmptyMask_StillDrawsTheHub_SoALoneRoadIsVisible()
        {
            var mesh = RoadMeshBuilder.Get(0, Width);

            Assert.AreEqual(7, mesh.vertexCount, "пятачок — центр и шесть углов");
            Assert.AreEqual(18, mesh.triangles.Length);
        }

        /// <summary>Лента должна доходить ровно до середины общей грани, не обрываясь раньше.</summary>
        [TestCase(0)]
        [TestCase(2)]
        [TestCase(5)]
        public void RibbonReachesTheEdgeMidpoint(int direction)
        {
            var mesh = RoadMeshBuilder.Get(1 << direction, Width);
            var edge = HexCoord.Directions[direction].ToWorld() * 0.5f;

            var closest = float.MaxValue;
            foreach (var vertex in mesh.vertices)
                closest = Mathf.Min(closest, Vector2.Distance(new Vector2(vertex.x, vertex.y), edge));

            Assert.LessOrEqual(closest, Width * 0.5f + 1e-4f, $"лента не дотянулась до грани {direction}");
        }

        [Test]
        public void Ribbon_StaysInsideTheTile()
        {
            var mesh = RoadMeshBuilder.Get(0x3f, Width);

            foreach (var vertex in mesh.vertices)
                Assert.LessOrEqual(
                    new Vector2(vertex.x, vertex.y).magnitude, HexCoord.Size + 1e-4f,
                    "лента вылезла за описанную окружность гекса и залезет на соседа");
        }

        /// <summary>
        /// Сквозной проезд — прямая: у противоположных направлений опорная точка в центре
        /// вырождает кривую Безье в отрезок. Проверяем, что лента не гуляет вбок.
        /// </summary>
        [Test]
        public void OppositeDirections_MakeAStraightRibbon()
        {
            var mesh = RoadMeshBuilder.Get(StraightMask, Width);
            var along = HexCoord.Directions[0].ToWorld().normalized;
            var across = new Vector2(-along.y, along.x);

            foreach (var vertex in mesh.vertices)
                Assert.LessOrEqual(
                    Mathf.Abs(Vector2.Dot(new Vector2(vertex.x, vertex.y), across)), Width * 0.5f + 1e-4f,
                    "прямой проезд отклонился от оси");
        }

        /// <summary>
        /// Поворот идёт дугой, а не изломом. Тест белый: знает раскладку вершин билдера — у ленты
        /// из двух направлений пятачка нет, вершины идут парами «левая, правая», — и по ней
        /// восстанавливает осевую линию. Излом из двух отрезков дал бы один поворот на 120°,
        /// у дуги он размазан по всем шагам.
        /// </summary>
        [Test]
        public void Turn_IsRoundedNotAKink()
        {
            var mesh = RoadMeshBuilder.Get(TurnMask, Width);
            var axis = Centerline(mesh);

            Assert.GreaterOrEqual(axis.Length, 5, "дуга должна быть разбита на отрезки");
            Assert.AreEqual(0f, Vector2.Distance(HexCoord.Directions[0].ToWorld() * 0.5f, axis[0]), 1e-4f,
                "дуга начинается на середине грани");
            Assert.AreEqual(0f, Vector2.Distance(HexCoord.Directions[2].ToWorld() * 0.5f, axis[^1]), 1e-4f,
                "дуга кончается на середине грани");

            var sharpest = 0f;
            for (var i = 1; i < axis.Length - 1; i++)
                sharpest = Mathf.Max(sharpest, Vector2.Angle(axis[i] - axis[i - 1], axis[i + 1] - axis[i]));

            Assert.Less(sharpest, 45f, $"самый резкий излом дуги — {sharpest:F0}°, это ещё угол, а не поворот");
        }

        /// <summary>Осевая линия ленты из одной дуги: вершины идут парами «левая, правая».</summary>
        static Vector2[] Centerline(Mesh mesh)
        {
            var vertices = mesh.vertices;
            var axis = new Vector2[vertices.Length / 2];
            for (var i = 0; i < axis.Length; i++)
            {
                var left = vertices[i * 2];
                var right = vertices[i * 2 + 1];
                axis[i] = new Vector2((left.x + right.x) * 0.5f, (left.y + right.y) * 0.5f);
            }

            return axis;
        }

        /// <summary>
        /// Дуга поворота срезает угол и проходит мимо центра плитки, поэтому пятачок торчал бы
        /// из внутренней стороны поворота шишкой. Опознаём его по вершине ровно в центре: у ленты
        /// такой нет, её вершины всегда отступают на полширины от осевой линии.
        /// </summary>
        [Test]
        public void Turn_HasNoHubBumpOnTheInsideOfTheCurve()
        {
            foreach (var vertex in RoadMeshBuilder.Get(TurnMask, Width).vertices)
                Assert.Greater(
                    new Vector2(vertex.x, vertex.y).magnitude, 1e-4f,
                    "в центре плитки лежит вершина пятачка, а на повороте его быть не должно");
        }

        /// <summary>А тупику пятачок нужен: он скругляет обрубленный конец.</summary>
        [Test]
        public void DeadEnd_KeepsTheHub()
        {
            var closest = float.MaxValue;
            foreach (var vertex in RoadMeshBuilder.Get(1, Width).vertices)
                closest = Mathf.Min(closest, new Vector2(vertex.x, vertex.y).magnitude);

            Assert.AreEqual(0f, closest, 1e-4f, "у тупика нет вершины в центре — пятачок пропал");
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(StraightMask)]
        [TestCase(TurnMask)]
        [TestCase(0x3f)]
        public void Triangles_FaceTheCamera(int linkMask)
        {
            var mesh = RoadMeshBuilder.Get(linkMask, Width);
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;

            Assert.Greater(triangles.Length, 0);
            for (var i = 0; i < triangles.Length; i += 3)
            {
                var a = vertices[triangles[i]];
                var b = vertices[triangles[i + 1]];
                var c = vertices[triangles[i + 2]];
                var signedArea = (b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y);

                Assert.Less(signedArea, 0f,
                    $"маска {linkMask:X2}: треугольник {i / 3} обходится против часовой и будет срезан");
            }
        }
    }
}
