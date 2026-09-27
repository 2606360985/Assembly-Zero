# 游戏工程侦探：零号装配厂 — EP01

EP01 用桌面微缩机器人装配厂演示一个问题：两万件物料运输是否必须逐件更新两万次。实现只复现“运输线/间隙”这一工程思想，不包含、复制或改编 Factorio 的资源、UI、代码或具体实现。

## 启动

- Unity：`6000.4.0f1`，URP `17.4.0`。
- 打开 `Assets/_AssemblyZero/Scenes/EP01_Demo.unity` 后进入 Play Mode。
- 数字键 `1`–`6` 切换固定演示阶段；Stage 6 中 `R` 重播，`G` 手动覆盖闸门状态。
- 场景和参数由 `Tools > Assembly Zero > Rebuild EP01 Demo Scene` 重建，生成内容仅位于 `Assets/_AssemblyZero`。

## 六个阶段

1. Belt Piece、方向及连接诊断。
2. Naive 后端运输真实尺寸与间距的 `CarboardBox04` 货箱，最多 96 件。
3. Naive 蛇形线路压力场景，真实模拟 20,000 Logical Items，均匀抽样显示 128 个代表箱。
4. Transport Line 统计与线路视图。
5. Gap 后端使用与 Stage 3 相同的 20,000 Logical Items、128 个代表箱及间隙/边界数据。
6. 112 个真实间距货箱进行固定 Tick 关闸、堵塞传播、开闸恢复；默认第 180 Tick 关闭、第 480 Tick 打开。

画面右上角会解释当前阶段；场景中的仓库、闸门、缓存区和 R-01 均有世界空间标牌。Stage 1 的白色箭头表示 Belt Piece 输出，Stage 4 的青色覆盖层表示合并后的 Transport Line。

## 后续路径规划约定

当前 EP01 的固定蛇形线路仍由场景生成器中的网格坐标序列生成；第二个掉头已加入一个直线过渡段，避免在尚无弧形模型时贴格急转。下一轮路径系统按以下层次演进：

1. 用 `BeltRouteAsset` 保存有序控制点、入口/出口方向、期望转弯半径和稳定 Route ID，不直接保存渲染 GameObject。
2. Editor 烘焙器把 Route 展开为 Straight、Corner90 和 Transition Piece；先校验重复格、断链、反向相邻、最小转弯空间，再生成 `BeltAuthoring`。
3. Domain 只接收节点、边、Lane、逻辑长度和连接优先级；直线或弧线只改变路径采样，不改变运输算法语义。
4. 表现层使用分段中心线采样。Straight 使用线性采样，Corner90 使用固定半径圆弧和弧长，使托盘运动连续；两侧 Lane 从中心线切线计算法向偏移。
5. 只有未来加入分流和动态目的地后，才在烘焙后的有向图上使用稳定排序的 Dijkstra/A*。代价可组合逻辑长度、转弯惩罚和拥堵权重；相同代价按稳定 ID 决胜，保证确定性。

这样，美术可以替换直角块为圆弧模块，关卡可以改路线，Naive/Gap 两个后端仍复用同一份整数逻辑拓扑。

## 架构与算法

`AssemblyZero.Domain` 是 `noEngineReferences=true` 的纯 C# 程序集。Unity 世界坐标、GameObject、Transform、碰撞和渲染不参与传送规则。Domain 使用每格 1024 个整数单位和 60 Tick/s。

- **NaiveItemBeltSimulation**：每件物品保存绝对逻辑距离，每 Tick 从出口向入口逐件推进并执行最小间距约束。
- **GapTransportLineSimulation**：使用环形缓冲保持顺序；畅通时只修改 Line 位移偏移，堵塞时从出口维护压缩区。Snapshot 才展开绝对距离。
- 两者实现同一个 `IBeltSimulationBackend`，使用相同 Scenario、Topology、Snapshot、哈希和实例渲染器。
- `IBeltAccess`、`BeltAccessPort`、`BeltItemHandle` 是 EP02 机械臂抓取区的接口预留；两个后端均支持固定抓取区查询、移除和插入，但本期不实现机械臂行为。

