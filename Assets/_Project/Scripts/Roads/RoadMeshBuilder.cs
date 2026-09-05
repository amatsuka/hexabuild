using System;
using System.Collections.Generic;
using Game.Grid;
using UnityEngine;

namespace Game.Roads
{
    /// <summary>
    /// Насыпь всей дорожной сети одним мешем в мировых координатах.
    ///
    /// Дорога больше не лента: у неё трапециевидное сечение — плоское полотно сверху и откосы
    /// вниз-наружу, поэтому она берёт боковой свет и SSAO, как призмы плиток вокруг. Тело
    /// продолжается ниже земли и там прячется внутри плитки: всё, что ниже крышки, закрыто
    /// её непрозрачной поверхностью, зато на шве между плитками откос выходит наружу и
    /// закрывает канавку фаски. Отсюда и «драпировка»: полотно идёт поверху ровно, а низ тела
    /// проваливается в шов вслед за землёй.
    ///
    /// Меш один на сеть, а не на плитку: дорога обязана пересекать границу гекса, иначе шов
    /// между соседями ничем не закрыть. Сеть — дерево родителей `RoadNetwork`, поэтому каждый
    /// участок маршрута рисуется ровно один раз, каждой плиткой до середины общей грани.
    ///
    /// Узел — одна площадка, а не три наложенные дуги: пятачок отодвигает начала лучей ровно
    /// настолько, чтобы соседние лучи не наезжали друг на друга, и замыкает их одним полигоном.
    ///
    /// Речную плитку насыпь не переходит: там дорога встаёт на мост, и её геометрию строит
    /// `BridgeMeshBuilder`. Насыпи на такой плитке достаются только устои у швов.
    /// </summary>
    public static class RoadMeshBuilder
    {
        /// <summary>
        /// Насколько полотно поднято над крышкой плитки. Это же число — высота, на которой
        /// едет груз: `RoadPath` кладёт осевую линию на полотно.
        ///
        /// Оно же решает, видно ли объём: наружу торчит ровно этот кусок откоса, остальное
        /// закопано в плитку. На 0.035 откос выходил в два-три пикселя и дорога читалась плоской
        /// заливкой — подобрано по кадру, число стартовое.
        /// </summary>
        public const float SurfaceLift = 0.05f;

        /// <summary>Глубина откоса: от кромки полотна вниз до подошвы насыпи.</summary>
        const float SlopeDrop = 0.10f;

        /// <summary>Вынос откоса наружу на всю его глубину. Меньше — стенка, больше — блин.</summary>
        const float SlopeRun = 0.055f;

        /// <summary>
        /// Насколько подошва уходит ниже земли под собой. Нужен только на шве: там земля
        /// проваливается в канавку фаски, и без запаса подошва повисла бы над ней.
        /// </summary>
        const float BuryMargin = 0.04f;

        /// <summary>
        /// Подъём на луче, выше которого пандус сменяется лестницей.
        ///
        /// Число из замера, а не с потолка: на 200 сгенерированных картах 28281 пара соседних
        /// проходимых плиток, и перепад между ними не больше 0.08 у 90.2% из них (не больше 0.05
        /// у 74.6%, не больше 0.12 у 97.6%, максимум 0.23). Порог 0.08 оставляет пандус
        /// подавляющему большинству швов и отдаёт лестнице примерно каждый десятый — редко, но
        /// достаточно часто, чтобы игрок её узнал.
        ///
        /// В уклон это переводится так: подъём укладывается на длину луча, а она от 0.5
        /// у сквозного проезда до ~0.31 у острого узла. На пороге это 16% и 26% — дальше пандус
        /// читается уже горкой, а не дорогой.
        /// </summary>
        public const float RampThreshold = 0.08f;

        /// <summary>
        /// Высота одной ступени. На замеренном разбросе (0.09…0.23 выше порога) даёт от двух
        /// до пяти ступеней, обычно две-три. Одной ступени не бывает: порог 0.08 больше её самой.
        /// </summary>
        public const float StepRise = 0.05f;

        /// <summary>Звеньев в закруглении тупика: полукруг за обрубленным концом луча.</summary>
        const int DeadEndSegments = 6;

        /// <summary>Шаг колейных плашек вдоль луча и доля мест, где плашка всё-таки ложится.</summary>
        const float PlateStep = 0.13f;

