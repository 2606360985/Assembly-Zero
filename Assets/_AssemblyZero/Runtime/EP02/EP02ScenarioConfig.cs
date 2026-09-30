using System;
using System.Collections.Generic;
using AssemblyZero.Domain;
using UnityEngine;
namespace AssemblyZero.Unity
{
    [CreateAssetMenu(menuName = "Assembly Zero/EP02 Scenario")]
    public sealed class EP02ScenarioConfig : ScriptableObject
    {
        public BackendKind Backend = BackendKind.Gap;
        public int BeltSpeed = 32, OccupancyUnits = 768, BalancedSourceInterval = 24, OverloadSourceInterval = 6, RejectInterval = 60, ScanTicks = 90, ShowcaseTicks = 180;
        public AssemblyScenarioDefinition Create(int stage, EP02LayoutAuthoring layout, BakedBeltTopology topology)
        {
            if (stage < 1 || stage > 6) throw new ArgumentOutOfRangeException(nameof(stage));
            var c = new AssemblyScenarioDefinition { Seed = 2000 + stage, BeltSpeed = BeltSpeed, MinimumSpacing = OccupancyUnits, SourceInterval = stage == 5 ? OverloadSourceInterval : BalancedSourceInterval, RejectInterval = RejectInterval, ScanTicks = ScanTicks, ShowcaseTicks = ShowcaseTicks, FullRobotManifest = stage > 2, SourceEnabled = stage > 2 };
            var arms = new List<InserterData>(); var sockets = new List<AssemblySocketData>(); var parts = new List<PartTypeId>();
            foreach (var socket in layout.Sockets) if (stage > 2 || socket.PartTypeId == 2) { sockets.Add(new AssemblySocketData { Id = new AssemblySocketId(socket.StableId), AcceptedPart = new PartTypeId(socket.PartTypeId), Locked = socket.InitiallyLocked }); parts.Add(new PartTypeId(socket.PartTypeId)); }
            Array.Sort(layout.Inserters, (a, b) => a.StableId.CompareTo(b.StableId));
            foreach (var author in layout.Inserters)
            {
                if (stage <= 2 && author.StableId >= stage) continue;
                var pickup = stage <= 2 ? layout.Arm(0) : author;
                var type = stage <= 2 ? 2 : author.PartTypeId; var target = stage <= 2 ? layout.Part(2).SocketId : author.SocketId; var slow = stage == 5 ? 2 : 1;
                arms.Add(new InserterData { Id = new InserterId(author.StableId), AllowedPart = new PartTypeId(type), TargetSocket = new AssemblySocketId(target), Port = new BeltAccessPort(new LineId(0), (ulong)pickup.LaneMask, pickup.PreferredDistance - pickup.PortWidth / 2, pickup.PreferredDistance + pickup.PortWidth / 2, pickup.PreferredDistance, new PartTypeId(type).Mask), PhaseTicks = stage <= 2 ? 0 : author.PhaseTicks, PickingTicks = author.PickingTicks * slow, CarryingTicks = author.CarryingTicks * slow, PlacingTicks = author.PlacingTicks * slow, ReturningTicks = author.ReturningTicks * slow });
            }
            c.Inserters = arms.ToArray(); c.Sockets = sockets.ToArray(); c.SourceParts = parts.ToArray(); c.Manifest = new AssemblyManifest { RequiredParts = parts.ToArray() };
            var initial = new List<InitialAssemblyPart>();
            if (stage <= 2) initial.Add(new InitialAssemblyPart(2, 0, layout.Arm(0).PreferredDistance - 256));
            else foreach (var arm in arms) initial.Add(new InitialAssemblyPart(arm.AllowedPart.Value, arm.Id.Value % 2, arm.Port.PreferredDistance - 256));
            c.InitialParts = initial.ToArray(); c.Validate(topology.DomainTopology); return c;
        }
    }
}
