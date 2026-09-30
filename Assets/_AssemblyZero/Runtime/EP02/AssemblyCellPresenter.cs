using AssemblyZero.Domain;
using UnityEngine;
namespace AssemblyZero.Unity
{
    public sealed class AssemblyCellPresenter : MonoBehaviour
    {
        public EP02LayoutAuthoring Layout;
        public Transform Scanner;
        public Renderer[] ProgressRing;
        private MaterialPropertyBlock block;
        public void Present(AssemblySnapshot snapshot, EP02ScenarioConfig config)
        {
            var count = 0;
            foreach (var author in Layout.Sockets)
            {
                var occupied = false; var locked = false; var reserved = false;
                foreach (var s in snapshot.Sockets) if (s.Id == author.StableId) { occupied = s.ItemId != 0; locked = s.Locked; reserved = s.ReservedBy >= 0; break; }
                if (occupied) count++;
                if (author.Ghost != null) author.Ghost.SetActive(!occupied);
                Tint(author.Indicator, locked ? Color.red : occupied ? Color.cyan : reserved ? Color.yellow : Color.gray);
            }
            for (var i = 0; i < ProgressRing.Length; i++) Tint(ProgressRing[i], snapshot.CellState == AssemblyCellState.Showcase ? Color.cyan : i < count * ProgressRing.Length / 6 ? Color.green : new Color(.12f, .2f, .24f));
            if (Scanner != null) { Scanner.gameObject.SetActive(snapshot.CellState == AssemblyCellState.Scanning); Scanner.localPosition = new Vector3(0, .7f + 2.5f * Mathf.Clamp01((snapshot.Tick - snapshot.CellStateEnterTick) / (float)config.ScanTicks), 0); }
        }
        private void Tint(Renderer r, Color color) { if (r == null) return; block ??= new MaterialPropertyBlock(); block.SetColor("_BaseColor", color); block.SetColor("_EmissionColor", color); r.SetPropertyBlock(block); }
    }
}
