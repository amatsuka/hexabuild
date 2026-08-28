using UnityEngine;

namespace Game.Grid
{
    /// <summary>Мелкие меши для декора плиток: всё строится кодом, ассетов в проекте нет.</summary>
    public static class ShapeMeshes
    {
        /// <summary>Толщина ободка гекса в долях его радиуса.</summary>
        const float RingWidth = 0.12f;

        static Mesh triangle;
        static Mesh bar;
        static Mesh hexRing;

        /// <summary>Равнобедренный треугольник высотой 1 и шириной 1 — ёлка или скала.</summary>
        public static Mesh Triangle => triangle != null ? triangle : triangle = BuildTriangle();

        /// <summary>Квадрат со стороной 1 — полоска воды или песка.</summary>
        public static Mesh Bar => bar != null ? bar : bar = BuildBar();

        /// <summary>
        /// Ободок по границе гекса. Увеличенный гекс позади плитки не годится: соседние плитки
        /// вплотную закрывают его, и виден только край поля.
        /// </summary>
        public static Mesh HexRing => hexRing != null ? hexRing : hexRing = BuildHexRing();

        static Mesh BuildHexRing()
        {
            var vertices = new Vector3[12];
            for (var i = 0; i < 6; i++)
            {
                var angle = Mathf.Deg2Rad * (60f * i - 30f);
                var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                vertices[i] = direction * HexCoord.Size;
                vertices[i + 6] = direction * (HexCoord.Size * (1f + RingWidth));
            }

            // Обход по часовой стрелке, как у остальных мешей: внутренний угол, следующий
            // внутренний, следующий внешний, внешний.
            var triangles = new int[36];
            for (var i = 0; i < 6; i++)
            {
                var next = (i + 1) % 6;
                triangles[i * 6] = i;
                triangles[i * 6 + 1] = next;
                triangles[i * 6 + 2] = next + 6;
                triangles[i * 6 + 3] = i;
                triangles[i * 6 + 4] = next + 6;
                triangles[i * 6 + 5] = i + 6;
            }

            var mesh = new Mesh { name = "HexRing" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

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
