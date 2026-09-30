using System.Text;
using AssemblyZero.Domain;
using UnityEngine;
namespace AssemblyZero.Unity
{
    public sealed class EP02MetricsHud : MonoBehaviour
    {
        public UnityEngine.UI.Text Title, Metrics, Arms, Lesson;
        private readonly StringBuilder text = new StringBuilder(2048);
        public void ShowError(string error) { if (Title != null) Title.text = "EP02 配置错误"; if (Metrics != null) Metrics.text = error; }
        public void Present(EP02SimulationDriver driver)
        {
            var world = driver.Simulation; var s = world.Statistics;
            Title.text = "游戏工程侦探 / 零号装配厂\nEP02 · " + EP02DemoStageController.Titles[driver.Stage];
            text.Clear(); text.Append("Tick ").Append(s.Tick).Append("  /  60 Hz   ").Append(driver.Paused ? "PAUSED" : "RUNNING").AppendLine();
            text.Append("生成 ").Append(s.Generated).Append(" = 带上 ").Append(s.OnBelt).Append(" + 预订 ").Append(s.Reserved).Append(" + 持料 ").Append(s.Held).AppendLine();
            text.Append(" + 安装 ").Append(s.Installed).Append(" + 回收 ").Append(s.Rejected).Append(" + 完成消耗 ").Append(s.CompletedConsumed).AppendLine();
            text.Append("守恒 ").Append(s.Conserved ? "OK" : "ERROR").Append("    完整机器人 ").Append(s.CompletedRobots).Append("    平均装配 ").Append(s.AverageAssemblySeconds.ToString("F2")).AppendLine(" s");
            text.Append("装配单元 ").Append(world.Snapshot.CellState).Append("   输入阻塞 ").Append(s.SourceBlockedTicks).AppendLine(" Tick");
            text.Append("模拟 ").Append(world.SimulationMs.ToString("F3")).Append("  查询/预订 ").Append(world.AccessMs.ToString("F3")).AppendLine(" ms");
            text.Append("哈希 ").Append(world.HashMs.ToString("F3")).Append("  表现 ").Append(driver.PresentationMs.ToString("F3")).AppendLine(" ms");
            text.Append("Belt touched ").Append(s.BeltValuesTouched).Append("   Hash ").Append(s.StateHash.ToString("X16")); Metrics.text = text.ToString();
            text.Clear(); foreach (var a in world.Snapshot.Inserters)
            {
                text.Append("I-").Append(a.Id.ToString("00")).Append(" ").Append(driver.Layout.Part(a.Part).PartName).Append(" → S").Append(a.TargetSocket).AppendLine();
                text.Append(a.State).Append("  ").Append((a.Progress(s.Tick) * 100).ToString("F0")).Append("%  enter ").Append(a.StateEnterTick).AppendLine();
                text.Append("意图#").Append(a.IntentItem).Append("  预订#").Append(a.ReservedItem).Append("  持料#").Append(a.HeldItem).Append("  交接 ").Append(a.Transfers).AppendLine();
                if (driver.Stage == 2) text.Append("最近意图#").Append(a.LastIntentItem).Append(" @ Tick ").Append(a.LastIntentTick).AppendLine();
                text.Append("Busy ").Append(a.BusyPercent.ToString("F0")).Append("% / Starved ").Append(a.StarvedPercent.ToString("F0")).Append("% / Blocked ").Append(a.BlockedPercent.ToString("F0")).AppendLine("%\n");
            }
            Arms.text = text.ToString();
            Lesson.text = driver.Stage == 1 ? "黄色：预订仍在带上\nPicking 完成的 Tick 才从皮带转为夹爪所有权。\n右侧动画对照没有物流权限。" : driver.Stage == 2 ? "观察两个 I 的意图 #：同一候选、稳定 ID 仲裁。\n物料与目标 Socket 同时预订，不会重复抓取。" : driver.Stage == 4 ? "Head 持料时锁定；WaitingForTarget 保留载荷。\nL 解锁恢复，未抓取部件只能进入回收。" : driver.Stage == 5 ? "来料 6 Tick，机械臂耗时 ×2，回收 60 Tick/件。\n末端等待件留在皮带上，堵塞向入口传播。" : "六槽填满后等待机械臂安全返回。\n扫描 90 Tick，展示 180 Tick；完成事件只计一次。";
        }
    }
}
