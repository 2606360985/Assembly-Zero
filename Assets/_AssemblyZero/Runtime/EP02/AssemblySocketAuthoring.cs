using UnityEngine;
namespace AssemblyZero.Unity
{
    public sealed class AssemblySocketAuthoring : MonoBehaviour
    {
        public int StableId, PartTypeId;
        public Renderer Indicator;
        public GameObject Ghost;
        public bool InitiallyLocked;
    }
}
