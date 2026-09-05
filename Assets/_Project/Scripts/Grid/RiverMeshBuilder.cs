using System.Collections.Generic;
using UnityEngine;

namespace Game.Grid
{
    /// <summary>
    /// Лента русла: полоса между воротами `RiverCourse.Gate`, повороты скруглены квадратичной
    /// кривой Безье с опорной точкой `RiverCourse.Bend`. Меш строится на плитку и каждый раз
    /// заново: с M22 ворота и опорная точка сносятся от решётки по хэшу координаты, и у двух
    /// плиток с одной маской они уже разные — общего меша на маску+ширину больше не бывает,
    /// а значит и кэшировать нечего. Владелец меша — вызывающий `TileView`, ему и гасить его
    /// в своём `OnDestroy`.
    ///
    /// Раньше эту ленту делил с руслом и дорогой один билдер. В M20 дорога перестала быть лентой
    /// и уехала в объём, а русло осталось плоским: оно лежит **в** крышке плитки, а не на ней,
    /// и объём ему только помешал бы. Так что копии кода тут нет — есть две разные вещи.
    /// </summary>
    public static class RiverMeshBuilder
    {
        static readonly List<Vector3> vertices = new();
        static readonly List<int> triangles = new();
        static readonly List<int> links = new(6);

        /// <summary>
        /// Меш ленты русла плитки. Вызывающий владеет результатом и обязан его `Destroy`.
        /// <paramref name="widthScale"/> — визуальный множитель поверх ширины по течению,
        /// <paramref name="extraMargin"/> — прибавка для второго, берегового слоя (0 у воды).
        /// </summary>
        public static Mesh Build(TileData tile, float widthScale, float extraMargin = 0f)
        {
            vertices.Clear();
            triangles.Clear();
            links.Clear();

            var mask = tile.RiverMask;
            for (var direction = 0; direction < HexCoord.Directions.Count; direction++)
                if ((mask & (1 << direction)) != 0)
                    links.Add(direction);

            // Пятачок нужен только там, где ленты не хватает: одинокое русло без соседей иначе
            // не нарисовалось бы вовсе, а тупику он скругляет обрубленный конец. На повороте его
            // класть нельзя — дуга срезает угол и проходит мимо центра, пятачок торчал бы из
            // внутренней стороны поворота шишкой. Ширина пятачка — с единственного рукава, а без
            // рукавов вовсе (одинокая плитка без соседей) — истоковая по умолчанию.
            if (links.Count < 2)
            {
                var water = links.Count == 1
                    ? RiverWidth.Water(RiverWidth.GateFlow(tile, links[0]))
                    : RiverWidth.SourceWidth;
                var half = (water * widthScale + extraMargin) * 0.5f;
                AppendHub(half);

                // Тупик — прямая от ворот грани до центра: пары направлений для дуги здесь нет.
                if (links.Count == 1)
                    AppendDeadEnd(tile.Coord, links[0], half);
            }

            // Каждая пара направлений — свой сквозной проток через опорную точку `RiverCourse.Bend`.
            for (var i = 0; i < links.Count; i++)
            for (var j = i + 1; j < links.Count; j++)
            {
                var halfFrom = (RiverWidth.Water(RiverWidth.GateFlow(tile, links[i])) * widthScale + extraMargin) * 0.5f;
                var halfTo = (RiverWidth.Water(RiverWidth.GateFlow(tile, links[j])) * widthScale + extraMargin) * 0.5f;
                AppendArc(tile.Coord, links[i], links[j], halfFrom, halfTo);
            }

            var mesh = new Mesh { name = $"River {tile.Coord}" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);

            // Разведка 3D: под Lit-шейдером меш без нормалей чёрный.
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Полоса между воротами двух направлений. Борта — не отступ от осевой линии по
        /// нормали дуги, а свои собственные кривые Безье через `RiverCourse.Bend`, заякоренные
        /// в воротах со сдвигом вдоль грани (`RiverCourse.EdgeTangent`) на полуширину. У осевой
        /// нормаль дуги в воротах смотрит не вдоль грани — снос увёл её, — и борт по ней срезал
        /// бы угол на соседа; борт по своей кривой стартует и кончается ровно на грани, а
        /// целиком лежит внутри шестиугольника по выпуклости: оба конца и опорная точка лежат
        /// в нём. Лерп ширины от конца к концу тут и даёт непараллельные борта.
        /// </summary>
        static void AppendArc(HexCoord tile, int fromDirection, int toDirection, float halfFrom, float halfTo)
        {
            var first = vertices.Count;
            const int samples = RiverCourse.CurveSegments + 1;

            var bend = RiverCourse.Bend(tile);
            var gateFrom = RiverCourse.Gate(tile, fromDirection);
            var gateTo = RiverCourse.Gate(tile, toDirection);

            // Смещение вдоль грани держит инвариант «внутри шестиугольника» (см. класс), но у
            // какой стороны грани окажется «near», а у какой «far», решает не сама грань, а
            // направление хода дуги в этой конкретной точке: на прямом протоке оба конца смотрят
            // в одну сторону от EdgeTangent, а на повороте — в разные, и без пересчёта знака
            // независимо на каждом конце борта на повороте перекручиваются и режут землю.
            var offsetFrom = EdgeOffset(fromDirection, bend - gateFrom, halfFrom);
            var offsetTo = EdgeOffset(toDirection, gateTo - bend, halfTo);

            var nearFrom = gateFrom - offsetFrom;
            var farFrom = gateFrom + offsetFrom;
            var nearTo = gateTo - offsetTo;
            var farTo = gateTo + offsetTo;

            for (var i = 0; i < samples; i++)
            {
                var t = i / (float)(samples - 1);
                var near = RiverCourse.Bezier(nearFrom, bend, nearTo, t);
                var far = RiverCourse.Bezier(farFrom, bend, farTo, t);

                vertices.Add(new Vector3(near.x, 0f, near.y));
                vertices.Add(new Vector3(far.x, 0f, far.y));
            }

            AddStrip(first, samples);
        }

        /// <summary>
        /// Тупик: прямая от ворот грани до центра плитки. Ширина по прямой постоянна, а сдвиг
        /// бортов — тот же `EdgeTangent`, что у дуги: борт стартует ровно на грани по той же
        /// причине, а к центру доходит с тем же сдвигом — там его в любом случае накрывает
        /// шестиугольный пятачок.
        /// </summary>
        static void AppendDeadEnd(HexCoord tile, int direction, float half)
        {
            var first = vertices.Count;
            var gate = RiverCourse.Gate(tile, direction);
            var offset = EdgeOffset(direction, -gate, half);

            vertices.Add(new Vector3(gate.x - offset.x, 0f, gate.y - offset.y));
            vertices.Add(new Vector3(gate.x + offset.x, 0f, gate.y + offset.y));
            vertices.Add(new Vector3(-offset.x, 0f, -offset.y));
            vertices.Add(new Vector3(offset.x, 0f, offset.y));

            AddStrip(first, 2);
        }

        /// <summary>
        /// Сдвиг вдоль грани в направлении <paramref name="direction"/> с знаком, подобранным
        /// под фактический ход дуги в этой точке (<paramref name="travel"/>, не обязательно
        /// нормализован): «near» всегда остаётся с той стороны, что даёт `AddStrip` треугольники
        /// нормалью вверх — см. `AppendArc`.
        /// </summary>
        static Vector2 EdgeOffset(int direction, Vector2 travel, float half)
        {
            var tangent = RiverCourse.EdgeTangent(direction);
            var rotatedTravel = new Vector2(-travel.y, travel.x);
            var sign = Vector2.Dot(tangent, rotatedTravel) >= 0f ? -1f : 1f;
            return tangent * (half * sign);
        }

        static void AddStrip(int first, int samples)
        {
            for (var i = 0; i < samples - 1; i++)
            {
                var left = first + i * 2;
                triangles.Add(left);
                triangles.Add(left + 2);
                triangles.Add(left + 3);
                triangles.Add(left);
                triangles.Add(left + 3);
                triangles.Add(left + 1);
            }
        }

        /// <summary>Шестиугольный пятачок в центре плитки.</summary>
        static void AppendHub(float radius)
        {
            var first = vertices.Count;
            vertices.Add(Vector3.zero);
            for (var i = 0; i < 6; i++)
            {
                var angle = Mathf.Deg2Rad * (60f * i - 30f);
                vertices.Add(new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }

            for (var i = 0; i < 6; i++)
            {
                triangles.Add(first);
                triangles.Add(first + (i == 5 ? 1 : i + 2));
                triangles.Add(first + i + 1);
            }
        }
    }
}
