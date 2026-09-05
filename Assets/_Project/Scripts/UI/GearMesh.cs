using System.Collections.Generic;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Шестерёнка на кнопке паузы. Модели в паке нет, а нужна она одна и мелкая — поэтому меш
    /// строится кодом, как и корона финального экрана (`CrownMesh`), и печётся в снимок тем же
    /// `ResourceIconBaker`.
    ///
    /// В отличие от короны контур звёздчатый относительно центра — зубец не заслоняет соседний, —
    /// поэтому лицевая грань триангулируется простым веером от центра, без ручного списка
    /// треугольников.
    /// </summary>
    public static class GearMesh
    {
        /// <summary>Половина толщины шестерёнки.</summary>
        const float HalfDepth = 0.10f;

        /// <summary>Во сколько раз лицевая грань уже задней: скос стенок, как у короны.</summary>
        const float FrontTaper = 0.86f;

        const int Teeth = 8;
        const float OuterRadius = 0.5f;
        const float InnerRadius = 0.36f;

        static Mesh shared;

        public static Mesh Shared => shared != null ? shared : shared = Build();

        public static Mesh Build()
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();

            var outline = Outline();
            AddFace(vertices, normals, triangles, outline, -HalfDepth, FrontTaper, front: true);
            AddFace(vertices, normals, triangles, outline, HalfDepth, 1f, front: false);
            AddWalls(vertices, normals, triangles, outline);

            var mesh = new Mesh { name = "Gear" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Зубчатый контур: чередование внешнего и внутреннего радиуса по кругу.</summary>
        static Vector2[] Outline()
        {
            var points = new Vector2[Teeth * 2];
            for (var i = 0; i < points.Length; i++)
            {
                var angle = i * Mathf.PI * 2f / points.Length;
                var radius = i % 2 == 0 ? OuterRadius : InnerRadius;
                points[i] = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
            }

            return points;
        }

        /// <summary>
        /// Грань веером от центра: контур звёздчатый относительно (0,0), и центр видит каждое
        /// ребро контура напрямую — в отличие от короны, ручной список треугольников не нужен.
        /// </summary>
        static void AddFace(
            List<Vector3> vertices, List<Vector3> normals, List<int> triangles,
            IReadOnlyList<Vector2> outline, float depth, float scale, bool front)
        {
            for (var i = 0; i < outline.Count; i++)
            {
                var a = outline[i];
                var b = outline[(i + 1) % outline.Count];
                var start = vertices.Count;

                vertices.Add(new Vector3(0f, 0f, depth));
                vertices.Add(new Vector3(a.x * scale, a.y * scale, depth));
                vertices.Add(new Vector3(b.x * scale, b.y * scale, depth));
                for (var corner = 0; corner < 3; corner++)
                    normals.Add(front ? Vector3.back : Vector3.forward);

                triangles.Add(start);
                triangles.Add(start + (front ? 2 : 1));
                triangles.Add(start + (front ? 1 : 2));
            }
        }

        /// <summary>
        /// Стенка на каждое ребро контура: от задней грани к суженной лицевой, тот же приём,
        /// что у короны.
        /// </summary>
        static void AddWalls(
            List<Vector3> vertices, List<Vector3> normals, List<int> triangles, IReadOnlyList<Vector2> outline)
        {
            for (var i = 0; i < outline.Count; i++)
            {
                var from = outline[i];
                var to = outline[(i + 1) % outline.Count];

                var backFrom = new Vector3(from.x, from.y, HalfDepth);
                var backTo = new Vector3(to.x, to.y, HalfDepth);
                var frontFrom = new Vector3(from.x * FrontTaper, from.y * FrontTaper, -HalfDepth);
                var frontTo = new Vector3(to.x * FrontTaper, to.y * FrontTaper, -HalfDepth);

                var normal = Vector3.Cross(frontFrom - backFrom, frontTo - backFrom).normalized;
                var start = vertices.Count;

                vertices.Add(backFrom);
                vertices.Add(frontFrom);
                vertices.Add(frontTo);
                vertices.Add(backTo);
                for (var corner = 0; corner < 4; corner++)
                    normals.Add(normal);

                triangles.Add(start);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
                triangles.Add(start + 2);
                triangles.Add(start + 3);
                triangles.Add(start);
            }
        }
    }
}
