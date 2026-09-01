using UnityEngine;

namespace Game.Grid
{
    /// <summary>Строит меш pointy-top гекса кодом: арта в проекте нет.</summary>
    public static class HexMeshBuilder
    {
        /// <summary>
        /// Насколько крышка ужата внутрь ради фаски. Наружу фаску не увести: соседние плитки
        /// стоят вплотную, и всё, что выходит за радиус гекса, врастает в соседа.
        /// </summary>
        public const float BevelInset = 0.09f;

        /// <summary>Глубина фаски: на ней крышка добирает до полного радиуса и переходит в юбку.</summary>
        public const float BevelDrop = 0.07f;

        /// <summary>
        /// Звеньев в четверти окружности фаски. Три — это уже скругление, а не второй перелом,
        /// и добавляет всего 36 треугольников на плитку.
        /// </summary>
        public const int BevelSegments = 3;

        static Mesh shared;

        static readonly System.Collections.Generic.Dictionary<int, Mesh> prisms = new();

        /// <summary>Один общий меш на все плитки поля.</summary>
        public static Mesh Shared => shared != null ? shared : shared = Build();

        /// <summary>
        /// Гекс с фаской и юбкой. Крышка там же, где у плоского меша (y = 0), поэтому вся раскладка
        /// дорог и декора по высоте остаётся верной, а борт уходит вниз на <paramref name="skirt"/>.
        /// Разную высоту плиток даёт не меш, а подъём самой плитки по Y.
        /// </summary>
        public static Mesh Prism(float skirt)
        {
            var key = Mathf.RoundToInt(skirt * 1000f);
            if (prisms.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var mesh = BuildPrism(key * 0.001f);
            prisms[key] = mesh;
            return mesh;
        }

        /// <summary>
        /// Борт плитки — четверть окружности, а не перелом: крышка ужата на <see cref="BevelInset"/>,
        /// на глубине <see cref="BevelDrop"/> плитка выходит на полный радиус и дальше падает
        /// вертикальной юбкой. Фаска касается крышки сверху и юбки снизу, поэтому по борту нет
        /// ни одного жёсткого ребра — объём читается скруглением, как на референсе.
        /// </summary>
        static Mesh BuildPrism(float skirt)
        {
            // Мелкая юбка не должна утащить фаску ниже дна плитки: тогда борт вывернулся бы наружу.
            var drop = Mathf.Min(BevelDrop, skirt);
            var inset = Mathf.Min(BevelInset, HexCoord.Size * 0.5f);
            var capRadius = HexCoord.Size - inset;

            var corner = new Vector3[6];
            var rim = new Vector3[6];
            var cap = new Vector3[6];
            for (var i = 0; i < 6; i++)
            {
                var angle = Mathf.Deg2Rad * (60f * i - 30f);
                corner[i] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                rim[i] = corner[i] * HexCoord.Size;
                cap[i] = corner[i] * capRadius;
            }

            var vertices = new System.Collections.Generic.List<Vector3>();
            var normals = new System.Collections.Generic.List<Vector3>();
            var triangles = new System.Collections.Generic.List<int>();

            // Крышка: та же геометрия и тот же обход, что у плоского гекса, только ужатая.
            vertices.Add(Vector3.zero);
            normals.Add(Vector3.up);
            for (var i = 0; i < 6; i++)
            {
                vertices.Add(cap[i]);
                normals.Add(Vector3.up);
            }

            for (var i = 0; i < 6; i++)
            {
                triangles.Add(0);
                triangles.Add(i == 5 ? 1 : i + 2);
                triangles.Add(i + 1);
            }

            // Борт: своя полоса вершин на грань, иначе нормали шести граней усреднятся и
            // силуэт гекса размажется в бочку. Внутри полосы вершины общие — по вертикали
            // фаска обязана быть гладкой, ради этого она и заведена.
            for (var face = 0; face < 6; face++)
            {
                var next = (face + 1) % 6;
                // Нормаль грани смотрит наружу по её апофеме, а не по вершине гекса.
                var outward = ((rim[face] + rim[next]) * 0.5f).normalized;
                var first = vertices.Count;

                for (var step = 0; step <= BevelSegments; step++)
                {
                    var turn = Mathf.PI * 0.5f * step / BevelSegments;
                    var sin = Mathf.Sin(turn);
                    var cos = Mathf.Cos(turn);
                    // Радиус идёт от ужатой крышки к полному, высота — от нуля к глубине фаски.
                    var radius = capRadius + inset * sin;
                    var y = -drop * (1f - cos);
                    var normal = outward * sin + Vector3.up * cos;

                    vertices.Add(corner[face] * radius + Vector3.up * y);
                    normals.Add(normal);
                    vertices.Add(corner[next] * radius + Vector3.up * y);
                    normals.Add(normal);
                }

                // Юбка: вертикальный участок под фаской, до общего дна поля.
                vertices.Add(rim[face] - new Vector3(0f, skirt, 0f));
                normals.Add(outward);
                vertices.Add(rim[next] - new Vector3(0f, skirt, 0f));
                normals.Add(outward);

                for (var row = 0; row <= BevelSegments; row++)
                {
                    var top = first + row * 2;
                    triangles.Add(top);
                    triangles.Add(top + 1);
                    triangles.Add(top + 3);
                    triangles.Add(top);
                    triangles.Add(top + 3);
                    triangles.Add(top + 2);
                }
            }

            var mesh = new Mesh { name = $"HexPrism {skirt:F3}" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        static Mesh Build()
        {
            var vertices = new Vector3[7];
            vertices[0] = Vector3.zero;
            for (var i = 0; i < 6; i++)
            {
                var angle = Mathf.Deg2Rad * (60f * i - 30f);
                vertices[i + 1] = new Vector3(HexCoord.Size * Mathf.Cos(angle), 0f, HexCoord.Size * Mathf.Sin(angle));
            }

            // Земля лежит в плоскости XZ, крышка смотрит нормалью вверх: при этом обходе
            // нормаль получается +Y, и backface culling грань не срезает.
            var triangles = new int[18];
            for (var i = 0; i < 6; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i == 5 ? 1 : i + 2;
                triangles[i * 3 + 2] = i + 1;
            }

            var mesh = new Mesh { name = "Hex" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);

            // Разведка 3D: без нормалей Lit-шейдер рисует меш чёрным. Обход по часовой стрелке
            // даёт нормаль в −Z, то есть в сторону камеры, — свет ставится с той же стороны.
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
