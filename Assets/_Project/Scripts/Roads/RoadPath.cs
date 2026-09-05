using System;
using System.Collections.Generic;
using Game.Grid;
using UnityEngine;

namespace Game.Roads
{
    /// <summary>
    /// Осевая линия маршрута в мировых координатах: ломаная по цепочке плиток, скруглённая на
    /// поворотах и параметризованная по длине дуги.
    ///
    /// Цепочка приходит из дерева родителей `RoadNetwork` — это тот самый маршрут, который игрок
    /// видит нарисованным. Линия идёт от центра плитки через середины общих граней: середина
    /// грани и есть точка, где дорога переходит к соседу, а поворот внутри плитки скругляется
    /// квадратичной кривой Безье с опорной точкой в центре — той же, которой раньше скруглялась
    /// лента.
    ///
    /// Высота берётся с полотна насыпи, а не с земли: на шве полотно встаёт на высоту старшей из
    /// двух плиток (см. `RoadMeshBuilder`), и линия обязана повторить это, иначе груз поедет
    /// сквозь насыпь. Отсюда `SurfaceLift` в каждой точке.
    /// </summary>
    public sealed class RoadPath
    {
        /// <summary>Звеньев в скруглении поворота внутри плитки.</summary>
        const int CornerSegments = 8;

        readonly List<Vector3> points = new();

        /// <summary>Расстояние вдоль линии до точки с тем же индексом. Первое всегда ноль.</summary>
        readonly List<float> distances = new();

        RoadPath()
        {
        }

        /// <summary>Полная длина маршрута. У цепочки из одной плитки — ноль.</summary>
        public float Length => distances[^1];

        public IReadOnlyList<Vector3> Points => points;

        /// <summary>
        /// Построить линию по цепочке соседних плиток. <paramref name="heightOf"/> отдаёт мировую
        /// высоту крышки плитки.
        /// </summary>
        public static RoadPath Along(IReadOnlyList<HexCoord> chain, Func<HexCoord, float> heightOf)
        {
            if (chain == null || chain.Count == 0)
                throw new ArgumentException("маршрут пуст: линию строить не по чему", nameof(chain));

            var path = new RoadPath();
            path.points.Add(Center(chain[0], heightOf));

            if (chain.Count > 1)
            {
                path.points.Add(Seam(chain[0], chain[1], heightOf));

                // Каждая промежуточная плитка — один поворот: от шва позади до шва впереди через
                // свой центр. Первая выборка дуги пропускается, она совпала бы с прошлым швом.
                for (var i = 1; i < chain.Count - 1; i++)
                {
                    var from = Seam(chain[i - 1], chain[i], heightOf);
                    var control = Center(chain[i], heightOf);
                    var to = Seam(chain[i], chain[i + 1], heightOf);

                    for (var step = 1; step <= CornerSegments; step++)
                        path.points.Add(Bezier(from, control, to, step / (float)CornerSegments));
                }

                path.points.Add(Center(chain[^1], heightOf));
            }

            path.Measure();
            return path;
        }

        /// <summary>
        /// Точка маршрута на заданном расстоянии от начала. За пределами линии берётся её конец:
        /// доставка считает время по плиткам, и округление не должно ронять груз в никуда.
        /// </summary>
        public Sample At(float distance)
        {
            if (points.Count == 1)
                return new Sample(points[0], Vector3.forward, Vector3.up);

            var clamped = Mathf.Clamp(distance, 0f, Length);

            var segment = points.Count - 2;
            for (var i = 1; i < distances.Count; i++)
            {
                if (distances[i] < clamped)
                    continue;

                segment = i - 1;
                break;
            }

            var span = distances[segment + 1] - distances[segment];
            var t = span > 1e-5f ? (clamped - distances[segment]) / span : 0f;
            var forward = (points[segment + 1] - points[segment]).normalized;

            return new Sample(Vector3.Lerp(points[segment], points[segment + 1], t), forward, UpAlong(forward));
        }

        /// <summary>
        /// Нормаль поверхности вдоль линии: вертикаль, наклонённая вместе с уклоном. По ней
        /// повозке достаётся крен, а грузу — посадка на полотно, а не в воздух над ним.
        /// </summary>
        static Vector3 UpAlong(Vector3 forward)
        {
            var side = Vector3.Cross(Vector3.up, forward);
            return side.sqrMagnitude < 1e-8f ? Vector3.up : Vector3.Cross(forward, side).normalized;
        }

        void Measure()
        {
            distances.Clear();
            distances.Add(0f);
            for (var i = 1; i < points.Count; i++)
                distances.Add(distances[i - 1] + Vector3.Distance(points[i - 1], points[i]));
        }

        static Vector3 Center(HexCoord coord, Func<HexCoord, float> heightOf)
        {
            var plane = coord.ToPlane();
            return new Vector3(plane.x, heightOf(coord) + RoadMeshBuilder.SurfaceLift, plane.y);
        }

        /// <summary>Середина общей грани: полотно там стоит на высоте старшей из двух плиток.</summary>
        static Vector3 Seam(HexCoord from, HexCoord to, Func<HexCoord, float> heightOf)
        {
            var plane = (from.ToPlane() + to.ToPlane()) * 0.5f;
            var top = Mathf.Max(heightOf(from), heightOf(to)) + RoadMeshBuilder.SurfaceLift;
            return new Vector3(plane.x, top, plane.y);
        }

        static Vector3 Bezier(Vector3 from, Vector3 control, Vector3 to, float t)
        {
            var inverse = 1f - t;
            return inverse * inverse * from + 2f * inverse * t * control + t * t * to;
        }

        /// <summary>Положение, направление движения и нормаль полотна в одной точке маршрута.</summary>
        public readonly struct Sample
        {
            public Sample(Vector3 position, Vector3 forward, Vector3 up)
            {
                Position = position;
                Forward = forward;
                Up = up;
            }

            public Vector3 Position { get; }

            /// <summary>Единичное направление движения вдоль линии.</summary>
            public Vector3 Forward { get; }

            /// <summary>Единичная нормаль полотна: вертикаль, наклонённая уклоном.</summary>
            public Vector3 Up { get; }
        }
    }
}
