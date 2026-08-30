using System.Collections.Generic;
using Game.Grid;
using UnityEngine;

namespace Game.Roads
{
    /// <summary>
    /// Лента дороги по маске направлений: полоса идёт через центр плитки к серединам граней,
    /// повороты скруглены квадратичной кривой Безье с опорной точкой в центре. Меш строится
    /// один раз на пару «маска + ширина» — плиток на поле сотня, а разных лент десяток.
    /// </summary>
    public static class RoadMeshBuilder
    {
        /// <summary>Отрезков в дуге поворота: на восьми излом уже не читается ни на каком зуме.</summary>
        const int CurveSegments = 8;

        static readonly Dictionary<(int Mask, int Width), Mesh> cache = new();
        static readonly Dictionary<(int Mask, int Width), Mesh> bridges = new();
        static readonly List<Vector3> vertices = new();
        static readonly List<int> triangles = new();
        static readonly List<int> links = new(6);

        /// <summary>
        /// Меш ленты. <paramref name="linkMask"/> — биты направлений `HexCoord.Directions`,
        /// <paramref name="width"/> — полная ширина полосы в мировых единицах (гекс шириной 1).
        /// </summary>
        public static Mesh Get(int linkMask, float width)
        {
            // Ширина квантуется вью в считаное число ступеней, поэтому ключ из тысячных долей
            // кэш не размножает.
            var key = (linkMask & 0x3f, Mathf.RoundToInt(width * 1000f));

            // Проверка на null, а не только на наличие ключа: выход из Play Mode уничтожает меши,
            // а статический кэш переживает это и остался бы со ссылками на мёртвые объекты.
            if (cache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var mesh = Build(key.Item1, key.Item2 * 0.001f);
            cache[key] = mesh;
            return mesh;
        }

        /// <summary>
        /// Настил моста: доска от центра плитки до середины грани по каждому биту маски.
        /// Кладётся позади обочины и шире её, поэтому у перехода через реку дорога получает
        /// заметную деревянную оторочку.
        /// </summary>
        public static Mesh Bridge(int bridgeMask, float width)
        {
            var key = (bridgeMask & 0x3f, Mathf.RoundToInt(width * 1000f));
            if (bridges.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var mesh = BuildBridge(key.Item1, key.Item2 * 0.001f);
            bridges[key] = mesh;
            return mesh;
        }

        static Mesh BuildBridge(int bridgeMask, float width)
        {
            vertices.Clear();
            triangles.Clear();

            var half = width * 0.5f;
            for (var direction = 0; direction < HexCoord.Directions.Count; direction++)
            {
                if ((bridgeMask & (1 << direction)) == 0)
                    continue;

                // Прямая полоса от центра до грани: двух выборок хватает, доска не изогнута.
                AppendRibbon(Edge(direction), Edge(direction) * 0.5f, Vector2.zero, half, 2);
            }

            var mesh = new Mesh { name = $"Bridge {bridgeMask:X2}" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);

            // Разведка 3D: под Lit-шейдером меш без нормалей чёрный.
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static Mesh Build(int linkMask, float width)
        {
            vertices.Clear();
            triangles.Clear();
            links.Clear();

            for (var direction = 0; direction < HexCoord.Directions.Count; direction++)
                if ((linkMask & (1 << direction)) != 0)
                    links.Add(direction);

            var half = width * 0.5f;

            // Пятачок нужен только там, где ленты не хватает: одинокая дорога без соседей иначе
            // не нарисовалась бы вовсе, а тупику он скругляет обрубленный конец. На повороте его
            // класть нельзя — дуга срезает угол и проходит мимо центра, пятачок торчал бы из
            // внутренней стороны поворота шишкой.
            if (links.Count < 2)
                AppendHub(half);

            // Тупик — прямая от середины грани до центра: пары направлений для дуги здесь нет.
            if (links.Count == 1)
                AppendRibbon(Edge(links[0]), Edge(links[0]) * 0.5f, Vector2.zero, half, 2);

            // Каждая пара направлений — свой сквозной проезд. Опорная точка в центре превращает
            // стык двух отрезков в дугу; у противоположных направлений она вырождается в прямую.
            for (var i = 0; i < links.Count; i++)
            for (var j = i + 1; j < links.Count; j++)
                AppendRibbon(Edge(links[i]), Vector2.zero, Edge(links[j]), half, CurveSegments + 1);

            var mesh = new Mesh { name = $"Road {linkMask:X2}" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);

            // Разведка 3D: под Lit-шейдером меш без нормалей чёрный.
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Середина общей грани с соседом: полпути до его центра.</summary>
        static Vector2 Edge(int direction) => HexCoord.Directions[direction].ToPlane() * 0.5f;

        /// <summary>
        /// Полоса постоянной ширины вдоль квадратичной кривой Безье. Кривая живёт в плоских
        /// координатах плитки, а вершины кладутся на землю XZ. Вершины парами «левая, правая»,
        /// обход такой же, как у крышки гекса, — нормаль ленты смотрит вверх.
        /// </summary>
        static void AppendRibbon(Vector2 from, Vector2 control, Vector2 to, float half, int samples)
        {
            var first = vertices.Count;

            for (var i = 0; i < samples; i++)
            {
                var t = i / (float)(samples - 1);
                var point = Bezier(from, control, to, t);
                var tangent = Tangent(from, control, to, t).normalized;
                var normal = new Vector2(-tangent.y, tangent.x) * half;

                vertices.Add(new Vector3(point.x + normal.x, 0f, point.y + normal.y));
                vertices.Add(new Vector3(point.x - normal.x, 0f, point.y - normal.y));
            }

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

        static Vector2 Bezier(Vector2 from, Vector2 control, Vector2 to, float t)
        {
            var inverse = 1f - t;
            return inverse * inverse * from + 2f * inverse * t * control + t * t * to;
        }

        static Vector2 Tangent(Vector2 from, Vector2 control, Vector2 to, float t) =>
            2f * (1f - t) * (control - from) + 2f * t * (to - control);
    }
}
