using System.Collections.Generic;
using UnityEngine;

namespace Game.Grid
{
    /// <summary>
    /// Сборщик объёмных холмиков: купол за куполом, всё кодом. Плоские фигуры собирает
    /// <see cref="FlatMesh"/>, но кочку и бархан плоскими оставлять нельзя — в наклонённом мире
    /// односторонняя фигура укорочена и с обратной стороны невидима.
    ///
    /// Холмик стоит основанием на земле: земля — плоскость XZ, вверх — Y. Это третья группа мешей
    /// проекта, отдельная и от лежащих на земле лент, и от стоящих в XY фигур декора.
    /// </summary>
    public sealed class MoundMesh
    {
        readonly List<Vector3> vertices = new();
        readonly List<int> triangles = new();

        /// <summary>
        /// Купол на земле: вершина в центре, кольца вниз по четверти окружности, дна нет —
        /// снизу холмик не видно никогда, а лишние грани стоят треугольников.
        /// </summary>
        public MoundMesh Dome(Vector2 center, float radiusX, float radiusZ, float height, int sides, int rings)
        {
            var apex = vertices.Count;
            vertices.Add(new Vector3(center.x, height, center.y));

            for (var ring = 1; ring <= rings; ring++)
            {
                var turn = Mathf.PI * 0.5f * ring / rings;
                var spread = Mathf.Sin(turn);
                var lift = Mathf.Cos(turn) * height;

                for (var side = 0; side < sides; side++)
                {
                    var angle = Mathf.PI * 2f * side / sides;
                    vertices.Add(new Vector3(
                        center.x + Mathf.Cos(angle) * radiusX * spread,
                        lift,
                        center.y + Mathf.Sin(angle) * radiusZ * spread));
                }
            }

            // Шапка: обход по убыванию угла даёт нормаль вверх, как у крышки гекса.
            for (var side = 0; side < sides; side++)
            {
                triangles.Add(apex);
                triangles.Add(apex + 1 + (side + 1) % sides);
                triangles.Add(apex + 1 + side);
            }

            // Пояса: обход по возрастанию угла даёт нормаль наружу, как у юбки гекса.
            for (var ring = 1; ring < rings; ring++)
            {
                var upper = apex + 1 + (ring - 1) * sides;
                var lower = upper + sides;

                for (var side = 0; side < sides; side++)
                {
                    var next = (side + 1) % sides;
                    triangles.Add(upper + side);
                    triangles.Add(upper + next);
                    triangles.Add(lower + next);
                    triangles.Add(upper + side);
                    triangles.Add(lower + next);
                    triangles.Add(lower + side);
                }
            }

            return this;
        }

        public Mesh Bake(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
