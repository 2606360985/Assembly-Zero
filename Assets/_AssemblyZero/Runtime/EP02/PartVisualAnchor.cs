using UnityEngine;
namespace AssemblyZero.Unity
{
    public sealed class PartVisualAnchor : MonoBehaviour
    {
        public Transform PayloadRoot, SourceRoot;
        public GameObject Carrier;
        public RobotPartDefinition Definition;
    }
}
