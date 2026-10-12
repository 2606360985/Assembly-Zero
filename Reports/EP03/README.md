# EP03 参考图布局修订验证

2026-10-12，主工程 `E:/HP_E/GITHUB_SKY/Assembly-Zero`，Unity 6000.4.0f1。当前全厂与最小布局均为 **Candidate**；撤销前版 V1，不宣称已经冻结验收。

本轮布局：左上三仓、左中三列四排基础加工、左下三排四列零件制造、中央六个开放式部件单元、右中横向六工位总装、右侧有限出库泊位。右下扩建区没有生产运输线路。路径沿设备占地外侧正交转弯，交叉处局部跨越，灰色带床配护栏与支脚。总装增加六个编号的门架，保持原机械臂连杆、原件、安装槽和装配顺序。

- [ReferenceLayoutTests.json](ReferenceLayoutTests.json)：本轮 EditMode **56/56**，包括 EP01/EP02 回归、新路径正交与扩建区避让、两种规模生产、账本及 Visual 替换。
- [PlayMode.xml](PlayMode.xml)：最终路径重新执行的 PlayMode **4/4**，包括 EP01/EP02、EP03 生产暂停恢复及不同状态下的重置。
- [Acceptance.txt](Acceptance.txt)：实际新布局 **19,123 Tick 交付 10 台**，30 台设备均完成批次；满出库背压、恢复、原料停供及恢复、账本审计通过。采样点 915 件在途、162 件缓存、4042 条账本、1951 个批次。
- [Generation.txt](Generation.txt)：重复生成数量和接口一致，45 个功能模块、83 条运输连接、27 个模块 Prefab，14,501 个 Transform（含灰盒护栏、支脚和门架）。
- [LayoutBaseline.txt](LayoutBaseline.txt)：Candidate 状态说明；[Snapshot.txt](Snapshot.txt)：新版路线快照 CPU 观察。
- [全景](View-0.png)、[基础加工](View-1.png)、[零件制造](View-2.png)、[部件装配](View-3.png)、[横向总装](View-4.png)、[抓取近景](View-5.png)、[有限出库队列](View-6.png)：新版实际 Editor 渲染截图，1920×1080。

固定 Tick CPU（包含路线快照）平均 0.4245 ms，P95 0.6655 ms，最大 40.2303 ms。加速 Tick 测量不能换算成玩家帧率；首次表现对象分配一次 Present 120.6800 ms。

通道最后一次收敛前的候选版另在连接的交互 Editor 中采样，详见 [InteractiveEditorFrameContext.txt](InteractiveEditorFrameContext.txt)、[InteractiveEditorFrameSamples.csv](InteractiveEditorFrameSamples.csv)。硬件为 Intel Xeon Silver 4210、Quadro RTX 5000、32415 MB RAM；GC 包含测试框架与采样记录的开销。

| 指标 | 平均 | P95 | 最大 |
|---|---:|---:|---:|
| frame_ms | 20.7820 | 22.9518 | 33.9138 |
| simulation_ms | 1.0054 | 1.5053 | 2.6435 |
| presentation_ms | 5.1877 | 5.5899 | 6.7661 |
| audit_ms | 0.0232 | 0.0000 | 11.1849 |
| gc_allocated_bytes | 263978.0830 | 815475.0000 | 2022633.0000 |

没有固定 Tick 或审计的帧，相应字段为零。没有独立 Player 性能达标结论。工程 manifest 的 URP 17.6 声明与当前解析的 17.4、共享 GlobalSettings 版本不一致，前版 Windows Development 构建失败见 [Build.txt](Build.txt)；本轮未升级 Unity 或包、未再次尝试构建。

剩余限制：左侧供料通道仍比较密集，局部跨越最高约 4.3m，后续可以继续收敛路线；没有把平面交叉当作物流连接。共同端口按既有轮询规则仲裁，各连接仍独立持有运输后端，不模拟逐件物理碰撞。历史账本持续增长；返修无不合格判定、第二产品无运行逻辑。旧 EditMode.xml 是前版历史记录；最终布局使用 ReferenceLayoutTests.json 与最新 PlayMode.xml。ReferenceLayoutPlayMode.json 保留最后一次通道收敛之前的交互 Editor 回归记录。

最终路径另外执行 Batch Editor PlayMode 的 10 秒采样，实际渲染尺寸和采样帧数见 [FrameContext.txt](FrameContext.txt)，逐帧数据见 [EditorFrameSamples.csv](EditorFrameSamples.csv)。它与上面的 4K 交互 Editor 采样条件不同，不能直接比较。

| 指标 | 平均 | P95 | 最大 |
|---|---:|---:|---:|
| frame_ms | 6.6967 | 8.6791 | 36.1105 |
| simulation_ms | 0.3499 | 0.9826 | 21.7862 |
| presentation_ms | 4.9400 | 5.9927 | 11.2827 |
| audit_ms | 0.0085 | 0.0000 | 12.6710 |
| gc_allocated_bytes | 156194.7564 | 398496.0000 | 2431946.0000 |
