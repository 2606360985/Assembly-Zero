using System;
using System.Linq;
using AssemblyZero.Domain;
using UnityEngine;
namespace AssemblyZero.Unity
{
    [CreateAssetMenu(menuName = "Assembly Zero/EP02 Scenario")]
    public sealed class EP02ScenarioConfig : ScriptableObject
    {
        public BackendKind Backend = BackendKind.Gap;
        public int SupplySpeedA = 32, SupplySpeedB = 24, ProductSpeed = 16, OccupancyUnits = 768, SourceInterval = 24, ProductInterval = 240, RejectInterval = 60, ScanTicks = 90, ShowcaseTicks = 180;
        public AssemblyScenarioDefinition Create(EP02LayoutAuthoring layout, EP02BakedLayout topology)
        {
            var c = new AssemblyScenarioDefinition { SupplySpeeds = layout.HasDedicatedSupplies ? Enumerable.Repeat(SupplySpeedA, topology.Supply.Length).ToArray() : new[] { SupplySpeedA, SupplySpeedB }, ProductSpeed = ProductSpeed, ProductInterval = ProductInterval, MinimumSpacing = OccupancyUnits, SourceInterval = SourceInterval, RejectInterval = RejectInterval, ScanTicks = ScanTicks, ShowcaseTicks = ShowcaseTicks, ProductLength = topology.Product.Length, ProductSpacing = Mathf.CeilToInt(layout.ProductSpacing * EP02Route.UnitsPerMeter), SegmentEnds = (int[])topology.Product.Ends.Clone() };
            c.Sockets = layout.Sockets.Select(s => new AssemblySocketData { Id = new AssemblySocketId(s.StableId), AcceptedPart = new PartTypeId(s.PartTypeId), Locked = s.InitiallyLocked }).ToArray();
            c.Manifest = new AssemblyManifest { RequiredParts = c.Sockets.Select(s => s.AcceptedPart).ToArray() };
            c.Stations = layout.Inserters.OrderBy(a => a.StableId).Select(a => new AssemblyStationDefinition { Id = a.StableId, Distance = topology.Product.CenterDistance(a.StationPiece), SocketId = a.SocketId, PartType = a.PartTypeId }).ToArray();
            c.Inserters = layout.Inserters.Select(a => new InserterData { Id = new InserterId(a.StableId), AllowedPart = new PartTypeId(a.PartTypeId), TargetSocket = new AssemblySocketId(a.SocketId), StationId = a.StableId, PickingTicks = a.PickingTicks, CarryingTicks = a.CarryingTicks, PlacingTicks = a.PlacingTicks, ReturningTicks = a.ReturningTicks, Ports = layout.PickupLanes(a).Select(l => new BeltAccessPort(new LineId(l), 1UL << l, a.PickupDistances[l] - a.PortWidth / 2, a.PickupDistances[l] + a.PortWidth / 2, a.PickupDistances[l], new PartTypeId(a.PartTypeId).Mask)).ToArray() }).ToArray();
            c.Validate(topology.SupplyTopology); return c;
        }
    }
}
