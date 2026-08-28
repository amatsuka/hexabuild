using UnityEngine;

namespace Game.Grid
{
    /// <summary>Строит меш pointy-top гекса кодом: арта в проекте нет.</summary>
    public static class HexMeshBuilder
    {
        static Mesh shared;

        /// <summary>Один общий меш на все плитки поля.</summary>
        public static Mesh Shared => shared != null ? shared : shared = Build();

        static Mesh Build()
        {
            var vertices = new Vector3[7];
            vertices[0] = Vector3.zero;
            for (var i = 0; i < 6; i++)
            {
                var angle = Mathf.Deg2Rad * (60f * i - 30f);
                vertices[i + 1] = new Vector3(HexCoord.Size * Mathf.Cos(angle), HexCoord.Size * Mathf.Sin(angle), 0f);
            }

            var triangles = new int[18];
            for (var i = 0; i < 6; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i == 5 ? 1 : i + 2;
            }

            var mesh = new Mesh { name = "Hex" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
