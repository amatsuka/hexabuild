using System.Collections.Generic;
using Game.Grid;
using Game.Roads;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Тесты насыпи. До M20 дорога была плоской лентой на плитке, и тесты стерегли ровно это:
    /// лежит на земле, не выходит за гекс, все треугольники смотрят вверх. В M20 контракт
    /// переписан — насыпь обязана выйти в шов между плитками, наклониться на перепаде и иметь
    /// боковые грани, — поэтому старые проверки не «починены», а заменены.
    /// </summary>
    public sealed class RoadMeshBuilderTests
    {
        const float Width = 0.24f;
        const float Half = Width * 0.5f;
        const float Flat = 0.12f;

        /// <summary>Насколько речная плитка утоплена: то же число, что `TileView.RiverSink`.</summary>
        const float RiverSink = 0.05f;

        /// <summary>Ось цепочки (0,0) — (0,1) — (0,2): направления соседей единичные.</summary>
        static readonly Vector2 Axis = new HexCoord(0, 1).ToPlane();

        Mesh mesh;
        Mesh masonry;
        Mesh timber;

        [SetUp]
        public void SetUp()
        {
            mesh = new Mesh();
            masonry = new Mesh();
            timber = new Mesh();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(mesh);
            Object.DestroyImmediate(masonry);
            Object.DestroyImmediate(timber);
        }

        static HexMap Map(int rows = 10)
        {
            var tiles = new List<TileData>();
            foreach (var coord in HexMap.CoordsInFlare(rows))
                tiles.Add(new TileData(coord, coord == HexCoord.Zero));

            return new HexMap(rows, tiles);
        }

        /// <summary>Прямая цепочка от Метрополии: (0,1) — сквозной проезд, (0,2) — тупик.</summary>
        static RoadNetwork Chain()
        {
            var network = new RoadNetwork(Map());
            network.Build(new HexCoord(0, 1));
            network.Build(new HexCoord(0, 2));
            return network;
        }

        static System.Func<HexCoord, RoadGround> Heights(params (int Q, int R, float Height)[] raised)
        {
            var map = new Dictionary<HexCoord, float>();
            foreach (var (q, r, height) in raised)
                map[new HexCoord(q, r)] = height;

            return coord => new RoadGround(map.TryGetValue(coord, out var height) ? height : Flat);
        }

        /// <summary>
        /// Река на плитке <paramref name="at"/>: она садится ниже соседей, как её сажает
        /// `TileView`, и дорога переходит её мостом.
        /// </summary>
        static System.Func<HexCoord, RoadGround> River(HexCoord at, BridgeKind kind)
        {
            return coord => coord == at
                ? new RoadGround(Flat - RiverSink, kind)
                : new RoadGround(Flat);
        }

        /// <summary>Координата вершины вдоль оси цепочки: центр Метрополии — ноль, (0,1) — единица.</summary>
        static float Along(Vector3 vertex) => Vector2.Dot(new Vector2(vertex.x, vertex.z), Axis);

        /// <summary>Отступ вершины от оси цепочки вбок.</summary>
        static float Across(Vector3 vertex) =>
            Vector2.Dot(new Vector2(vertex.x, vertex.z), new Vector2(-Axis.y, Axis.x));

        [Test]
        public void EmptyNetwork_MakesAnEmptyMesh()
        {
            RoadMeshBuilder.Build(mesh, masonry, timber, new RoadNetwork(Map()), Heights(), Width);

            Assert.AreEqual(0, mesh.vertexCount);
        }

        /// <summary>
        /// Хвост из диагноза стадии: лента доходила только до инрадиуса крышки и последние 0.078
        /// висели над канавкой фаски. Насыпь обязана дойти ровно до середины общей грани — там её
        /// встречает соседняя плитка, — и не залезть под Метрополию, где дороги нет.
        /// </summary>
        [Test]
        public void Road_RunsFromTheMetropolisSeamToTheDeadEnd()
        {
            RoadMeshBuilder.Build(mesh, masonry, timber, Chain(), Heights(), Width);

            var start = float.MaxValue;
            var finish = float.MinValue;
            var seam = float.MaxValue;
            foreach (var vertex in mesh.vertices)
            {
                // Меряем по полотну: откосы уходят наружу и за его кромку, это их работа.
                if (vertex.y < Flat + RoadMeshBuilder.SurfaceLift - 1e-4f)
                    continue;

                start = Mathf.Min(start, Along(vertex));
                finish = Mathf.Max(finish, Along(vertex));
                seam = Mathf.Min(seam, Mathf.Abs(Along(vertex) - 1.5f));
            }

            Assert.AreEqual(0.5f, start, 1e-4f, "насыпь не дошла до грани с Метрополией или залезла под неё");
            Assert.AreEqual(0f, seam, 1e-4f, "на шве между (0,1) и (0,2) нет ни одной вершины");
            Assert.AreEqual(2f + Half, finish, 1e-4f, "тупик не закруглён пятачком за концом луча");
        }

        /// <summary>
        /// Вторая половина того же хвоста: мало дойти до шва, надо закрыть канавку. Тело насыпи
        /// на шве обязано уйти ниже дна канавки, иначе под дорогой останется щель.
        /// </summary>
        [Test]
        public void Body_SinksBelowTheBevelGroove()
        {
            RoadMeshBuilder.Build(mesh, masonry, timber, Chain(), Heights(), Width);

            var lowest = float.MaxValue;
            foreach (var vertex in mesh.vertices)
                if (Mathf.Abs(Along(vertex) - 1.5f) < 1e-3f)
                    lowest = Mathf.Min(lowest, vertex.y);

            Assert.LessOrEqual(lowest, Flat - HexMeshBuilder.BevelDrop + 1e-4f,
                "подошва насыпи повисла над канавкой фаски");
        }

        /// <summary>Полотно лежит на своей высоте над крышкой плитки, а не на самой крышке.</summary>
        [Test]
        public void Surface_RidesAboveTheTile()
        {
            RoadMeshBuilder.Build(mesh, masonry, timber, Chain(), Heights(), Width);

            var highest = float.MinValue;
            foreach (var vertex in mesh.vertices)
                highest = Mathf.Max(highest, vertex.y);

            Assert.AreEqual(Flat + RoadMeshBuilder.SurfaceLift, highest, 1e-4f);
        }

        /// <summary>
        /// На перепаде шов встаёт на высоту старшей плитки: провалить полотно до средней значило бы
        /// врезать дорогу в склон, а крышку плитки мы не режем. Подъём достаётся нижней плитке.
        /// </summary>
        [Test]
        public void Seam_StandsOnTheHigherTile()
        {
            const float raised = 0.33f;
            RoadMeshBuilder.Build(mesh, masonry, timber, Chain(), Heights((0, 2, raised)), Width);

            var atSeam = float.MinValue;
            foreach (var vertex in mesh.vertices)
                if (Mathf.Abs(Along(vertex) - 1.5f) < 1e-3f)
                    atSeam = Mathf.Max(atSeam, vertex.y);

            Assert.AreEqual(raised + RoadMeshBuilder.SurfaceLift, atSeam, 1e-4f,
                "полотно на шве село ниже старшей плитки и врезалось в её склон");
        }

        /// <summary>
        /// Диагноз стадии: плоская лента нормалью вверх не берёт ни бокового света, ни SSAO.
        /// У насыпи обязаны быть боковые грани — откосы, иначе объёма нет и стадия ни при чём.
        /// </summary>
        [Test]
        public void Slopes_FaceSideways_SoTheRoadCatchesTheLight()
        {
            RoadMeshBuilder.Build(mesh, masonry, timber, Chain(), Heights(), Width);

            var sideways = 0;
            foreach (var normal in Normals(mesh))
                if (Mathf.Abs(normal.y) < 0.8f)
                    sideways++;

            Assert.Greater(sideways, 0, "у насыпи нет ни одной боковой грани — это снова плоская лента");
        }

        /// <summary>Ширина полотна одна на всю сеть: разброса по плиткам больше нет.</summary>
        [Test]
        public void Surface_KeepsOneWidthAlongTheRoute()
        {
            RoadMeshBuilder.Build(mesh, masonry, timber, Chain(), Heights(), Width);

            var widest = 0f;
            foreach (var vertex in mesh.vertices)
            {
                if (vertex.y < Flat + RoadMeshBuilder.SurfaceLift - 1e-4f)
                    continue;

                widest = Mathf.Max(widest, Mathf.Abs(Across(vertex)));
            }

            Assert.AreEqual(Half, widest, 1e-4f, "полотно шире или уже заказанного");
        }

        /// <summary>
        /// Узел — одна площадка. Раньше три направления давали три попарные дуги на одном `y`:
        /// компланарное перекрытие в центре плитки. Проверяем прямо это — никакая точка одной
        /// горизонтальной грани не лежит внутри другой.
        /// </summary>
        [Test]
        public void Junction_LaysOneSurface_NotOverlappingArcs()
        {
            var network = new RoadNetwork(Map());
            network.Build(new HexCoord(0, 1));
            network.Build(new HexCoord(0, 2));
            network.Build(new HexCoord(1, 1));

            RoadMeshBuilder.Build(mesh, masonry, timber, network, Heights(), Width);

            var surface = SurfaceTriangles(mesh);
            Assert.Greater(surface.Count, 0);

            for (var i = 0; i < surface.Count; i++)
            for (var j = 0; j < surface.Count; j++)
            {
                if (i == j)
                    continue;

                var centroid = (surface[i][0] + surface[i][1] + surface[i][2]) / 3f;
                Assert.IsFalse(
                    Inside(surface[j], centroid),
                    $"полотно {i} лежит поверх полотна {j}: узел собран из наложенных кусков");
            }
        }

        /// <summary>
        /// У Метрополии своей дороги нет, и луч там обрывается. Открытый торец сквозь backface
        /// culling выглядел бы дырой в насыпи, поэтому он обязан быть закрыт гранью наружу.
        /// </summary>
        [Test]
        public void ArmAtTheMetropolis_IsCapped()
        {
            RoadMeshBuilder.Build(mesh, masonry, timber, Chain(), Heights(), Width);

            var outward = new Vector3(-Axis.x, 0f, -Axis.y);
            var capped = false;
            foreach (var normal in Normals(mesh))
                capped |= Vector3.Dot(normal, outward) > 0.99f;

            Assert.IsTrue(capped, "торец луча у Метрополии открыт");
        }

        /// <summary>
        /// Малый перепад остаётся пандусом: одна наклонная плоскость, ни одного вертикального
        /// подступенка. Иначе лестница полезла бы на каждую вторую грань поля.
        /// </summary>
        [Test]
        public void DropUnderTheThreshold_StaysARamp()
        {
            RoadMeshBuilder.Build(mesh, masonry, timber, Chain(), Heights((0, 2, Flat + RoadMeshBuilder.RampThreshold - 0.01f)), Width);

            Assert.IsEmpty(Risers(mesh), "на пологом перепаде выросли ступени");
        }

        /// <summary>
        /// Выше порога луч становится лестницей. Число ступеней задаёт высота одной: подъём
        /// делится на неё с округлением вверх, и ни один подступенок не выходит за неё.
        /// </summary>
        [Test]
        public void DropOverTheThreshold_BecomesStairs()
        {
            const float raised = 0.33f;
            var rise = raised - Flat;
            var expected = Mathf.CeilToInt(rise / RoadMeshBuilder.StepRise);

            RoadMeshBuilder.Build(mesh, masonry, timber, Chain(), Heights((0, 2, raised)), Width);
            var risers = Risers(mesh);

            Assert.AreEqual(expected, risers.Count, "ступеней вышло не столько, сколько просит подъём");

            var climbed = 0f;
            foreach (var riser in risers.Values)
            {
                var step = riser.High - riser.Low;
                Assert.LessOrEqual(step, RoadMeshBuilder.StepRise + 1e-4f, $"подступенок {step:F3} выше ступени");
                climbed += step;
            }

            Assert.AreEqual(rise, climbed, 1e-3f, "лестница подняла дорогу не на весь перепад");
        }

        /// <summary>
        /// Под лестницей откос заменяется подпорной стенкой: бок уходит вниз вертикально, а не
        /// расползается наружу. Контроль — тот же луч на ровном месте, где вынос откоса на месте.
        /// </summary>
        [Test]
        public void Stairs_StandOnARetainingWall_NotOnASlope()
        {
            // Замер идёт по нутру луча: на самом шве стоит встречное сечение соседней плитки,
            // а у неё подъёма нет и откос на месте.
            RoadMeshBuilder.Build(mesh, masonry, timber, Chain(), Heights((0, 2, 0.33f)), Width);
            var walled = WidestBetween(mesh, 1.05f, 1.45f);

            RoadMeshBuilder.Build(mesh, masonry, timber, Chain(), Heights(), Width);
            var sloped = WidestBetween(mesh, float.MinValue, float.MaxValue);

            Assert.AreEqual(Half, walled, 1e-4f, "у лестницы бок расползся откосом вместо стенки");
            Assert.Greater(sloped, Half + 1e-4f, "на ровном месте откос пропал — сравнивать не с чем");
        }

        /// <summary>
        /// Кладка живёт своим мешем и своей геометрией: не пустая, не наклейка (у неё есть
        /// боковые грани) и целиком лежит над полотном, а не под ним.
        /// </summary>
        [Test]
        public void Masonry_IsItsOwnBodyOnTopOfTheRoad()
        {
            RoadMeshBuilder.Build(mesh, masonry, timber, Chain(), Heights(), Width);

            Assert.Greater(masonry.vertexCount, 0, "кладки не выложено вовсе");

            var sideways = 0;
            foreach (var normal in Normals(masonry))
                if (Mathf.Abs(normal.y) < 0.5f)
                    sideways++;

            Assert.Greater(sideways, 0, "у кладки нет боковых граней — это снова наклейка");

            foreach (var vertex in masonry.vertices)
                Assert.Greater(vertex.y, Flat, "кладка провалилась под крышку плитки");
        }

        /// <summary>
        /// Кладка стоит на дороге, а не рядом: всё, что она кладёт, помещается в габарит насыпи
        /// вместе с откосом. Камень, уехавший на соседнюю плитку, читался бы просто мусором.
        /// </summary>
        [Test]
        public void Masonry_StaysWithinTheRoadbed()
        {
            RoadMeshBuilder.Build(mesh, masonry, timber, Chain(), Heights(), Width);
            var limit = WidestBetween(mesh, float.MinValue, float.MaxValue);

            foreach (var vertex in masonry.vertices)
                Assert.LessOrEqual(Mathf.Abs(Across(vertex)), limit + 1e-4f, "кладка вылезла за насыпь");
        }

        /// <summary>
        /// Разброс кладки держится за луч, а не за плитку: та же сеть даёт ту же кладку, а разные
        /// лучи — разную. Иначе дорога снова выглядит штампованной, только штамп мельче.
        /// </summary>
        [Test]
        public void Masonry_IsStableAndNotRepeatedTileByTile()
        {
            RoadMeshBuilder.Build(mesh, masonry, timber, Chain(), Heights(), Width);
            var first = masonry.vertices;

            RoadMeshBuilder.Build(mesh, masonry, timber, Chain(), Heights(), Width);
            Assert.AreEqual(first, masonry.vertices, "кладка перекладывается при каждой пересборке");

            // Луч (0,1) → Метрополия и луч (0,1) → (0,2) идут по одной оси в разные стороны:
            // одинаковая раскладка на них означала бы, что хеш не видит направления.
            var toMetropolis = 0;
            var toDeadEnd = 0;
            foreach (var vertex in first)
            {
                if (Along(vertex) < 1f)
                    toMetropolis++;
                else
                    toDeadEnd++;
            }

            Assert.Greater(toMetropolis, 0, "на луче к Метрополии кладки нет вовсе");
            Assert.AreNotEqual(toMetropolis, toDeadEnd, "оба луча получили одну и ту же кладку");
        }

        /// <summary>На лестнице кладки нет: плашка на переломе ступени торчала бы в воздух.</summary>
        [Test]
        public void Stairs_CarryNoMasonry()
        {
            RoadMeshBuilder.Build(mesh, masonry, timber, Chain(), Heights((0, 2, 0.33f)), Width);

            foreach (var vertex in masonry.vertices)
                Assert.IsFalse(
                    Along(vertex) > 1.05f && Along(vertex) < 1.45f,
                    "на ступенях выложена кладка");
        }

        /// <summary>
        /// Подступенки на луче (0,1) — (0,2): вертикальные грани лицом назад по ходу подъёма.
        /// Ключ — положение вдоль оси, по нему грани одной ступени собираются вместе.
        /// </summary>
        /// <summary>
        /// Хвост шага 1: до шага 4 дорога переходила реку насыпью — та же земляная лента шла
        /// прямо по руслу. Теперь на речной плитке насыпи остаются только устои у швов,
        /// а середину занимает настил.
        /// </summary>
        [Test]
        public void Bridge_TakesTheEmbankmentOffTheRiver()
        {
            RoadMeshBuilder.Build(mesh, masonry, timber, Chain(), River(new HexCoord(0, 1), BridgeKind.Timber), Width);

            foreach (var vertex in mesh.vertices)
                Assert.IsFalse(
                    Along(vertex) > 0.62f && Along(vertex) < 1.38f,
                    $"насыпь осталась над руслом: вершина на {Along(vertex):0.###} вдоль оси");

            Assert.Greater(timber.vertexCount, 0, "моста на речной плитке нет вовсе");
        }

        /// <summary>
        /// Устой — это стык, а не украшение: соседняя плитка доводит полотно до середины общей
        /// грани, и встретить его надо на той же высоте, иначе на шве останется ступенька.
        /// </summary>
        [Test]
        public void Abutment_KeepsTheSeamAtTheRoadbedHeight()
        {
            RoadMeshBuilder.Build(mesh, masonry, timber, Chain(), River(new HexCoord(0, 1), BridgeKind.Timber), Width);

            var top = float.MinValue;
            foreach (var vertex in mesh.vertices)
                if (Along(vertex) >= 0.5f && Along(vertex) <= 0.62f)
                    top = Mathf.Max(top, vertex.y);

            Assert.AreEqual(Flat + RoadMeshBuilder.SurfaceLift, top, 1e-4f, "устой встал не на высоту шва");
        }

        /// <summary>
        /// Подъём в середине: настил не повторяет утопленную плитку, а идёт над ней горбом.
        /// Меряем по самой высокой точке в двух полосах — над серединой и у устоев: перила идут
        /// вдоль настила и разница между ними и есть подъём.
        /// </summary>
        [Test]
        public void Deck_RisesInTheMiddleOverTheRiver()
        {
            RoadMeshBuilder.Build(mesh, masonry, timber, Chain(), River(new HexCoord(0, 1), BridgeKind.Timber), Width);

            var middle = float.MinValue;
            var ends = float.MinValue;
            foreach (var vertex in timber.vertices)
            {
                var offset = Mathf.Abs(Along(vertex) - 1f);
                if (offset < 0.05f)
                    middle = Mathf.Max(middle, vertex.y);
                else if (offset > 0.35f)
                    ends = Mathf.Max(ends, vertex.y);
            }

            Assert.AreEqual(BridgeMeshBuilder.Camber, middle - ends, 1e-3f, "настил идёт ровно, горба нет");
        }

        /// <summary>Сваи затем и нужны, чтобы настил стоял на дне, а не висел над ним.</summary>
        [Test]
        public void Piles_ReachTheRiverbed()
        {
            RoadMeshBuilder.Build(mesh, masonry, timber, Chain(), River(new HexCoord(0, 1), BridgeKind.Timber), Width);

            var lowest = float.MaxValue;
            foreach (var vertex in timber.vertices)
                lowest = Mathf.Min(lowest, vertex.y);

            Assert.Less(lowest, Flat - RiverSink, "сваи не дошли до русла");
        }

        /// <summary>
        /// Скальной плитке достаётся каменная арка, и уходит она в меш кладки: цвет в этом
        /// проекте живёт на рендерере, и дерево от камня одним мешем не отличить.
        /// </summary>
        [Test]
        public void StoneBridge_GoesToTheMasonryMesh()
        {
            RoadMeshBuilder.Build(mesh, masonry, timber, Chain(), River(new HexCoord(0, 1), BridgeKind.Stone), Width);

            Assert.AreEqual(0, timber.vertexCount, "на скальной плитке появился деревянный настил");

            var overTheRiver = 0;
            foreach (var vertex in masonry.vertices)
                if (Along(vertex) > 0.62f && Along(vertex) < 1.38f)
                    overTheRiver++;

            Assert.Greater(overTheRiver, 0, "арки над руслом нет");
        }

        /// <summary>
        /// Арка на то и арка: у замка под ней просвет, а пята уходит в русло. Без первого это
        /// плита, без второго — плита на весу.
        /// </summary>
        [Test]
        public void Arch_OpensUnderTheDeck()
        {
            RoadMeshBuilder.Build(mesh, masonry, timber, Chain(), River(new HexCoord(0, 1), BridgeKind.Stone), Width);

            var bed = Flat - RiverSink;
            var crown = float.MaxValue;
            var lowest = float.MaxValue;
            foreach (var vertex in masonry.vertices)
            {
                lowest = Mathf.Min(lowest, vertex.y);
                if (Mathf.Abs(Along(vertex) - 1f) < 1e-3f)
                    crown = Mathf.Min(crown, vertex.y);
            }

            Assert.Greater(crown, bed, "под замком арки нет просвета");
            Assert.Less(lowest, bed, "пята арки не встала в русло");
        }

        /// <summary>
        /// Мост не насыпь, но правило то же: за свою плитку он не выходит. Вдоль — до устоев,
        /// поперёк — в габарите настила вместе с перилами.
        /// </summary>
        [Test]
        public void Bridge_StaysInsideTheTile()
        {
            RoadMeshBuilder.Build(mesh, masonry, timber, Chain(), River(new HexCoord(0, 1), BridgeKind.Timber), Width);

            foreach (var vertex in timber.vertices)
            {
                Assert.That(
                    Along(vertex),
                    Is.InRange(1f - BridgeMeshBuilder.DeckReach - 0.05f, 1f + BridgeMeshBuilder.DeckReach + 0.05f),
                    "мост вылез за устои вдоль дороги");

                Assert.LessOrEqual(
                    Mathf.Abs(Across(vertex)),
                    Half + BridgeMeshBuilder.Overhang + 0.02f,
                    "мост вылез за габарит настила поперёк дороги");
            }
        }

        static Dictionary<int, (float Low, float High)> Risers(Mesh mesh)
        {
            var back = new Vector3(-Axis.x, 0f, -Axis.y);
            var risers = new Dictionary<int, (float Low, float High)>();
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;

            for (var i = 0; i < triangles.Length; i += 3)
            {
                var a = vertices[triangles[i]];
                var b = vertices[triangles[i + 1]];
                var c = vertices[triangles[i + 2]];

                var cross = Vector3.Cross(b - a, c - a);
                if (cross.sqrMagnitude < 1e-12f || Vector3.Dot(cross.normalized, back) < 0.99f)
                    continue;

                var along = (Along(a) + Along(b) + Along(c)) / 3f;
                if (along < 1.05f || along > 1.55f)
                    continue;

                var key = Mathf.RoundToInt(along * 1000f);
                var low = Mathf.Min(a.y, Mathf.Min(b.y, c.y));
                var high = Mathf.Max(a.y, Mathf.Max(b.y, c.y));

                risers[key] = risers.TryGetValue(key, out var known)
                    ? (Mathf.Min(known.Low, low), Mathf.Max(known.High, high))
                    : (low, high);
            }

            return risers;
        }

        /// <summary>Самый широкий вылет вбок на отрезке оси: полотно, стенка или подошва откоса.</summary>
        static float WidestBetween(Mesh mesh, float from, float to)
        {
            var widest = 0f;
            foreach (var vertex in mesh.vertices)
                if (Along(vertex) > from && Along(vertex) < to)
                    widest = Mathf.Max(widest, Mathf.Abs(Across(vertex)));

            return widest;
        }

        /// <summary>Нормали граней меша: по три вершины на треугольник, вырожденные пропускаем.</summary>
        static IEnumerable<Vector3> Normals(Mesh mesh)
        {
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;

            for (var i = 0; i < triangles.Length; i += 3)
            {
                var a = vertices[triangles[i]];
                var cross = Vector3.Cross(vertices[triangles[i + 1]] - a, vertices[triangles[i + 2]] - a);
                if (cross.sqrMagnitude > 1e-12f)
                    yield return cross.normalized;
            }
        }

        /// <summary>Горизонтальные грани, спроецированные на землю: это и есть полотно.</summary>
        static List<Vector2[]> SurfaceTriangles(Mesh mesh)
        {
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            var surface = new List<Vector2[]>();

            for (var i = 0; i < triangles.Length; i += 3)
            {
                var a = vertices[triangles[i]];
                var b = vertices[triangles[i + 1]];
                var c = vertices[triangles[i + 2]];
                var cross = Vector3.Cross(b - a, c - a);
                if (cross.sqrMagnitude < 1e-12f || cross.normalized.y < 0.99f)
                    continue;

                surface.Add(new[] { Plane(a), Plane(b), Plane(c) });
            }

            return surface;
        }

        static Vector2 Plane(Vector3 vertex) => new(vertex.x, vertex.z);

        /// <summary>Точка строго внутри треугольника: касание по ребру за перекрытие не считаем.</summary>
        static bool Inside(Vector2[] triangle, Vector2 point)
        {
            var first = Side(triangle[0], triangle[1], point);
            var second = Side(triangle[1], triangle[2], point);
            var third = Side(triangle[2], triangle[0], point);

            return (first > 1e-5f && second > 1e-5f && third > 1e-5f)
                || (first < -1e-5f && second < -1e-5f && third < -1e-5f);
        }

        static float Side(Vector2 from, Vector2 to, Vector2 point) =>
            (to.x - from.x) * (point.y - from.y) - (to.y - from.y) * (point.x - from.x);
    }
}
