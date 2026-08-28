using System;
using System.Collections.Generic;
using Game.Grid;
using UnityEngine;

namespace Game.Economy
{
    /// <summary>Один везомый ресурс: путь фиксируется при отправке и не пересчитывается.</summary>
    public sealed class Delivery
    {
        public Delivery(ResourceType type, IReadOnlyList<HexCoord> path, float duration)
        {
            Type = type;
            Path = path;
            Duration = duration;
        }

        public ResourceType Type { get; }

        public IReadOnlyList<HexCoord> Path { get; }

        public float Duration { get; }

        public float Elapsed { get; private set; }

        public bool HasArrived => Elapsed >= Duration;

        public float Progress => Duration <= 0f ? 1f : Mathf.Clamp01(Elapsed / Duration);

        /// <summary>Положение на маршруте: Lerp по точкам пути, без физики.</summary>
        public Vector2 Position
        {
            get
            {
                if (Path.Count == 1)
                    return Path[0].ToWorld();

                var travelled = Progress * (Path.Count - 1);
                var index = Mathf.Min(Mathf.FloorToInt(travelled), Path.Count - 2);
                return Vector2.Lerp(Path[index].ToWorld(), Path[index + 1].ToWorld(), travelled - index);
            }
        }

        public void Advance(float deltaTime) => Elapsed += deltaTime;
    }

    /// <summary>Доставка ресурсов на склад: секунда на плитку пути.</summary>
    public sealed class DeliverySystem
    {
        readonly float secondsPerTile;
        readonly List<Delivery> active = new();
        readonly List<Delivery> arrived = new();

        public DeliverySystem(float secondsPerTile)
        {
            this.secondsPerTile = secondsPerTile;
        }

        public event Action<Delivery> Started;

        public event Action<Delivery> Arrived;

        public IReadOnlyList<Delivery> Active => active;

        public Delivery Send(ResourceType type, IReadOnlyList<HexCoord> path)
        {
            var delivery = new Delivery(type, path, (path.Count - 1) * secondsPerTile);
            active.Add(delivery);
            Started?.Invoke(delivery);
            return delivery;
        }

        public void Tick(float deltaTime)
        {
            arrived.Clear();

            foreach (var delivery in active)
            {
                delivery.Advance(deltaTime);
                if (delivery.HasArrived)
                    arrived.Add(delivery);
            }

            foreach (var delivery in arrived)
            {
                active.Remove(delivery);
                Arrived?.Invoke(delivery);
            }
        }
    }
}
