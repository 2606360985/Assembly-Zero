using UnityEngine;

namespace AssemblyZero.Unity
{
    // Continuous multi-carrier elevator. Transport ownership, spacing and
    // backpressure belong to the route backend, never to visual transforms.
    public sealed class FactoryElevatorAuthoring : MonoBehaviour
    {
        public string RouteId;
        public string[] SharedRoutes=System.Array.Empty<string>();
        public int Segment;
        public Vector3 Entry,Exit;
        public Transform Visual;
        public Material CarrierMaterial;
    }
}
