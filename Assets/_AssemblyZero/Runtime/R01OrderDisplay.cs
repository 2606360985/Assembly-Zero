using UnityEngine;

namespace AssemblyZero.Unity
{
    public sealed class R01OrderDisplay : MonoBehaviour
    {
        [SerializeField] private Renderer leftArmRenderer;
        [SerializeField] private UnityEngine.UI.Text orderText;
        [SerializeField] private Color pending = new Color(0.1f, 0.35f, 0.42f, 1);
        [SerializeField] private Color complete = new Color(0.2f, 1f, 0.9f, 1);
        private MaterialPropertyBlock block;
        public void Configure(Renderer leftArm, UnityEngine.UI.Text label) { leftArmRenderer = leftArm; orderText = label; }
        public void SetProgress(int value, int target) { if (orderText != null) orderText.text = $"R-01 左臂关节订单  {value}/{target}"; if (leftArmRenderer != null) { block ??= new MaterialPropertyBlock(); leftArmRenderer.GetPropertyBlock(block); block.SetColor("_BaseColor", value >= target ? complete : pending); block.SetColor("_EmissionColor", (value >= target ? complete : pending) * (value >= target ? 3f : 0.5f)); leftArmRenderer.SetPropertyBlock(block); } }
    }
}
