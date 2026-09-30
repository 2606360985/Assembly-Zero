using UnityEngine;
namespace AssemblyZero.Unity
{
    public sealed class InserterAuthoring : MonoBehaviour
    {
        public int StableId, PartTypeId, SocketId, PreferredDistance;
        public int PortWidth = 2048, PickingTicks = 12, CarryingTicks = 24, PlacingTicks = 12, ReturningTicks = 24, PhaseTicks;
        public int LaneMask = 3;
        public Transform Grip, Shoulder, Elbow;
        public Transform[] Links;
        public Renderer StatusLight;
        public float Reach = 7.5f;
    }
}