        const float PlateChance = 0.45f;

        /// <summary>Полудлина плашки вдоль колеи, полуширина поперёк и высота над полотном.</summary>
        const float PlateLength = 0.045f;

        const float PlateWidth = 0.028f;

        const float PlateRise = 0.014f;

        /// <summary>Половина расстояния между колеями: плашки ложатся парой по обе стороны оси.</summary>
        const float RutOffset = 0.048f;

        /// <summary>Шаг обочных камней и доля мест, где камень всё-таки встаёт.</summary>
        const float StoneStep = 0.14f;

        const float StoneChance = 0.4f;

        /// <summary>Радиус камня у земли, во сколько раз он сужается кверху и его высота.</summary>
        const float StoneRadius = 0.042f;

        const float StoneTaper = 0.7f;

        const float StoneHeight = 0.034f;

        /// <summary>
        /// Насколько камень вынесен за кромку полотна и насколько врос в откос. Утоплен он
        /// сильно: над полотном валун поднимается меньше чем на сантиметр в масштабе гекса,
        /// зато из откоса рядом торчит целым боком. Не утопить — получаются столбики ограждения.
        /// </summary>
        const float StoneOffset = 0.01f;

        const float StoneSink = 0.026f;

        const int PlateSalt = 11;

        const int StoneSalt = 29;

        static readonly List<Vector3> vertices = new();
        static readonly List<int> triangles = new();
        static readonly List<Vector3> stones = new();
        static readonly List<int> stoneTriangles = new();
        static readonly List<Vector3> decks = new();
        static readonly List<int> deckTriangles = new();
        static readonly List<BridgeMeshBuilder.Span> spans = new(6);
        static readonly List<int> arms = new(6);
        static readonly List<Vector3> ring = new(16);
        static readonly List<Vector3> ringOut = new(16);
        static readonly List<bool> ringSkirt = new(16);

        static readonly Comparison<int> byAngle =
            (a, b) => Angle(a).CompareTo(Angle(b));

        /// <summary>
        /// Пересобрать сеть. Мешей три, и делит их не геометрия, а цвет — он в этом проекте живёт
        /// на рендерере: <paramref name="bed"/> — насыпь, <paramref name="masonry"/> — кладка
        /// поверх неё и каменные арки мостов, <paramref name="timber"/> — деревянные настилы.
        /// <paramref name="groundAt"/> отдаёт мировую высоту крышки плитки и мост, если плитку
        /// режет река; отвечать он обязан и за Метрополию: к ней дороги примыкают, но своей
        /// дороги у неё нет. <paramref name="width"/> — ширина полотна; вместе с откосом она
        /// не должна вылезти за крышку плитки, то есть держится в пределах ~0.26.
        /// </summary>
        public static void Build(
            Mesh bed, Mesh masonry, Mesh timber,
            RoadNetwork network, Func<HexCoord, RoadGround> groundAt, float width)
        {
            vertices.Clear();
            triangles.Clear();
            stones.Clear();
            stoneTriangles.Clear();
            decks.Clear();
            deckTriangles.Clear();

            var half = width * 0.5f;
            foreach (var coord in network.Roads)
                AppendTile(network, coord, groundAt, half);

            Fill(bed, vertices, triangles);
            Fill(masonry, stones, stoneTriangles);
            Fill(timber, decks, deckTriangles);
        }

