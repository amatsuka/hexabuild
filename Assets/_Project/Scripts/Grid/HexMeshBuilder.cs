using UnityEngine;

namespace Game.Grid
{
    /// <summary>Строит меш pointy-top гекса кодом: арта в проекте нет.</summary>
    public static class HexMeshBuilder
    {
        static Mesh shared;

        static readonly System.Collections.Generic.Dictionary<int, Mesh> prisms = new();

        /// <summary>Один общий меш на все плитки поля.</summary>
        public static Mesh Shared => shared != null ? shared : shared = Build();

        /// <summary>
        /// Гекс с юбкой. Крышка там же, где у плоского меша (y = 0), поэтому вся раскладка
        /// дорог и декора по высоте остаётся верной, а юбка уходит вниз на <paramref name="skirt"/>.
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

        static Mesh BuildPrism(float skirt)
        {
            var rim = new Vector3[6];
            for (var i = 0; i < 6; i++)
            {
                var angle = Mathf.Deg2Rad * (60f * i - 30f);
                rim[i] = new Vector3(HexCoord.Size * Mathf.Cos(angle), 0f, HexCoord.Size * Mathf.Sin(angle));
            }

            var vertices = new System.Collections.Generic.List<Vector3>(7 + 24);
            var triangles = new System.Collections.Generic.List<int>(18 + 36);

            // Крышка: та же геометрия и тот же обход, что у плоского гекса.
            vertices.Add(Vector3.zero);
            vertices.AddRange(rim);
            for (var i = 0; i < 6; i++)
            {
                triangles.Add(0);
                triangles.Add(i == 5 ? 1 : i + 2);
                triangles.Add(i + 1);
            }

            // Юбка: своя четвёрка вершин на грань, иначе нормали крышки и стенки усреднятся
            // и рёбра плитки размажутся вместо чёткого перелома.
            for (var i = 0; i < 6; i++)
            {
                var top = rim[i];
                var next = rim[(i + 1) % 6];
                var first = vertices.Count;

                vertices.Add(top);
                vertices.Add(next);
                vertices.Add(next - new Vector3(0f, skirt, 0f));
                vertices.Add(top - new Vector3(0f, skirt, 0f));

                triangles.Add(first);
                triangles.Add(first + 1);
                triangles.Add(first + 2);
                triangles.Add(first);
                triangles.Add(first + 2);
                triangles.Add(first + 3);
            }

            var mesh = new Mesh { name = $"HexPrism {skirt:F3}" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
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
