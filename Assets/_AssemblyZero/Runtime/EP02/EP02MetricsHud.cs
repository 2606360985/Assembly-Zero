using System.Text;
using System.Linq;
using UnityEngine;
namespace AssemblyZero.Unity
{
    public sealed class EP02MetricsHud : MonoBehaviour
    {
        public UnityEngine.UI.Text Title, Metrics, Arms, Lesson;
        private readonly StringBuilder text = new StringBuilder(2048);
        public void ShowError(string error) { Title.text = "EP02 配置错误"; Metrics.text = error; }
        public void Present(EP02SimulationDriver d)
        {
            var w = d.Simulation; var s = w.Statistics; var p = w.Snapshot;
            Title.text = "零号装配厂 / EP02\n六轴机械臂 · 连续装配产线";
            text.Clear(); text.Append("Tick ").Append(s.Tick).Append(d.Paused ? "   PAUSED" : "   RUNNING").AppendLine(" / 60 Hz");
            text.Append("完成 ").Append(s.CompletedRobots).Append("   在制 ").Append(p.Products.Count(x=>!x.Completed)).Append("   出库 ").Append(p.ExitedProducts).AppendLine();
            text.Append("平均生产 ").Append(s.AverageAssemblySeconds.ToString("F1")).AppendLine(" s");
            text.Append("物料 ").Append(s.Generated).Append(" = 带上 ").Append(s.OnBelt).Append(" + 预订 ").Append(s.Reserved).Append(" + 持料 ").Append(s.Held).AppendLine();
            text.Append(" + 安装 ").Append(s.Installed).Append(" + 回收 ").Append(s.Rejected).Append(" + 完成消耗 ").Append(s.CompletedConsumed).AppendLine();
            text.Append("守恒 ").Append(s.Conserved && p.ProductsConserved ? "OK" : "ERROR").Append("   入口等待 ").Append(p.ProductSourceBlockedTicks).AppendLine(" Tick");
            text.Append("模拟 ").Append(w.SimulationMs.ToString("F2")).Append(" / 查询 ").Append(w.AccessMs.ToString("F2")).AppendLine(" ms");
            text.Append("表现 ").Append(d.PresentationMs.ToString("F2")).Append(" / Hash ").Append(w.HashMs.ToString("F2")).AppendLine(" ms");
            text.Append("Belt touched ").Append(s.BeltValuesTouched).Append(" / ").Append(s.StateHash.ToString("X16")); Metrics.text = text.ToString();
            text.Clear(); foreach (var a in p.Inserters)
            {
                text.Append("工位 ").Append(a.StationId + 1).Append(" / ").Append(d.Layout.Part(a.Part).PartName).Append(" → P-").Append(a.TargetProductId.ToString("000")).AppendLine();
                text.Append(a.State).Append("   ").Append((a.Progress(s.Tick)*100).ToString("F0")).AppendLine("%");
                text.Append("预订 #").Append(a.ReservedItem).Append(" / 持料 #").Append(a.HeldItem).Append(" / 安装 ").Append(a.Transfers).AppendLine();
                text.Append("Busy ").Append(a.BusyPercent.ToString("F0")).Append("% / Blocked ").Append(a.BlockedPercent.ToString("F0")).AppendLine("%\n");
            }
            Arms.text = text.ToString(); text.Clear();
            foreach (var product in p.Products.Take(8)) text.Append("P-").Append(product.Id.ToString("000")).Append("  ").Append(product.Sockets.Count(x=>x.ItemId!=0)).Append("/6  ").Append(product.State).AppendLine();
            if (p.Products.Count > 8) text.Append("另有 ").Append(p.Products.Count - 8).AppendLine(" 台产品在队列中");
            Lesson.text = text.Length == 0 ? "产品托盘等待进入产线" : text.ToString();
        }
    }
}