        static void Fill(Mesh mesh, List<Vector3> points, List<int> faces)
        {
            mesh.Clear();
            mesh.SetVertices(points);
            mesh.SetTriangles(faces, 0);

            // Разведка 3D: под Lit-шейдером меш без нормалей чёрный.
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        static void AppendTile(RoadNetwork network, HexCoord coord, Func<HexCoord, RoadGround> groundAt, float half)
        {
            arms.Clear();
            for (var direction = 0; direction < HexCoord.Directions.Count; direction++)
                if (network.IsRouteLink(coord, coord.Neighbor(direction)))
                    arms.Add(direction);

            arms.Sort(byAngle);

            var center = coord.ToPlane();
            var ground = groundAt(coord);

            // Реку дорога переходит мостом, а не насыпью: на речной плитке вместо узла и лучей
            // встают устои у швов и пролёт между ними.
            if (ground.Bridge != BridgeKind.None && arms.Count > 0)
            {
                AppendBridge(network, coord, center, ground, groundAt, half);
                return;
            }

            var radius = NodeRadius(half);

            AppendNode(center, ground.Top, radius, half);

            foreach (var direction in arms)
                AppendArm(network, coord, direction, center, ground.Top, groundAt, radius, half);
        }

        /// <summary>
        /// Мост вместо насыпи на речной плитке.
        ///
        /// У каждого шва остаётся устой — короткий кусок насыпи высотой шва: соседняя плитка
        /// доводит своё полотно до середины общей грани, и его торец нужно чем-то встретить.
        /// Между устоями идёт пролёт, и его середина поднята над самым высоким швом.
        ///
        /// Ширину площадки на развилке считает тот же <see cref="NodeRadius"/>, что разводит
        /// лучи насыпи, но от кромки настила: настил шире полотна, и по узкому радиусу пролёты
        /// наехали бы друг на друга у самого центра.
        /// </summary>
        static void AppendBridge(
            RoadNetwork network, HexCoord coord, Vector2 center,
            RoadGround ground, Func<HexCoord, RoadGround> groundAt, float half)
        {
            spans.Clear();
            var crown = float.MinValue;

            foreach (var direction in arms)
            {
                var neighbor = coord.Neighbor(direction);
                var neighborTop = groundAt(neighbor).Top;
                var seam = Mathf.Max(ground.Top, neighborTop) + SurfaceLift;

                spans.Add(new BridgeMeshBuilder.Span(coord, direction, Plane(direction), seam));
                crown = Mathf.Max(crown, seam);

                AppendAbutment(network, coord, direction, neighbor, center, ground.Top, neighborTop, seam, half);
            }

            var radius = NodeRadius(half + BridgeMeshBuilder.Overhang);
            crown += BridgeMeshBuilder.Camber;

            if (ground.Bridge == BridgeKind.Stone)
                BridgeMeshBuilder.Append(
                    stones, stoneTriangles, ground.Bridge, center, ground.Top, crown, radius, half, spans);
            else
                BridgeMeshBuilder.Append(
                    decks, deckTriangles, ground.Bridge, center, ground.Top, crown, radius, half, spans);
        }

        /// <summary>
        /// Устой: кусок насыпи от шва внутрь плитки, ровный по высоте шва. Подъём ему не нужен —
        /// весь перепад забирает пролёт, — а внутренний торец закрывается: там насыпь обрывается
        /// на середине луча, и без торца сквозь backface culling видно, что она полая.
        /// </summary>
        static void AppendAbutment(
            RoadNetwork network, HexCoord coord, int direction, HexCoord neighbor,
            Vector2 center, float top, float neighborTop, float seam, float half)
        {
            var forward = Plane(direction);
            var side = new Vector3(-forward.y, 0f, forward.x);

            var nearFloor = top - BuryMargin;
            var farFloor = Mathf.Min(top, neighborTop) - HexMeshBuilder.BevelDrop - BuryMargin;

            // Радиус нулевой: устой считается от центра плитки, доля длины и есть доля от 0.5.
            var shape = new Arm(center, forward, side, 0f, nearFloor, farFloor, half, SlopeRun);
            var inner = shape.At(BridgeMeshBuilder.DeckReach / 0.5f, seam);
            var outer = shape.At(1f, seam);

            AppendSection(inner, outer);
            AppendCap(inner, false);

            if (!network.HasRoad(neighbor))
                AppendCap(outer, true);
        }

        /// <summary>
        /// Радиус площадки: столько нужно отступить от центра, чтобы два самых близких луча
        /// разошлись и не наехали друг на друга. У сквозного проезда угол 180° и радиус нулевой —
        /// площадка вырождается, и прямая дорога остаётся прямой без утолщения в центре.
        /// </summary>
        static float NodeRadius(float half)
        {
            if (arms.Count < 2)
                return 0f;

            var narrowest = 180f;
            for (var i = 0; i < arms.Count; i++)
                narrowest = Mathf.Min(narrowest, Vector2.Angle(Plane(arms[i]), Plane(arms[(i + 1) % arms.Count])));

            return half / Mathf.Tan(narrowest * 0.5f * Mathf.Deg2Rad);
        }

        /// <summary>
        /// Площадка узла: полотно — один полигон по кромкам всех лучей, откос — только на
        /// участках между лучами. Там, где к площадке примыкает луч, откоса нет: его несёт сам
        /// луч, и вершины у них общие, так что шва между ними не появляется.
        ///
        /// Земля под узлом плоская — узел целиком лежит внутри крышки плитки, — поэтому
        /// вертикальной подошвы тут не нужно: откос уходит под крышку и там прячется.
        /// </summary>
        static void AppendNode(Vector2 center, float top, float radius, float half)
        {
            ring.Clear();
            ringOut.Clear();
            ringSkirt.Clear();

            var y = top + SurfaceLift;

            if (arms.Count == 0)
            {
                // Дороги без единого участка маршрута не бывает: `CanExtendTo` не даёт построить
                // оторванный кусок. Но пятачок стоит рисовать, а не падать в пустой меш.
                for (var i = 0; i < DeadEndSegments * 2; i++)
                {
                    var angle = Mathf.PI * 2f * i / (DeadEndSegments * 2);
                    Rim(center, y, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), half, true);
                }
            }

            for (var i = 0; i < arms.Count; i++)
            {
                var direction = Plane(arms[i]);
                var side = new Vector2(-direction.y, direction.x);
                var foot = center + direction * radius;

                // Кромки луча: сначала та, что по часовой, потом та, что против, — кольцо
                // обходится против часовой стрелки.
                Add(foot - side * half, -side, y, false);
                Add(foot + side * half, side, y, true);

                // Тупик замыкается полукругом за обрубленным концом: одного луча на полигон мало.
                if (arms.Count != 1)
                    continue;

                for (var step = 1; step < DeadEndSegments; step++)
                {
                    var angle = Mathf.Atan2(side.y, side.x) + Mathf.PI * step / DeadEndSegments;
                    Rim(center, y, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), half, true);
                }
            }

