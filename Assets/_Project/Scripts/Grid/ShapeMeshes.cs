using UnityEngine;

namespace Game.Grid
{
    /// <summary>Мелкие меши для декора плиток: всё строится кодом, ассетов в проекте нет.</summary>
    public static class ShapeMeshes
    {
        static Mesh triangle;
        static Mesh bar;

        /// <summary>Равнобедренный треугольник высотой 1 и шириной 1 — ёлка или скала.</summary>
        public static Mesh Triangle => triangle != null ? triangle : triangle = BuildTriangle();

        /// <summary>Квадрат со стороной 1 — полоска воды или песка.</summary>
        public static Mesh Bar => bar != null ? bar : bar = BuildBar();

        static Mesh BuildTriangle()
        {
            var mesh = new Mesh { name = "Triangle" };
            mesh.SetVertices(new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0f, 0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f)
            });
            // Обход по часовой стрелке, как у гекса: иначе грань срежет backface culling.
            mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        static Mesh BuildBar()
        {
            var mesh = new Mesh { name = "Bar" };
            mesh.SetVertices(new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f)
            });
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
