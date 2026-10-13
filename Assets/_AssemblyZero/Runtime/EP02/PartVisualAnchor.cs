using UnityEngine;
namespace AssemblyZero.Unity
{
    public sealed class PartVisualAnchor : MonoBehaviour
    {
        public Transform PayloadRoot, SourceRoot;
        public GameObject Carrier;
        public RobotPartDefinition Definition;
        public void AttachPayload(Transform parent)
        {
            PayloadRoot.SetParent(parent, false);
            // SetParent(false) preserves the previous LOCAL transform. In particular,
            // the grip's world-orientation compensation must not reach the socket.
            PayloadRoot.localPosition = Vector3.zero;
            PayloadRoot.localRotation = Quaternion.identity;
            PayloadRoot.localScale = Vector3.one;
        }
    }
}
