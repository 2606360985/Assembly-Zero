using System.Linq;
using AssemblyZero.Domain;
using UnityEngine;
namespace AssemblyZero.Unity
{
    public sealed class AssemblyCellPresenter : MonoBehaviour
    {
        public EP02LayoutAuthoring Layout;
        public Transform Scanner;
        public Renderer[] ProgressRing;
        public GameObject[] DisplayParts;
        public TextMesh Label;
        public int DisplayedProductId { get; private set; }
        private long shownCompletionTick = -1;
        private ProductSnapshot showcase;
        private MaterialPropertyBlock block;
        public void Clear() { DisplayedProductId = 0; shownCompletionTick = -1; showcase = null; }
        public void Present(AssemblySnapshot snapshot, EP02ScenarioConfig config)
        {
            var tracked = snapshot.Products.FirstOrDefault(p => p.Id == DisplayedProductId);
            if (tracked != null && tracked.Completed && shownCompletionTick != tracked.CompletedTick) { showcase = tracked; shownCompletionTick = tracked.CompletedTick; }
            if (showcase == null && snapshot.LatestCompleted != null && DisplayedProductId == snapshot.LatestCompleted.Id && shownCompletionTick != snapshot.LatestCompleted.CompletedTick) { showcase = snapshot.LatestCompleted; shownCompletionTick = showcase.CompletedTick; }
            var selected = showcase != null && snapshot.Tick - shownCompletionTick < config.ShowcaseTicks ? showcase : snapshot.Products.FirstOrDefault(p => !p.Completed);
            if (selected == null) selected = snapshot.LatestCompleted;
            if (showcase != null && snapshot.Tick - shownCompletionTick >= config.ShowcaseTicks) showcase = null;
            DisplayedProductId = selected?.Id ?? 0; var count = 0;
            foreach (var author in Layout.Sockets)
            {
                var s = selected?.Sockets.FirstOrDefault(x => x.Id == author.StableId) ?? default; var occupied = s.ItemId != 0; if (occupied) count++;
                author.Ghost.SetActive(!occupied); DisplayParts[author.StableId].SetActive(occupied);
                Tint(author.Indicator, s.Locked ? Color.red : occupied ? Color.cyan : selected != null && s.ReservedBy >= 0 ? Color.yellow : Color.gray);
            }
            for (var i = 0; i < ProgressRing.Length; i++) Tint(ProgressRing[i], selected?.Completed == true ? Color.cyan : i < count * ProgressRing.Length / 6 ? Color.green : new Color(.12f,.2f,.24f));
            if (Scanner != null) { Scanner.gameObject.SetActive(selected?.State == ProductState.Scanning); Scanner.localPosition = new Vector3(0, .7f + Mathf.PingPong(snapshot.Tick / 30f, 2.8f), 0); }
            if (Label != null) Label.text = "装配状态可视化  /  P-" + DisplayedProductId.ToString("000") + "\n进度 " + count + "/6    已完成 " + snapshot.Statistics.CompletedRobots;
        }
        private void Tint(Renderer r, Color c) { if (r == null) return; block ??= new MaterialPropertyBlock(); block.SetColor("_BaseColor",c); block.SetColor("_EmissionColor",c); r.SetPropertyBlock(block); }
    }
}