            // Полотно: веер из центра. Обход тот же, что у крышки гекса, — нормаль смотрит вверх.
            var hub = vertices.Count;
            vertices.Add(new Vector3(center.x, y, center.y));
            var first = vertices.Count;
            foreach (var point in ring)
                vertices.Add(point);

            for (var i = 0; i < ring.Count; i++)
            {
                triangles.Add(hub);
                triangles.Add(first + (i + 1) % ring.Count);
                triangles.Add(first + i);
            }

            // Откос по внешним участкам кольца.
            for (var i = 0; i < ring.Count; i++)
            {
                if (!ringSkirt[i])
                    continue;

                var next = (i + 1) % ring.Count;
                AppendSlope(ring[i], ringOut[i], ring[next], ringOut[next]);
            }
        }

        static void Rim(Vector2 center, float y, Vector2 outward, float half, bool skirt) =>
            Add(center + outward * half, outward, y, skirt);

        static void Add(Vector2 point, Vector2 outward, float y, bool skirt)
        {
            ring.Add(new Vector3(point.x, y, point.y));
            ringOut.Add(new Vector3(outward.x, 0f, outward.y));
            ringSkirt.Add(skirt);
        }

        /// <summary>
        /// Четырёхугольник откоса между двумя точками кромки полотна. Наружу и вниз, поэтому
        /// нормаль у него боковая: она и ловит ключевой свет, которого плоской ленте не досталось.
        /// </summary>
        static void AppendSlope(Vector3 from, Vector3 fromOut, Vector3 to, Vector3 toOut)
        {
            var fromFoot = from + fromOut * SlopeRun + Vector3.down * SlopeDrop;
            var toFoot = to + toOut * SlopeRun + Vector3.down * SlopeDrop;

            var first = vertices.Count;
            vertices.Add(from);
            vertices.Add(fromFoot);
            vertices.Add(toFoot);
            vertices.Add(to);

            triangles.Add(first);
            triangles.Add(first + 1);
            triangles.Add(first + 2);
            triangles.Add(first);
            triangles.Add(first + 2);
            triangles.Add(first + 3);
        }

