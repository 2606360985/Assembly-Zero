using UnityEngine;

namespace AssemblyZero.Unity
{
    public sealed class StagePresentationController : MonoBehaviour
    {
        [SerializeField] private GameObject topologyOverlay;
        [SerializeField] private GameObject lineOverlay;
        [SerializeField] private Renderer gateIndicator;
        [SerializeField] private Transform gateBarrier;
        private MaterialPropertyBlock block;

        public void Configure(GameObject topology, GameObject line, Renderer indicator, Transform barrier)
        { topologyOverlay = topology; lineOverlay = line; gateIndicator = indicator; gateBarrier = barrier; }

        public void ApplyStage(int stage)
        {
            if (topologyOverlay != null) topologyOverlay.SetActive(stage == 1);
            if (lineOverlay != null) lineOverlay.SetActive(stage == 4);
        }

        public void SetGate(bool open)
        {
            if (gateIndicator != null) { block ??= new MaterialPropertyBlock(); gateIndicator.GetPropertyBlock(block); var color = open ? new Color(0.08f, 1f, 0.32f) : new Color(1f, 0.04f, 0.03f); block.SetColor("_BaseColor", color); block.SetColor("_EmissionColor", color * 3f); gateIndicator.SetPropertyBlock(block); }
            if (gateBarrier != null) { var target = open ? new Vector3(0, 1.65f, 0) : new Vector3(0, 0.72f, 0); gateBarrier.localPosition = Vector3.Lerp(gateBarrier.localPosition, target, 0.25f); }
        }
    }
}
