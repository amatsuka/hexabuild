using System.Collections.Generic;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Свиток контракта: лист с двумя скрутками сверху и снизу. Модели свитка в паке нет, а
    /// иконка нужна одна и мелкая — поэтому меш строится кодом, как арт проекта по умолчанию.
    ///
    /// На поле он не встаёт и ни в одну из трёх групп мешей (см. `AGENTS.md`) не входит: это
    /// только исходник для снимка в `ResourceIconBaker`, живущий в кадре ортокамеры пекаря.
    /// </summary>
    public static class ScrollMesh
    {
        const int RollSides = 12;
        const float SheetHalfWidth = 0.40f;
        const float SheetHalfHeight = 0.46f;
        const float SheetHalfDepth = 0.05f;
        const float RollRadius = 0.21f;
        const float RollHalfLength = 0.62f;

        static Mesh shared;

        public static Mesh Shared => shared != null ? shared : shared = Build();

        public static Mesh Build()
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();

            AddBox(vertices, normals, triangles, Vector3.zero,
                new Vector3(SheetHalfWidth, SheetHalfHeight, SheetHalfDepth));

            // Скрутки лежат поперёк листа и чуть выступают за его края — иначе на снимке в
            // полсотни пикселей лист и скрутка сливаются в один прямоугольник.
            AddRoll(vertices, normals, triangles, new Vector3(0f, SheetHalfHeight, 0f));
            AddRoll(vertices, normals, triangles, new Vector3(0f, -SheetHalfHeight, 0f));

            var mesh = new Mesh { name = "ContractScroll" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Скрутка: цилиндр вдоль X с крышками на торцах.</summary>
        static void AddRoll(List<Vector3> vertices, List<Vector3> normals, List<int> triangles, Vector3 centre)
        {
            var start = vertices.Count;

            for (var i = 0; i < RollSides; i++)
            {
                var angle = i / (float)RollSides * Mathf.PI * 2f;
                var ring = new Vector3(0f, Mathf.Cos(angle), Mathf.Sin(angle));

                vertices.Add(centre + ring * RollRadius - Vector3.right * RollHalfLength);
                normals.Add(ring);
                vertices.Add(centre + ring * RollRadius + Vector3.right * RollHalfLength);
                normals.Add(ring);
            }

            for (var i = 0; i < RollSides; i++)
            {
                var a = start + i * 2;
                var b = start + (i + 1) % RollSides * 2;
                triangles.Add(a);
                triangles.Add(a + 1);
                triangles.Add(b);
                triangles.Add(b);
                triangles.Add(a + 1);
                triangles.Add(b + 1);
            }

            AddCap(vertices, normals, triangles, centre - Vector3.right * RollHalfLength, Vector3.left);
            AddCap(vertices, normals, triangles, centre + Vector3.right * RollHalfLength, Vector3.right);
        }

        static void AddCap(
            List<Vector3> vertices, List<Vector3> normals, List<int> triangles, Vector3 centre, Vector3 normal)
        {
            var hub = vertices.Count;
            vertices.Add(centre);
            normals.Add(normal);

            for (var i = 0; i < RollSides; i++)
            {
                var angle = i / (float)RollSides * Mathf.PI * 2f;
                vertices.Add(centre + new Vector3(0f, Mathf.Cos(angle), Mathf.Sin(angle)) * RollRadius);
                normals.Add(normal);
            }

            for (var i = 0; i < RollSides; i++)
            {
                var a = hub + 1 + i;
                var b = hub + 1 + (i + 1) % RollSides;
                triangles.Add(hub);
                triangles.Add(normal.x > 0f ? a : b);
                triangles.Add(normal.x > 0f ? b : a);
            }
        }

        /// <summary>Коробка с плоскими нормалями: шесть граней по четыре своих вершины.</summary>
        static void AddBox(
            List<Vector3> vertices, List<Vector3> normals, List<int> triangles, Vector3 centre, Vector3 half)
        {
            Vector3[] faces =
            {
                Vector3.forward, Vector3.back, Vector3.up, Vector3.down, Vector3.right, Vector3.left
            };

            foreach (var normal in faces)
            {
                var start = vertices.Count;
                var right = new Vector3(normal.y, normal.z, normal.x);
                var up = Vector3.Cross(normal, right);

                foreach (var corner in new[] { new Vector2(-1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f), new Vector2(1f, -1f) })
                {
                    vertices.Add(centre + Vector3.Scale(normal + right * corner.x + up * corner.y, half));
                    normals.Add(normal);
                }

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
