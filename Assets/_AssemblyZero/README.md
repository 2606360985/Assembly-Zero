# 游戏工程侦探：零号装配厂 — EP01 / EP02

EP01 用桌面微缩机器人装配厂演示一个问题：两万件物料运输是否必须逐件更新两万次。实现只复现“运输线/间隙”这一工程思想，不包含、复制或改编 Factorio 的资源、UI、代码或具体实现。

## 启动

- Unity：`6000.6.3f1`，URP `17.6.0`；沿用当前项目版本，不执行升级。
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
- `IBeltAccess` 保留 EP01 查询／移除／插入语义。EP02 通过兼容扩展 `ITransactionalBeltAccess` 使用类型过滤、多 Lane 查询、Reset 标记验证与原子交接。

## 性能指标

- **Simulation ms**：只测量 `IBeltSimulationBackend.Tick`。
- **Presentation ms**：Snapshot、Gap 位置展开、逻辑距离到世界坐标采样、实例矩阵和 UI 数据准备。
- **Values Touched This Tick**：算法工作量指标，不等同于时间。
- **Logical Items / Visible Cargo**：压力场景运行 20,000 个独立逻辑物料，并按车道及逻辑距离均匀抽样 128 个代表箱；HUD 同时显示抽样比例和当前物理线路容量。
- EP01 实际为 45 段、8 个弯道、单车道；小负载与闸门阶段使用 400 逻辑单位中心间距，线路容量为 115 箱，Stage 6 为 112 件。压力阶段使用 2 逻辑单位测试间距，使 20,000 个逻辑物料可在同一拓扑中运行，但不宣称它们都具有纸箱物理尺寸。
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
unity test . --editor-version 6000.6.3f1 --mode EditMode --output Temp/AssemblyZero-EditMode.xml --timeout 600
unity test . --editor-version 6000.6.3f1 --mode PlayMode --output Temp/AssemblyZero-PlayMode.xml --timeout 600
```

运行 CLI 测试前需关闭同一工程的交互式 Unity Editor，否则 Unity 会因工程锁拒绝第二个实例。

也可使用 `Tools > Assembly Zero > Build Windows Development Demo` 生成只包含 EP01 场景的 Windows Development Build。

历史隔离验证记录（旧版双车道布局，Unity Editor、20,000 items、300 Tick，状态哈希在 Presentation/Snapshot 阶段计算）：Naive 约 `0.3102 ms/tick`、`20,000 touched`；Gap 约 `0.0001 ms/tick`、`2 touched`。当前 EP01 为单车道，畅通运输的偏移更新为 1 touched；不要将旧版数据当作当前场景实测。压力阶段封闭出口，进入压缩阶段后 touched 会增加。这不是跨硬件基准，正式演示应以 HUD 和目标机器 Development Build 记录为准。

## 已知限制

- EP01 场景为单车道；后端支持多车道，EP02 使用双车道。仅支持普通直线、90° 弯道、Source、Gate、Sink 和一条主要 Transport Line；不支持分流、地下带、动态铺设或复杂交叉。
- 压力阶段的 20,000 是逻辑物料数，画面只提交 128 个代表箱；因此该演示比较的是后端模拟工作量，不代表“同时渲染 20,000 个纸箱”的 GPU 压力测试。不得把 Presentation/Rendering 时间归入 Simulation。
- 当前不使用 DOTS/ECS、Jobs、Burst、Compute Shader 或物理系统。
- Corner90 仍使用方形模块表现；运动采样已使用连续圆弧、切线朝向与法向 Lane 偏移，圆弧美术网格留待下一轮。
- 项目未导入 TMP Essentials；为避免在 `Assets/TextMesh Pro` 生成额外资源，HUD 使用 `_AssemblyZero/UI` 内的本地字体与 uGUI Text。
- EP02 是固定 U 形单线、六类部件、六臂、单装配单元和末端回收；闭环、分合流、自由建造与加工配方不在本期范围。

## EP02：机械臂与机器人最终装配单元

入口：`Assets/_AssemblyZero/Scenes/EP02_Demo.unity`。重建菜单为 `Tools > Assembly Zero > Rebuild EP02 Demo Scene`，需先保存当前场景；不会自动重建或覆盖原始六个身体 prefab。新增资产集中在 `_AssemblyZero/EP02`，代码沿用原程序集，Domain 仍不引用 UnityEngine。

`1`–`6` 切换阶段，`R` 重播并重置全部状态，`Space` 暂停，`N` 单步，`L` 切换 HeadSocket 锁定，`C` 轮换四镜头；HUD 数字按钮也可切换阶段。EP01 的数字键及 `G` 保持原样。

| 阶段 | 可验证的现象 |
|---|---|
| 1 | 单臂抓放；旁侧球形动画没有物流权限，Picking 完成 Tick 才切换所有权 |
| 2 | 两臂查询同一物料与 Socket，意图相同但稳定 ID 0 获得唯一预订 |
| 3 | 六类固定种子混合来料，按类型安装，连续装配 |
| 4 | 第 16 Tick 锁定 HeadSocket，Head 已持料而进入 WaitingForTarget；L 解锁恢复，漏抓件到末端回收 |
| 5 | 来料间隔 6 Tick、机械臂耗时 ×2；有限回收区 60 Tick/件，末端物料仍在皮带上形成真实背压 |
| 6 | 恢复 24 Tick 来料，六槽完成后等待安全返回、扫描 90 Tick、展示 180 Tick，再清空 |

所有阶段都会预置教学物料，使开始播放即可观察抓取。Stage 1/2 是单槽教学 Manifest，不增加完整机器人计数。种子为 2001–2006，重播可复现；阶段 4 的锁定命令本身也进入状态哈希。Stage 2 的「最近意图 / Tick」保留冲突证据，避免只显示一 Tick 而无法读清。Stage 5 从无拥堵到明显背压需要时间，可暂停后逐 Tick 检查或加速手动 Tick。Stage 6 的「平衡」指恢复指定的教学参数，不保证无限时长稳态吞吐；扫描／展示期间仍有来料，长时间运行也可能积压。

### 配置与资源

- U 形线路：27 个 1.2 m 模块、两个 90° 转弯、双车道；速度 32 单位/Tick，托盘占位 768 单位，Port 宽 2048 单位。
- `EP02Scenario.asset` 配置速度、来料／回收／扫描／展示时长与 Naive/Gap 后端。
- `InserterAuthoring` 配置稳定 ID、部件、Socket、Port、LaneMask、四段动作时间和初相位；六臂基础动作 12/24/12/24 Tick，按稳定 ID 加少量固定差异。
- 六个 `RobotPartDefinition` 为 Head=2、Body=3、ArmA=4、ArmB=5、LegA=6、LegB=7；EP01 ID 0/1 不变。定义包含原 prefab、三个 Pose、Socket、Carrier、占位、颜色及可选图标。
- 包装为 `CarrierRoot → PayloadRoot → PartVisualAnchor → SourcePrefab`。共同 FBX 网格注册验证 67 个网格无遗漏／重复；装配姿态共用一种尺度，原 prefab 和子网格导入变换不变。ArmA/B、LegA/B 不推断左右语义。
- Socket 位姿、部件 Pose、动作、Port 和镜头均可在 Inspector 调整。重建时保留已有 PartDefinition、Scenario 和机械臂 prefab 配置；如果想重新烘焙包装，先在 Inspector 清空对应 Definition 的 WrapperPrefab 引用，再执行重建。
- 启动前检查缺失引用、重复 ID、六类映射、Port 范围、动作时长和可达性；失败时 HUD 与 Console 给出明确错误。

### 固定 Tick 与所有权

```text
ApplyCommands → ItemSource → BeltTransport → BeltAccessQuery
→ ItemReservation → Inserter → AssemblyCell → RejectSink
→ Statistics → StateHash → PublishSnapshot
```

后端新增 `TransportTick` 只运输；EP01 的旧 `Tick` 顺序及结果不变。EP02 不同时启用内置生成与出库，统一账本分配单调递增 ItemId。命令按 Tick/Sequence，机械臂与 Socket 按稳定 ID 执行；候选按距 PreferredDistance 的差值、ItemId 排序。

```text
OnBelt → ReservedOnBelt → HeldByInserter → InAssemblySocket → CompletedProduct
   └──────────────────────────────→ InRejectSink
