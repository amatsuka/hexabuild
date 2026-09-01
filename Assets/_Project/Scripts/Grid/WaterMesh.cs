using UnityEngine;

namespace Game.Grid
{
    /// <summary>
    /// Подложка воды: один квадрат в плоскости XZ нормалью вверх — первая группа мешей проекта,
    /// та же, где крышка плитки и ленты дорог. Плоскость лежит на урезе `TileView.WaterSurface`,
    /// а не под полем: суша стоит выше неё, дно — ниже, и глубину шейдер читает как разницу
    /// между ними. Ассетом её делать нечего — четыре вершины строятся кодом.
    /// </summary>
    public static class WaterMesh
    {
        /// <summary>Квадрат со стороной <paramref name="size"/>, центр в нуле, крышка вверх.</summary>
        public static Mesh Build(float size)
        {
            var half = size * 0.5f;
            var mesh = new Mesh { name = $"Water {size:F0}" };

            mesh.SetVertices(new[]
            {
                new Vector3(-half, 0f, -half),
                new Vector3(-half, 0f, half),
                new Vector3(half, 0f, half),
                new Vector3(half, 0f, -half)
            });

            // Тот же обход, что у крышки гекса: при нём нормаль выходит в +Y и backface culling
            // плоскость не срезает.
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.SetNormals(new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            mesh.SetUVs(0, new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right });
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