        /// <summary>
        /// Луч от площадки узла до середины общей грани.
        ///
        /// На шве полотно встаёт на высоту **старшей** из двух плиток: провалить его до средней
        /// значило бы врезать дорогу в склон, а крышку плитки мы не режем. Весь подъём поэтому
        /// достаётся лучу нижней плитки, а луч верхней идёт горизонтально.
        ///
        /// Как этот подъём отработан, решает его величина. До <see cref="RampThreshold"/> луч —
        /// один наклонный пандус. Выше — лестница: горизонтальные площадки и вертикальные
        /// подступенки не выше <see cref="StepRise"/>, а откос по бокам заменяется подпорной
        /// стенкой. Пандус везде на большом перепаде выходил почти вертикальным откосом, то есть
        /// той же стенкой, только не признающейся в этом; серпантин на одном гексе не читается.
        /// </summary>
        static void AppendArm(
            RoadNetwork network, HexCoord coord, int direction,
            Vector2 center, float top, Func<HexCoord, RoadGround> groundAt, float radius, float half)
        {
            var neighbor = coord.Neighbor(direction);
            var neighborTop = groundAt(neighbor).Top;

            var forward = Plane(direction);
            var side = new Vector3(-forward.y, 0f, forward.x);

            var bottom = top + SurfaceLift;
            var rise = Mathf.Max(top, neighborTop) - top;

            // Подошва идёт за землёй: под узлом земля плоская, на шве она падает в канавку фаски
            // на глубину фаски от нижней из плиток.
            var nearFloor = top - BuryMargin;
            var farFloor = Mathf.Min(top, neighborTop) - HexMeshBuilder.BevelDrop - BuryMargin;

            var steps = rise > RampThreshold ? Mathf.CeilToInt(rise / StepRise) : 0;

            // Подпорная стенка — тот же откос с нулевым выносом. Отдельной геометрии ей не нужно:
            // лестница и так стоит телом выше земли, и вертикальный бок читается стенкой.
            var run = steps == 0 ? SlopeRun : 0f;

            var shape = new Arm(center, forward, side, radius, nearFloor, farFloor, half, run);
            var previous = shape.At(0f, bottom);

            if (steps == 0)
            {
                var far = shape.At(1f, bottom + rise);
                AppendSection(previous, far);
                previous = far;
            }

            for (var step = 1; step <= steps; step++)
            {
                var t = step / (float)steps;

                // Площадка держит высоту прошлой ступени до её конца, и только там встаёт
                // подступенок: два сечения с одним XZ и разной высотой.
                var tread = shape.At(t, bottom + rise * (step - 1) / steps);
                AppendSection(previous, tread);

                var riser = shape.At(t, bottom + rise * step / steps);
                AppendSection(tread, riser);
                previous = riser;
            }

            // Стык двух дорог каждая сторона строит сама, и на середине грани они сходятся.
            // А у Метрополии своей дороги нет — там луч обрывается, и его надо закрыть торцом,
            // иначе сквозь backface culling видно, что насыпь полая.
            if (!network.HasRoad(neighbor))
                AppendCap(previous, true);

            // Кладка ложится только на пандус. У лестницы свой ритм — площадки и подступенки, —
            // а плашка, попавшая на перелом, торчала бы из него в воздух.
            if (steps == 0)
                AppendMasonry(coord, direction, shape, bottom, rise);
        }

        /// <summary>
        /// Кладка поверх насыпи: колейные плашки на полотне и обочные камни у кромки.
        ///
        /// Нужна она затем, что гладкая насыпь читается отлитой, а курс идёт к дороге, сделанной
        /// руками. Места берутся от хеша по длине дуги, а не по плитке: разброс должен ехать
        /// **вдоль** маршрута, иначе получается ровно тот поплиточный штамп, который стадия сняла.
        /// Хеш считается от координаты, направления луча и номера шага, поэтому кладка не
        /// перекладывается при каждой новой дороге.
        /// </summary>
        static void AppendMasonry(HexCoord coord, int direction, Arm shape, float bottom, float rise)
        {
            var forward = shape.Forward;
            var side = shape.Side;

            var plates = Mathf.FloorToInt(shape.Length / PlateStep);
            for (var i = 0; i < plates; i++)
            {
                if (Hash01(coord, direction, i, PlateSalt) > PlateChance)
                    continue;

                var t = (i + 0.5f) * PlateStep / shape.Length;
                var axis = shape.Axis(t, bottom + rise * t);
                var rut = Hash01(coord, direction, i, PlateSalt + 1) < 0.5f ? RutOffset : -RutOffset;

                AppendPlate(axis + side * rut, forward, side);
            }

            var count = Mathf.FloorToInt(shape.Length / StoneStep);
            for (var i = 0; i < count; i++)
            {
                if (Hash01(coord, direction, i, StoneSalt) > StoneChance)
                    continue;

                var t = (i + 0.5f) * StoneStep / shape.Length;
                var axis = shape.Axis(t, bottom + rise * t);
                var verge = Hash01(coord, direction, i, StoneSalt + 1) < 0.5f ? 1f : -1f;
                var spin = Hash01(coord, direction, i, StoneSalt + 2) * 60f;

                AppendStone(axis + side * (verge * (shape.Half + StoneOffset)) + Vector3.down * StoneSink, spin);
            }
        }

