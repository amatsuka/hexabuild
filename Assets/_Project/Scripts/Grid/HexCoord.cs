using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Grid
{
    /// <summary>Axial-координата гекса с pointy-top ориентацией.</summary>
    public readonly struct HexCoord : IEquatable<HexCoord>
    {
        /// <summary>Ширина гекса (расстояние между противоположными гранями) в юнитах.</summary>
        public const float Width = 1f;

        /// <summary>Радиус описанной окружности: расстояние от центра до вершины.</summary>
        public static readonly float Size = Width / Mathf.Sqrt(3f);

        static readonly HexCoord[] DirectionOffsets =
        {
            new(1, 0), new(1, -1), new(0, -1), new(-1, 0), new(-1, 1), new(0, 1)
        };

        public static IReadOnlyList<HexCoord> Directions => DirectionOffsets;

        public static HexCoord Zero => new(0, 0);

        public readonly int Q;
        public readonly int R;

        public HexCoord(int q, int r)
        {
            Q = q;
            R = r;
        }

        public int S => -Q - R;

        public HexCoord Neighbor(int direction) => this + DirectionOffsets[direction];

        /// <summary>Шесть соседей по часовой стрелке, начиная с направления +Q.</summary>
        public HexCoord[] Neighbors()
        {
            var result = new HexCoord[DirectionOffsets.Length];
            for (var i = 0; i < DirectionOffsets.Length; i++)
                result[i] = this + DirectionOffsets[i];
            return result;
        }

        /// <summary>
        /// Детерминированный хеш координаты в [0, 1). `GetHashCode` для этого не годится: он идёт
        /// через `HashCode.Combine`, а тот подмешивает случайное на процесс зерно — и одна и та же
        /// карта выглядела бы по-разному при каждом запуске. <paramref name="salt"/> даёт
        /// независимые потоки: количество декора, его позиция и оттенок не должны быть связаны.
        /// </summary>
        public float Hash01(int salt)
        {
            unchecked
            {
                var hash = Q * 73856093 ^ R * 19349663 ^ salt * 83492791;
                hash = (hash ^ (hash >> 13)) * 1274126177;
                hash ^= hash >> 16;
                return (hash & 0x7fffffff) / 2147483648f;
            }
        }

        public static int Distance(HexCoord a, HexCoord b)
        {
            var dq = a.Q - b.Q;
            var dr = a.R - b.R;
            return (Mathf.Abs(dq) + Mathf.Abs(dq + dr) + Mathf.Abs(dr)) / 2;
        }

        public Vector2 ToWorld() => new(Size * Mathf.Sqrt(3f) * (Q + R * 0.5f), Size * 1.5f * R);

        public static HexCoord FromWorld(Vector2 world)
        {
            var q = (Mathf.Sqrt(3f) / 3f * world.x - world.y / 3f) / Size;
            var r = 2f / 3f * world.y / Size;
            return Round(q, r);
        }

        /// <summary>Округление дробных axial-координат до ближайшего гекса через кубические координаты.</summary>
        public static HexCoord Round(float q, float r)
        {
            var s = -q - r;
            var roundedQ = Mathf.RoundToInt(q);
            var roundedR = Mathf.RoundToInt(r);
            var roundedS = Mathf.RoundToInt(s);

            var deltaQ = Mathf.Abs(roundedQ - q);
            var deltaR = Mathf.Abs(roundedR - r);
            var deltaS = Mathf.Abs(roundedS - s);

            if (deltaQ > deltaR && deltaQ > deltaS)
                roundedQ = -roundedR - roundedS;
            else if (deltaR > deltaS)
                roundedR = -roundedQ - roundedS;

            return new HexCoord(roundedQ, roundedR);
        }

        public static HexCoord operator +(HexCoord a, HexCoord b) => new(a.Q + b.Q, a.R + b.R);

        public static HexCoord operator -(HexCoord a, HexCoord b) => new(a.Q - b.Q, a.R - b.R);

        public static bool operator ==(HexCoord a, HexCoord b) => a.Equals(b);

        public static bool operator !=(HexCoord a, HexCoord b) => !a.Equals(b);

        public bool Equals(HexCoord other) => Q == other.Q && R == other.R;

        public override bool Equals(object obj) => obj is HexCoord other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Q, R);

        public override string ToString() => $"({Q}, {R})";
    }
}