## 性能指标

- **Simulation ms**：只测量 `IBeltSimulationBackend.Tick`。
- **Presentation ms**：Snapshot、Gap 位置展开、逻辑距离到世界坐标采样、实例矩阵和 UI 数据准备。
- **Values Touched This Tick**：算法工作量指标，不等同于时间。
- **Logical Items / Visible Cargo**：压力场景运行 20,000 个独立逻辑物料，并按车道及逻辑距离均匀抽样 128 个代表箱；HUD 同时显示抽样比例和当前物理线路容量。
- 小负载与闸门阶段使用 512 逻辑单位中心间距，当前 32 段双车道的物理容量约为 128 箱；压力阶段使用 3 逻辑单位测试间距，使 20,000 个逻辑物料可在同一拓扑中运行，但不宣称它们都具有纸箱物理尺寸。
- 两类货物共同使用 `CarboardBox04` 的 24 顶点 Mesh，并分别使用橙色、蓝色材质。物料由 `Graphics.RenderMeshInstanced` 以每批最多 1023 个实例提交；没有物料 MonoBehaviour、Rigidbody、Collider 或实时阴影。
- 传送面使用 `AssemblyZero/ConveyorSurface` URP Shader 表现流动条纹；它只负责视觉，不参与逻辑位移。
- Editor 数字仅供调试。正式记录应使用 1920×1080 Development Build，明确 VSync/帧率限制和硬件。

## 测试

在 Test Runner 运行 EditMode 与 PlayMode：

- 最小间距、顺序、守恒；
- 闸门堵塞与恢复；
- 10,000 Tick 确定性哈希；
- Naive/Gap 外部结果一致；
- 直线/弯道逻辑距离连续；
- 手动 Tick 证明渲染帧率不改变模拟结果；
- Demo 场景加载且不含 Rigidbody。
- Stage 3/5 均运行 20,000 件、使用相同的 128 个代表箱和同一纸箱 Mesh；Stage 6 全量显示 112 个真实间距货箱。

命令行示例：

```powershell
$env:ALLUSERSPROFILE='C:\ProgramData'
unity test . --editor-version 6000.4.0f1 --mode EditMode --output Temp/AssemblyZero-EditMode.xml --timeout 600
unity test . --editor-version 6000.4.0f1 --mode PlayMode --output Temp/AssemblyZero-PlayMode.xml --timeout 600
```

运行 CLI 测试前需关闭同一工程的交互式 Unity Editor，否则 Unity 会因工程锁拒绝第二个实例。

也可使用 `Tools > Assembly Zero > Build Windows Development Demo` 生成只包含 EP01 场景的 Windows Development Build。

本机隔离验证记录（Unity Editor、20,000 items、300 Tick，状态哈希在 Presentation/Snapshot 阶段计算）：Naive 约 `0.3102 ms/tick`、`20,000 touched`；Gap 约 `0.0001 ms/tick`、`2 touched`。这不是跨硬件基准，正式演示应以 HUD 和目标机器 Development Build 记录为准。

## 已知限制

- EP01 只支持普通直线、90° 弯道、双车道、Source、Gate、Sink和一条主要 Transport Line；不支持分流、地下带、动态铺设或复杂交叉。
- 压力阶段的 20,000 是逻辑物料数，画面只提交 128 个代表箱；因此该演示比较的是后端模拟工作量，不代表“同时渲染 20,000 个纸箱”的 GPU 压力测试。不得把 Presentation/Rendering 时间归入 Simulation。
- 当前不使用 DOTS/ECS、Jobs、Burst、Compute Shader 或物理系统。
- Corner90 当前仍使用网格块表现；第二个掉头已有直线过渡，但圆弧网格、弧长采样和切线朝向留待下一轮实现。
- 项目未导入 TMP Essentials；为避免在 `Assets/TextMesh Pro` 生成额外资源，HUD 使用 `_AssemblyZero/UI` 内的本地字体与 uGUI Text。
- EP02 可通过 `IBeltAccess` 在固定抓取区查询/移除/插入物品；本期不处理机械臂动画、占用预约或多机械臂竞争。