```

预订同时锁定物料和 Socket，物料仍随皮带运动；离开范围会释放。Picking 结束原子移除，Placing 结束原子安装，单 Tick 不跨多个动作状态。占用／锁定／类型不符的目标不能从皮带偷取物料；持料时锁定则保留载荷等待。禁用空臂释放预订，禁用持料臂明确记为 `InserterDisabledWhileHolding` 回收；末端漏抓记为 `MissedPickupAtLineEnd`。Reset token 使旧句柄失效，但不进入确定性哈希。

八态：WaitingForItem、Reserving、Picking、Carrying、Placing、Returning、WaitingForTarget、Disabled。机械臂没有 Rigidbody 或 Animator；三关节几何只读取快照进度，不以动画结束／碰撞回调驱动物流。载荷根据所有权切换到皮带、夹爪或 Socket；空托盘进入纯视觉回收动画，不参与物料账本。

每 Tick 检查：`Generated = OnBelt + Reserved + Held + Installed + Rejected + CompletedConsumed`，预订不重复计为 OnBelt。扫描结束只触发一次完整机器人完成事件，消耗六件；平均时长从该轮第一件安装到完成事件。Busy/Starved/Blocked 只统计启用 Tick。

### 表现、性能与验证

HUD 显示守恒、完整数量、平均装配时间、输入阻塞、哈希和每臂状态／意图／预订／持料／交接数／利用率。五色 Gizmo：绿抓取区、蓝目标、黄预订、红锁定、白可达范围。音频用 Tick 事件去重，可选 Inspector 音频未配置时静默。

EP02 分开测量模拟、查询／预订、状态哈希、表现（含 Snapshot）；Profiler 提供 `EP02.FixedTick` 与 `EP02.Presentation` 样本。Belt touched 只表示运输后端的工作量，不包含查询、账本、哈希或渲染。查询当前使用扫描，哈希与历史账本成本会随生成物料总量增加；这里不宣称整座装配系统 O(1)。EP02 载荷使用多网格 prefab 呈现，不是 EP01 的代表箱批量渲染压力测试。

新增测试覆盖竞争互斥、禁用释放／回收、持料锁定、所有权守恒、六槽唯一完成、Reset 句柄、多 Lane 类型过滤、头／中／尾移除、双后端 10,000 Tick 哈希、帧率独立性、六阶段行为、资源覆盖和场景载荷挂接。PlayMode 验证重复呈现不重复计数、重播不残留载荷。EP01 原 15 个 EditMode 和 1 个 PlayMode 基线继续运行。

`Tools > Assembly Zero > Build EP02 Windows Development Player` 生成 `Build/EP02/AssemblyZero.exe`，以 EP02 为启动场景，同时包含原有启用场景；项目 Build Settings 只追加 EP02，不调整原条目。构建与测试结果以本次执行报告为准。

### 本轮验收记录（2026-09-30）

- 当前五个程序集编译成功；EditMode **38/38**、PlayMode **2/2** 通过，包含原 EP01 **15+1** 基线；六阶段行为和双后端 10,000 Tick 对照均通过。
- 原始六个身体 prefab 的 SHA-256 与实施前完全一致。Build Settings 只新增 EP02 条目；编辑器恢复到原 SampleScene。
- Windows x64 Development Build：**Succeeded**，224,881,346 bytes。报告的两条 Error 均为构建期间 CLI `/api/exec` 等待主线程超过 5000 ms，不是编译失败；两条 Warning 来自 EP01 的 Unity 6.6 已弃用 FindObjectsByType 排序重载，未改动该旧驱动器。
- 独立 Player 以 `-batchmode -nographics` 启动检查，未发现 EP02 启动错误或异常；检查后已退出测试进程。画面验收来自 Editor PlayMode，不把无图形启动称为独立 Player GPU 性能测试。
- `Temp/EP02Profiler.raw` 已读取到 279 个 `EP02.FixedTick` 样本（累计约 16.31 ms）和 22 个 `EP02.Presentation` 样本（累计约 46.98 ms）。包含冷启动和手动推进，仅证明分区采样有效，不作为稳态帧率或跨硬件基准。
- 演示截图：`Temp/EP02_Stage6.png`；Player 启动日志：`Temp/EP02Player.log`。Temp 与 Build 为忽略目录，不进入源码版本控制。
