using UnityEngine;
namespace AssemblyZero.Unity
{
    public sealed class EP02DemoStageController : MonoBehaviour
    {
        public EP02SimulationDriver Driver;
        public static readonly string[] Titles = { "", "所有权交接：动画不等于物流", "共享 Port：意图可以冲突，预订必须互斥", "六类部件：类型过滤与连续装配", "锁定 HeadSocket：持料等待与漏抓回收", "真实背压：快来料 → 漏抓 → 回收饱和", "平衡生产：安全退臂 → 扫描 → 完整机器人" };
        public void SelectStage(int stage) { Driver.ResetStage(stage); }
    }
}
