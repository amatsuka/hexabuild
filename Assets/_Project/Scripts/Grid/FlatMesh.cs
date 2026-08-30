using System.Collections.Generic;
using UnityEngine;

namespace Game.Grid
{
    /// <summary>
    /// Сборщик плоских фигур: примитивы копятся в списки и отдаются либо мешем для поля, либо
    /// сырыми вершинами для канваса. Всё строится кодом, ассетов в проекте нет.
    ///
    /// Все грани обходятся по часовой стрелке: камера смотрит вдоль +Z, иначе их срежет backface
    /// culling. Порядок вершин выпуклой фигуры — снизу слева вверх по левому краю, вправо по
    /// верхнему и вниз по правому.
    /// </summary>
    public sealed class FlatMesh
    {
        readonly List<Vector2> vertices = new();
        readonly List<int> triangles = new();

        public IReadOnlyList<Vector2> Vertices => vertices;

        public IReadOnlyList<int> Triangles => triangles;

        /// <summary>Равнобедренный треугольник остриём вверх.</summary>
        public FlatMesh Triangle(Vector2 center, float width, float height) =>
            Polygon(
                new Vector2(center.x - width * 0.5f, center.y - height * 0.5f),
                new Vector2(center.x, center.y + height * 0.5f),
                new Vector2(center.x + width * 0.5f, center.y - height * 0.5f));

        public FlatMesh Quad(Vector2 center, float width, float height)
        {
            var halfWidth = width * 0.5f;
            var halfHeight = height * 0.5f;
            return Polygon(
                new Vector2(center.x - halfWidth, center.y - halfHeight),
                new Vector2(center.x - halfWidth, center.y + halfHeight),
                new Vector2(center.x + halfWidth, center.y + halfHeight),
                new Vector2(center.x + halfWidth, center.y - halfHeight));
        }

        public FlatMesh Ellipse(Vector2 center, float radiusX, float radiusY, int sides) =>
            Fan(center, radiusX, radiusY, sides, 0f, 360f);

        /// <summary>Сектор эллипса от одного угла до другого; целый круг — это 0…360.</summary>
        public FlatMesh Fan(Vector2 center, float radiusX, float radiusY, int sides, float fromDegrees, float toDegrees)
        {
            var first = vertices.Count;

            // У полного круга последняя вершина обода совпала бы с первой — её не кладём,
            // а замыкаем последний треугольник на первую.
            var full = Mathf.Approximately(Mathf.Abs(toDegrees - fromDegrees), 360f);
            var rim = full ? sides : sides + 1;

            vertices.Add(center);
            for (var i = 0; i < rim; i++)
            {
                var angle = Mathf.Deg2Rad * Mathf.Lerp(fromDegrees, toDegrees, i / (float)sides);
                vertices.Add(new Vector2(
                    center.x + Mathf.Cos(angle) * radiusX,
                    center.y + Mathf.Sin(angle) * radiusY));
            }

            for (var i = 0; i < sides; i++)
            {
                triangles.Add(first);
                triangles.Add(first + (full && i == sides - 1 ? 1 : i + 2));
                triangles.Add(first + i + 1);
            }

            return this;
        }

        /// <summary>Кристалл: столбик, срезанный к острию сверху.</summary>
        public FlatMesh Crystal(Vector2 center, float width, float height, float tip)
        {
            var halfWidth = width * 0.5f;
            var halfHeight = height * 0.5f;
            return Polygon(
                new Vector2(center.x - halfWidth, center.y - halfHeight),
                new Vector2(center.x - halfWidth, center.y + halfHeight - tip),
                new Vector2(center.x, center.y + halfHeight),
                new Vector2(center.x + halfWidth, center.y + halfHeight - tip),
                new Vector2(center.x + halfWidth, center.y - halfHeight));
        }

        /// <summary>Обломок кристалла: вместо острия косой скол.</summary>
        public FlatMesh Broken(Vector2 center, float width, float height)
        {
            var halfWidth = width * 0.5f;
            var halfHeight = height * 0.5f;
            return Polygon(
                new Vector2(center.x - halfWidth, center.y - halfHeight),
                new Vector2(center.x - halfWidth, center.y + halfHeight),
                new Vector2(center.x + halfWidth, center.y + halfHeight * 0.2f),
                new Vector2(center.x + halfWidth, center.y - halfHeight));
        }

        /// <summary>Травинка: узкий клин от низа квадрата, завалённый вбок.</summary>
        public FlatMesh Blade(float baseX, float lean, float width, float height) =>
            Polygon(
                new Vector2(baseX - width * 0.5f, -0.5f),
                new Vector2(baseX + lean, -0.5f + height),
                new Vector2(baseX + width * 0.5f, -0.5f));

        /// <summary>Выпуклый многоугольник, вершины по часовой стрелке. Разбивается веером.</summary>
        public FlatMesh Polygon(params Vector2[] points)
        {
            var first = vertices.Count;
            foreach (var point in points)
                vertices.Add(point);

            for (var i = 1; i < points.Length - 1; i++)
            {
                triangles.Add(first);
                triangles.Add(first + i);
                triangles.Add(first + i + 1);
            }

            return this;
        }

        public Mesh Bake(string name)
        {
            var positions = new Vector3[vertices.Count];
            for (var i = 0; i < vertices.Count; i++)
                positions[i] = vertices[i];

            var mesh = new Mesh { name = name };
            mesh.SetVertices(positions);
            mesh.SetTriangles(triangles, 0);

            // Разведка 3D: под Lit-шейдером меш без нормалей чёрный.
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
