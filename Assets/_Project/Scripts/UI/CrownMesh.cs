using System.Collections.Generic;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Корона над заголовком финального экрана. Модели короны в паке нет, а нужна она одна и
    /// мелкая — поэтому меш строится кодом, как арт проекта по умолчанию, и печётся снимком
    /// тем же `ResourceIconBaker`, что монета и свиток.
    ///
    /// На поле она не встаёт и ни в одну из трёх групп мешей (см. `AGENTS.md`) не входит: это
    /// только исходник для снимка, живущий в кадре ортокамеры пекаря.
    ///
    /// Силуэт вытянут в глубину с сужением к лицевой стороне: боковые стенки от этого ловят
    /// свет под своим углом, и плоская фигура читается объёмной. Ровная призма давала бы одну
    /// заливку с тонким контуром — на снимке в полсотни пикселей это просто пятно.
    /// </summary>
    public static class CrownMesh
    {
        /// <summary>Половина толщины короны.</summary>
        const float HalfDepth = 0.10f;

        /// <summary>Во сколько раз лицевая грань уже задней: это и есть скос стенок.</summary>
        const float FrontTaper = 0.86f;

        /// <summary>Обод: от низа до линии, с которой начинаются зубцы.</summary>
        const float BandBottom = -0.50f;
        const float BandTop = 0.02f;
        const float HalfWidth = 0.50f;

        /// <summary>Зубцы: боковые ниже среднего, впадины между ними сидят на линии обода.</summary>
        const float SidePeak = 0.40f;
        const float MiddlePeak = 0.56f;
        const float ValleyX = 0.22f;

        static Mesh shared;

        public static Mesh Shared => shared != null ? shared : shared = Build();

        public static Mesh Build()
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();

            var face = Face();
            AddFace(vertices, normals, triangles, face, -HalfDepth, FrontTaper, front: true);
            AddFace(vertices, normals, triangles, face, HalfDepth, 1f, front: false);
            AddWalls(vertices, normals, triangles, Outline());

            var mesh = new Mesh { name = "Crown" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Лицевая сторона треугольниками: обод плюс три зубца. Веером из одной вершины её не
        /// собрать — впадины между зубцами делают силуэт невыпуклым, и луч из угла обода уходит
        /// над впадиной наружу фигуры.
        /// </summary>
        static Vector2[] Face() => new[]
        {
            new Vector2(-HalfWidth, BandBottom), new Vector2(HalfWidth, BandBottom), new Vector2(HalfWidth, BandTop),
            new Vector2(-HalfWidth, BandBottom), new Vector2(HalfWidth, BandTop), new Vector2(-HalfWidth, BandTop),

            new Vector2(-HalfWidth, BandTop), new Vector2(-ValleyX, BandTop), new Vector2(-HalfWidth, SidePeak),
            new Vector2(-ValleyX, BandTop), new Vector2(ValleyX, BandTop), new Vector2(0f, MiddlePeak),
            new Vector2(ValleyX, BandTop), new Vector2(HalfWidth, BandTop), new Vector2(HalfWidth, SidePeak)
        };

        /// <summary>Кромка силуэта по кругу: по ней идут боковые стенки.</summary>
        static Vector2[] Outline() => new[]
        {
            new Vector2(-HalfWidth, BandBottom), new Vector2(HalfWidth, BandBottom), new Vector2(HalfWidth, SidePeak),
            new Vector2(ValleyX, BandTop), new Vector2(0f, MiddlePeak), new Vector2(-ValleyX, BandTop),
            new Vector2(-HalfWidth, SidePeak)
        };

        /// <summary>
        /// Грань силуэта на своей глубине. Треугольники силуэта перечислены против часовой
        /// стрелки, то есть нормалью на зрителя вдоль `+Z`; лицевой грани обход разворачивают.
        /// </summary>
        static void AddFace(
            List<Vector3> vertices, List<Vector3> normals, List<int> triangles,
            IReadOnlyList<Vector2> face, float depth, float scale, bool front)
        {
            for (var i = 0; i < face.Count; i += 3)
            {
                var start = vertices.Count;
                for (var corner = 0; corner < 3; corner++)
                {
                    vertices.Add(new Vector3(face[i + corner].x * scale, face[i + corner].y * scale, depth));
                    normals.Add(front ? Vector3.back : Vector3.forward);
                }

                triangles.Add(start);
                triangles.Add(start + (front ? 2 : 1));
                triangles.Add(start + (front ? 1 : 2));
            }
        }

        /// <summary>
        /// Стенка на каждое ребро кромки: от задней грани к суженной лицевой. Кромка обходится
        /// против часовой стрелки, поэтому наружу смотрит правая сторона хода.
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
