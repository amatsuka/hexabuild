using Game.Grid;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Тесты ленты русла. С M22 осевая линия сносится от решётки по хэшу координаты
    /// (`RiverCourse`), а ширина растёт по течению (`RiverWidth`) — меш строится на плитку и
    /// больше не кэшируется по маске. Тесты поэтому строят свою `TileData` под каждый сценарий,
    /// а не зовут билдер сырыми числами маски.
    /// </summary>
    public sealed class RiverMeshBuilderTests
    {
        /// <summary>Маска сквозного протока: два противоположных направления.</summary>
        const int StraightMask = (1 << 0) | (1 << 3);

        /// <summary>Маска поворота: два направления под углом.</summary>
        const int TurnMask = (1 << 0) | (1 << 2);

        /// <summary>Маска развилки: три направления.</summary>
        const int BranchMask = (1 << 0) | (1 << 2) | (1 << 4);

        static TileData RiverTile(HexCoord coord, int mask, int flow = 4, int downMask = 0) =>
            new(coord, false, null, BiomeType.Meadow, 0f, mask, 0f, flow, downMask);

        [Test]
        public void EmptyMask_StillDrawsTheHub_SoALoneArmIsVisible()
        {
            var mesh = RiverMeshBuilder.Build(RiverTile(HexCoord.Zero, 0), 1f);

            Assert.AreEqual(7, mesh.vertexCount, "пятачок — центр и шесть углов");
            Assert.AreEqual(18, mesh.triangles.Length);
        }

        /// <summary>Лента должна доходить ровно до ворот грани, не обрываясь раньше и не переезжая их.</summary>
        [TestCase(0)]
        [TestCase(2)]
        [TestCase(5)]
        public void RibbonReachesTheGate(int direction)
        {
            var tile = RiverTile(new HexCoord(1, -2), 1 << direction, flow: 4, downMask: 1 << direction);
            var mesh = RiverMeshBuilder.Build(tile, 1f);
            var gate = RiverCourse.Gate(tile.Coord, direction);
            var half = RiverWidth.Water(RiverWidth.GateFlow(tile, direction)) * 0.5f;

            var closest = float.MaxValue;
            foreach (var vertex in mesh.vertices)
                closest = Mathf.Min(closest, Vector2.Distance(Plane(vertex), gate));

            Assert.LessOrEqual(closest, half + 1e-4f, $"лента не дотянулась до ворот направления {direction}");
        }

        /// <summary>
        /// Внутрь **шестиугольника**, а не описанной окружности: снос ворот уводит вершину к углу
        /// гекса, и прежняя проверка по окружности такой промах пропустила бы.
        /// </summary>
        [Test]
        public void Ribbon_StaysInsideTheTile()
        {
            var mesh = RiverMeshBuilder.Build(RiverTile(new HexCoord(-3, 4), 0x3f), 1f);

            // Апофема — 0.5 (Width/2), а не HexCoord.Size/2: то, второе, полудлина самой грани,
            // и ей меряют снос ворот вдоль грани, а не расстояние вглубь до её линии.
            const float apothem = 0.5f;

            for (var direction = 0; direction < HexCoord.Directions.Count; direction++)
            {
                var normal = HexCoord.Directions[direction].ToPlane().normalized;
                foreach (var vertex in mesh.vertices)
                    Assert.LessOrEqual(
                        Vector2.Dot(Plane(vertex), normal), apothem + 1e-4f,
                        $"лента вылезла за грань {direction} и залезет на соседа");
            }
        }

        /// <summary>
        /// Сквозной проток обязан отклониться от оси — иначе три плитки подряд снова дают
        /// линейку, — но не больше чем на `GateDrift + BendDrift`. Это и есть контракт стадии.
        /// </summary>
        [Test]
        public void OppositeDirections_WanderOffTheAxisWithinTheInvariant()
        {
            var tile = RiverTile(new HexCoord(5, -2), StraightMask);
            var mesh = RiverMeshBuilder.Build(tile, 1f);
            var axis = Centerline(mesh);
            var along = HexCoord.Directions[0].ToPlane().normalized;
            var across = new Vector2(-along.y, along.x);

            foreach (var point in axis)
                Assert.LessOrEqual(
                    Mathf.Abs(Vector2.Dot(point, across)), RiverCourse.GateDrift + RiverCourse.BendDrift + 1e-4f,
                    "прямой проток отклонился от оси больше инварианта GateDrift + BendDrift");
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
            var tile = RiverTile(new HexCoord(2, 1), TurnMask);
            var mesh = RiverMeshBuilder.Build(tile, 1f);
            var axis = Centerline(mesh);

            Assert.GreaterOrEqual(axis.Length, 5, "дуга должна быть разбита на отрезки");
            Assert.AreEqual(0f, Vector2.Distance(RiverCourse.Gate(tile.Coord, 0), axis[0]), 1e-4f,
                "дуга начинается не в воротах грани");
            Assert.AreEqual(0f, Vector2.Distance(RiverCourse.Gate(tile.Coord, 2), axis[^1]), 1e-4f,
                "дуга кончается не в воротах грани");

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
                axis[i] = (Plane(left) + Plane(right)) * 0.5f;
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
            var mesh = RiverMeshBuilder.Build(RiverTile(new HexCoord(0, -4), TurnMask), 1f);
            foreach (var vertex in mesh.vertices)
                Assert.Greater(
                    Plane(vertex).magnitude, 1e-4f,
                    "в центре плитки лежит вершина пятачка, а на повороте его быть не должно");
        }

        /// <summary>А тупику пятачок нужен: он скругляет обрубленный конец.</summary>
        [Test]
        public void DeadEnd_KeepsTheHub()
        {
            var mesh = RiverMeshBuilder.Build(RiverTile(new HexCoord(4, 4), 1), 1f);

            var closest = float.MaxValue;
            foreach (var vertex in mesh.vertices)
                closest = Mathf.Min(closest, Plane(vertex).magnitude);

            Assert.AreEqual(0f, closest, 1e-4f, "у тупика нет вершины в центре — пятачок пропал");
        }

        /// <summary>Лента лежит на земле, значит её нормаль смотрит вверх, а не в камеру.</summary>
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(StraightMask)]
        [TestCase(TurnMask)]
        [TestCase(BranchMask)]
        [TestCase(0x3f)]
        public void Triangles_FaceUp(int linkMask)
        {
            var mesh = RiverMeshBuilder.Build(RiverTile(new HexCoord(-1, 2), linkMask), 1f);
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;

            Assert.Greater(triangles.Length, 0);
            for (var i = 0; i < triangles.Length; i += 3)
            {
                var a = vertices[triangles[i]];
                var b = vertices[triangles[i + 1]];
                var c = vertices[triangles[i + 2]];
                var up = Vector3.Cross(b - a, c - a).y;

                Assert.Greater(up, 0f,
                    $"маска {linkMask:X2}: треугольник {i / 3} смотрит вниз и будет срезан backface culling");
            }
        }

        /// <summary>Вся лента лежит ровно на земле: высота не должна появиться сама собой.</summary>
        [Test]
        public void Ribbon_LiesFlatOnTheGround()
        {
            var mesh = RiverMeshBuilder.Build(RiverTile(new HexCoord(3, -3), 0x3f), 1f);
            foreach (var vertex in mesh.vertices)
                Assert.AreEqual(0f, vertex.y, 1e-4f, "вершина ленты оторвалась от земли");
        }

        /// <summary>
        /// Ворота — общая точка двух соседей: с какой бы стороны их ни спросили, мировая точка
        /// обязана совпасть. Без этого теста шов расходится молча при первом же меандре.
        /// </summary>
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void GateMatchesFromBothSides(int direction)
        {
            var tile = new HexCoord(2, -1);
            var neighbor = tile.Neighbor(direction);
            var opposite = (direction + 3) % 6;

            var fromTile = tile.ToPlane() + RiverCourse.Gate(tile, direction);
            var fromNeighbor = neighbor.ToPlane() + RiverCourse.Gate(neighbor, opposite);

            Assert.AreEqual(fromTile.x, fromNeighbor.x, 1e-4f, "ворота разошлись по X");
            Assert.AreEqual(fromTile.y, fromNeighbor.y, 1e-4f, "ворота разошлись по Y");
        }

        /// <summary>
        /// Инвариант сноса из плана M22: `|GateDrift| + полуширина берега ≤ полудлина грани − 0.02`
        /// на худшем случае ширины (устье). Проверяется не константой, а формулой — иначе будущая
        /// правка `RiverWidth` могла бы молча вывести ленту за угол на соседа.
        /// </summary>
        [Test]
        public void GateDrift_NeverCrossesTowardTheCorner()
        {
            var halfEdge = HexCoord.Size * 0.5f;
            var worstBankHalf = (RiverWidth.MouthWidth + RiverWidth.BankMargin) * 0.5f;
            var bound = halfEdge - worstBankHalf - 0.02f;

            for (var q = -3; q <= 3; q++)
            for (var r = -3; r <= 3; r++)
            for (var direction = 0; direction < HexCoord.Directions.Count; direction++)
            {
                var tile = new HexCoord(q, r);
                var edgeMid = HexCoord.Directions[direction].ToPlane() * 0.5f;
                var drift = Vector2.Distance(RiverCourse.Gate(tile, direction), edgeMid);

                Assert.LessOrEqual(drift, bound + 1e-4f,
                    $"{tile} направление {direction}: снос ворот вылезает за инвариант ширины берега");
            }
        }

        /// <summary>
        /// Ширина на воротах одинакова у обеих плиток шва: соседи по руслу отличаются потоком
        /// ровно на единицу, и полушаг в каждую сторону (`RiverWidth.GateFlow`) даёт одно число.
        /// </summary>
        [Test]
        public void WidthAtTheGate_MatchesOnBothSidesOfTheSeam()
        {
            var upstream = RiverTile(new HexCoord(0, 0), 1 << 0, flow: 3, downMask: 1 << 0);
            var downstream = RiverTile(upstream.Coord.Neighbor(0), 1 << 3, flow: 4, downMask: 0);

            var widthAtUpstream = RiverWidth.Water(RiverWidth.GateFlow(upstream, 0));
            var widthAtDownstream = RiverWidth.Water(RiverWidth.GateFlow(downstream, 3));

            Assert.AreEqual(widthAtUpstream, widthAtDownstream, 1e-4f,
                "ширина шва разошлась у соседей, отличающихся потоком на единицу");
        }

        /// <summary>Плоская координата вершины: земля — это XZ.</summary>
        static Vector2 Plane(Vector3 vertex) => new(vertex.x, vertex.z);
    }
}
