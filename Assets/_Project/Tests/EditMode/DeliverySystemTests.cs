using System.Collections.Generic;
using Game.Economy;
using Game.Grid;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class DeliverySystemTests
    {
        static List<HexCoord> Path(params HexCoord[] coords) => new(coords);

        [Test]
        public void Duration_IsOneSecondPerTileOfThePath()
        {
            var deliveries = new DeliverySystem(1f);

            var delivery = deliveries.Send(
                ResourceType.Wood,
                Path(new HexCoord(3, 0), new HexCoord(2, 0), new HexCoord(1, 0), HexCoord.Zero));

            Assert.AreEqual(3f, delivery.Duration, 1e-4f);
        }

        [Test]
        public void TileNextToMetropolis_TakesOneSecond()
        {
            var deliveries = new DeliverySystem(1f);

            var delivery = deliveries.Send(ResourceType.Stone, Path(new HexCoord(1, 0), HexCoord.Zero));

            Assert.AreEqual(1f, delivery.Duration, 1e-4f);
        }

        [Test]
        public void Delivery_ArrivesExactlyAfterItsDuration()
        {
            var deliveries = new DeliverySystem(1f);
            var arrived = new List<Delivery>();
            deliveries.Arrived += arrived.Add;
            deliveries.Send(ResourceType.Ore, Path(new HexCoord(2, 0), new HexCoord(1, 0), HexCoord.Zero));

            deliveries.Tick(1.9f);
            Assert.IsEmpty(arrived);

            deliveries.Tick(0.2f);
            Assert.AreEqual(1, arrived.Count);
            Assert.AreEqual(ResourceType.Ore, arrived[0].Type);
            Assert.IsEmpty(deliveries.Active);
        }

        [Test]
        public void Position_MovesAlongThePathPoints()
        {
            var deliveries = new DeliverySystem(1f);
            var delivery = deliveries.Send(ResourceType.Wood, Path(new HexCoord(2, 0), new HexCoord(1, 0), HexCoord.Zero));

            Assert.AreEqual(new HexCoord(2, 0).ToWorld(), delivery.Position);

            deliveries.Tick(1f);
            Assert.AreEqual(new HexCoord(1, 0).ToWorld(), delivery.Position, "на середине пути ресурс стоит на средней плитке");

            deliveries.Tick(1f);
            Assert.AreEqual(Vector2.zero, delivery.Position);
        }

        [Test]
        public void Path_IsFixedAtSendTimeAndDoesNotChange()
        {
            var deliveries = new DeliverySystem(1f);
            var path = Path(new HexCoord(1, 0), HexCoord.Zero);

            var delivery = deliveries.Send(ResourceType.Wood, path);

            Assert.AreSame(path, delivery.Path);
            Assert.AreEqual(2, delivery.Path.Count);
        }

        [Test]
        public void SeveralDeliveries_TravelIndependently()
        {
            var deliveries = new DeliverySystem(1f);
            var arrived = new List<Delivery>();
            deliveries.Arrived += arrived.Add;

            deliveries.Send(ResourceType.Wood, Path(new HexCoord(1, 0), HexCoord.Zero));
            deliveries.Send(ResourceType.Stone, Path(new HexCoord(3, 0), new HexCoord(2, 0), new HexCoord(1, 0), HexCoord.Zero));

            deliveries.Tick(1f);
            Assert.AreEqual(1, arrived.Count);
            Assert.AreEqual(1, deliveries.Active.Count);

            deliveries.Tick(2f);
            Assert.AreEqual(2, arrived.Count);
            Assert.IsEmpty(deliveries.Active);
        }
    }
}