        /// <summary>Плашка в колее: низкая коробка по ходу дороги, низ упирается в полотно.</summary>
        static void AppendPlate(Vector3 center, Vector3 forward, Vector3 side)
        {
            var along = forward * PlateLength;
            var across = side * PlateWidth;
            var up = Vector3.up * PlateRise;

            var backLeft = center - along + across;
            var frontLeft = center + along + across;
            var frontRight = center + along - across;
            var backRight = center - along - across;

            StoneQuad(backLeft + up, frontLeft + up, frontRight + up, backRight + up);
            StoneQuad(backLeft + up, backLeft, frontLeft, frontLeft + up);
            StoneQuad(frontRight + up, frontRight, backRight, backRight + up);
            StoneQuad(frontLeft + up, frontLeft, frontRight, frontRight + up);
            StoneQuad(backRight + up, backRight, backLeft, backLeft + up);
        }

        /// <summary>
        /// Обочный камень: шестигранник, сужающийся кверху. Дна у него нет — он врос в откос,
        /// и снизу его всё равно не видно.
        /// </summary>
        static void AppendStone(Vector3 seat, float spin)
        {
            var first = stones.Count;
            for (var i = 0; i < 6; i++)
            {
                var angle = Mathf.Deg2Rad * (60f * i + spin);
                var ray = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                stones.Add(seat + ray * StoneRadius);
                stones.Add(seat + ray * (StoneRadius * StoneTaper) + Vector3.up * StoneHeight);
            }

            // Бок: обход от следующего угла к текущему, иначе грань смотрит внутрь камня.
            for (var i = 0; i < 6; i++)
            {
                var next = (i + 1) % 6;
                StoneFace(first + next * 2 + 1, first + next * 2, first + i * 2, first + i * 2 + 1);
            }

            // Крышка: веер, обход тот же, что у крышки гекса.
            var hub = stones.Count;
            stones.Add(seat + Vector3.up * StoneHeight);
            for (var i = 0; i < 6; i++)
            {
                stoneTriangles.Add(hub);
                stoneTriangles.Add(first + ((i + 1) % 6) * 2 + 1);
                stoneTriangles.Add(first + i * 2 + 1);
            }
        }

        static void StoneQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            var first = stones.Count;
            stones.Add(a);
            stones.Add(b);
            stones.Add(c);
            stones.Add(d);

            StoneFace(first, first + 1, first + 2, first + 3);
        }

        static void StoneFace(int a, int b, int c, int d)
        {
            stoneTriangles.Add(a);
            stoneTriangles.Add(b);
            stoneTriangles.Add(c);
            stoneTriangles.Add(a);
            stoneTriangles.Add(c);
            stoneTriangles.Add(d);
        }

        /// <summary>
        /// Хеш места кладки. Координата и направление задают луч, номер шага — место на нём,
        /// соль — что именно решается. Один и тот же луч всегда получает одну и ту же кладку.
        /// Тем же хешем раздаёт разброс доскам моста: разброс обязан ехать вдоль маршрута.
        /// </summary>
        internal static float Hash01(HexCoord coord, int direction, int index, int salt)
        {
            unchecked
            {
                var hash = (uint)(coord.Q * 73856093)
                    ^ (uint)(coord.R * 19349663)
                    ^ (uint)(direction * 83492791)
                    ^ (uint)(index * 2654435761)
                    ^ (uint)(salt * 40503);

                hash ^= hash >> 13;
                hash *= 2246822519u;
                hash ^= hash >> 16;
                return hash / (float)uint.MaxValue;
            }
        }

        /// <summary>Луч в своих координатах: <see cref="At"/> режет его поперёк на доле длины.</summary>
        readonly struct Arm
        {
            readonly Vector2 center;
            readonly Vector2 forward;
            readonly Vector3 side;
            readonly float radius;
            readonly float nearFloor;
            readonly float farFloor;
            readonly float half;
            readonly float run;

            public Arm(
                Vector2 center, Vector2 forward, Vector3 side, float radius,
                float nearFloor, float farFloor, float half, float run)
            {
                this.center = center;
                this.forward = forward;
                this.side = side;
                this.radius = radius;
                this.nearFloor = nearFloor;
                this.farFloor = farFloor;
                this.half = half;
                this.run = run;
            }

            /// <summary>Направление луча по земле.</summary>
            public Vector3 Forward => new(forward.x, 0f, forward.y);

            /// <summary>Единичный вектор поперёк луча.</summary>
            public Vector3 Side => side;

            /// <summary>Полуширина полотна.</summary>
            public float Half => half;

            /// <summary>Длина луча по земле: от кромки площадки узла до середины общей грани.</summary>
            public float Length => 0.5f - radius;

            /// <summary>Точка осевой линии на полотне заданной высоты.</summary>
            public Vector3 Axis(float t, float top)
            {
                var point = center + forward * Mathf.Lerp(radius, 0.5f, t);
                return new Vector3(point.x, top, point.y);
            }

            /// <summary>
            /// Поперечное сечение насыпи: кромка полотна, подошва откоса и низ вертикальной юбки.
            /// Юбка нужна там, где земля ушла вниз — на шве и под лестницей; под узлом она
            /// вырождается в ноль, потому что подошва там уже под крышкой плитки.
            /// </summary>
            public Vector3[] At(float t, float top)
            {
                var point = center + forward * Mathf.Lerp(radius, 0.5f, t);
                var floor = Mathf.Lerp(nearFloor, farFloor, t);

                var axis = new Vector3(point.x, top, point.y);
                var skirtY = Mathf.Min(top - SlopeDrop, floor);

                var edge = side * half;
                var foot = side * (half + run);

                return new[]
                {
                    axis + edge,
                    axis + foot + Vector3.down * SlopeDrop,
                    new Vector3(axis.x + foot.x, skirtY, axis.z + foot.z),
                    new Vector3(axis.x - foot.x, skirtY, axis.z - foot.z),
                    axis - foot + Vector3.down * SlopeDrop,
                    axis - edge
                };
            }
        }

        /// <summary>Полотно, два откоса и две юбки между двумя сечениями луча.</summary>
        static void AppendSection(Vector3[] near, Vector3[] far)
        {
            // Полотно: пары «левая, правая», обход как у крышки гекса — нормаль вверх.
            Quad(near[0], far[0], far[5], near[5]);

            // Левая сторона: наружу смотрит та же половина, что и кромка.
            Quad(near[0], near[1], far[1], far[0]);
            Quad(near[1], near[2], far[2], far[1]);

            // Правая — обход зеркальный, иначе откос вывернулся бы внутрь насыпи.
            Quad(near[5], far[5], far[4], near[4]);
            Quad(near[4], far[4], far[3], near[3]);
        }

        /// <summary>
        /// Торец луча: сечение целиком. <paramref name="outward"/> — лицом от центра плитки;
        /// внутренний торец нужен устою моста, там насыпь обрывается на середине луча.
        /// </summary>
        static void AppendCap(Vector3[] section, bool outward)
        {
            var first = vertices.Count;
            foreach (var point in section)
                vertices.Add(point);

            for (var i = 1; i < section.Length - 1; i++)
            {
                triangles.Add(first);
                triangles.Add(first + (outward ? i : i + 1));
                triangles.Add(first + (outward ? i + 1 : i));
            }
        }

        static void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            var first = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);

            triangles.Add(first);
            triangles.Add(first + 1);
            triangles.Add(first + 2);
            triangles.Add(first);
            triangles.Add(first + 2);
            triangles.Add(first + 3);
        }

        /// <summary>Единичный вектор направления в плоскости земли: соседи стоят на расстоянии 1.</summary>
        static Vector2 Plane(int direction) => HexCoord.Directions[direction].ToPlane();

        static float Angle(int direction)
        {
            var plane = Plane(direction);
            return Mathf.Atan2(plane.y, plane.x);
        }
    }
}
