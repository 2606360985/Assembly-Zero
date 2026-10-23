# 显卡工厂视觉、运输与全产线拐角迭代

2026-10-23。入口仍为 `Assets/_AssemblyZero/Scenes/Hardware_Business.unity`，打开直接 Play，无须生成或重建场景。Windows 新版位于 `Builds/Hardware/AssemblyZeroHardware.exe`。

## 已完成

- 精致科技微缩造型：16 种物料使用显式视觉描述；裸板、贴装板卡、待检卡、合格卡、品牌包装具有不同结构。标准版采用石墨双风扇，静音版采用浅色三风扇和加厚散热。工位展示区分正在加工的输入与实际完成的输出。
- 八类设备保留原位置、端口、占地与业务接口，加入倒角外壳、控制屏、机械细节和跟随实际生产进度的机构。共享材质与产品合批网格，重复货物复用视图；探索、通知等非几何变化不会重建工厂。
- 半开放厂房包括后墙、侧墙、前侧低墙、窗带、立柱、厂名和装卸入口。地坪、厂房、设备、皮卡及 UI 采用统一的石墨、灰蓝、浅灰和青色配色。
- 矿区移动至世界网格 `(12,72)`，采购区移动至 `(36,64)`，增加独立作业场地、矿石装载架、围挡、采购仓、卷帘门和装货棚。矿堆随实际储量减少，皮卡载货时显示固定货载。
- Hardware 寻路避开两座厂房和墙体的安全边界；可分别定位矿区、采购站、车辆和当前工厂。维持原逻辑速度，允许外围搬迁增加真实运输时间。
- 皮卡按完整路径段的 Tick 进度连续显示位置，平滑转向；暂停冻结显示位置，读档和新战役重置表现缓存；新战役、读档和切厂同时清理放置状态及幽灵模型，重建第一帧即按真实库存设置产品和状态灯。新增独立布局版本，旧 Hardware v2 存档执行一次性迁移，保留资金、订单、加工进度及货物，更新矿点并重算受影响路线。
- 保留 uGUI/TMP 与原有操作，更新导航、目标卡、工具栏、设备图标、型号卡、订单进度卡和按钮状态。建设栏支持窄屏横向滚动，抽屉保持纵向滚动，通知与抽屉互斥显示。

## 全产线拐角与 EP03 复用

将 EP03 已验证的连续截面扫掠提取为运行时 `FactoryConveyorGeometry`，原 Editor 构建器委托共享实现。没有执行 EP03 场景生成、通道烘焙或布局重建。

- 当前显卡工厂的所有现有及新建运输线统一使用 EP03 的 16 段圆弧中心线。
- 带床和双侧护栏共用中心线，保留短端口、坡道折返的独立等宽封口处理；垂直交接保留导向框，并为上下层线路避让支脚。
- 货物按同一中心线的弧长采样位置、连续采样朝向；显示尺寸适配原有 512 单位间距，保持逻辑长度、身份、吞吐和生产节奏。
- 与 EP03 Full / Minimal 的全部 121 条已接受带床逐一比较，顶点数及三角面一致，顶点位置误差小于 0.00001m；既有场景和烘焙网格未改动。旧 EP04 的车辆寻路与规则保持兼容。

## 验证结果

| 验证 | 结果 |
|---|---|
| Unity 原生 EditMode | 72 / 72 通过 |
| Unity 原生 PlayMode | 12 / 12 通过 |
| Windows Development Build | 成功，0 错误，6 项既有 Unity API 弃用警告 |
| 1366×768 / 1920×1080 / 2560×1080 | 三组新版 Player 经营与存档走查成功 |
| 新版截图 | 39 张，尺寸及非黑屏检查通过；已查看总览、产品、墙体、矿区、采购区、转角及大 UI |
| 控制台 | 三组 Player 日志未发现空引用、材质 Shader 错误或中文缺字警告 |
| 文件保护 | 受保护项目设置、EP03 全厂场景与布局哈希与开工前一致 |

测试覆盖两厂到两处外围目的地的可达性和厂界避让、四种旧车辆状态迁移、重复载入幂等、物料守恒、循环采集、仓满等待、多趟自提、救援、订单和旧 EP04 回归。几何检查覆盖普通圆弧、紧凑端口、折返坡道、垂直交接、截面宽度及实际扩建后的全部路线。

30 / 60 / 120 FPS、1× / 2×以采样参数测试验证皮卡位移连续性；共享圆弧测试检查转角位置及朝向连续。运行测试使用合成键鼠设备验证建设点击、UI 拦截、暂停与运输操作；没有宣称完成真人键鼠长时间试玩。

三组 Player 均完成：常规配送和自动自提两单、自产散热器首单、零现金采矿恢复以及 Unity 原生 JsonUtility 存档继续运行比较。首张合格成品入库约 248.38 模拟秒，配送及自提流程均在走查设定的 1650 秒完成两单，没有注资或注入物品。

## 实测性能

Unity 6000.4.0f1 / Direct3D11 / Windows Development Player，1920×1080、PC 画质、关闭 VSync、不限帧；每组独立预热 30 秒后采样 120 秒。硬件为 Intel Xeon Silver 4210 2.20GHz / NVIDIA Quadro RTX 5000。采样期间未运行其他本轮 Player 验证或视频编码。

| 场景 | 节点 / 路线 | 帧数 | 平均帧时 | P95 |
|---|---:|---:|---:|---:|
| 初始工厂 | 6 / 10 | 62,119 | 1.932ms | 2.363ms |
| 扩建工厂 | 8 / 14 | 55,088 | 2.178ms | 2.796ms |

两组均达到平均 ≤16.7ms、P95 ≤25ms 的预算。其他两种分辨率完成画面与业务验证，没有单独测量性能，不能将上述数值外推为它们的帧时。

## 图片、录像与原始记录

- [总览](1920x1080/Default.png)、[工序和型号对照](1920x1080/ProductStages.png)、[圆弧带床与护栏近景](1920x1080/ConveyorCorner.png)。
- [矿区](1920x1080/Mine.png)、[采购区](1920x1080/Supplier.png)、[订单 UI](1920x1080/Orders.png)、[产品 UI](1920x1080/Products.png)。
- [运输录像](1920x1080/Transport.mp4)：180 张实际 Unity 渲染帧，保留约 23.68 秒录制时间。15 个暂停采样中位置与 Tick 各只有一个唯一值。
- 录像使用逐帧 PNG 采集，编码按实际时间间隔保留可变帧率，没有补帧；录制开销会降低视频采样率。流畅度预算以未录制时的上述 Player 帧时为准。
- `EditMode.xml`、`PlayMode.xml`、`Build.txt`、`BuildLog.txt`、`Player-*.log`、各分辨率 `Walkthrough.txt` / `Verification.txt`、`VerificationSummary.json` 与 `PlayerAssemblies.json` 保存验证依据。
- 原始录像帧移至被 Git 忽略的 `Library/VisualIterationCaptureFrames`，编码脚本为本目录 `EncodeTransport.py`。编码工具仅用于报告，不是游戏运行依赖。

## 复验命令

在工程目录、未打开同工程 Editor 时运行；保留 Unity 6000.4.0f1。

```powershell
unity test . --mode EditMode --filter 'AssemblyZero.Tests.HardwareBusinessTests;AssemblyZero.Tests.EP04BusinessTests;AssemblyZero.Tests.HardwareConveyorTests;AssemblyZero.Tests.EP03RouteGeometryTests' --output Reports/Hardware/VisualIteration/EditMode.xml
unity test . --mode PlayMode --filter AssemblyZero.Tests.HardwarePlayTests --output Reports/Hardware/VisualIteration/PlayMode.xml
unity build . --target StandaloneWindows64 --execute-method AssemblyZero.Unity.Editor.HardwareSceneBuilder.Build
```

Player 的 `--hardware-verify` 不读取或写入用户存档；`--hardware-short` 跳过性能采样，`--hardware-video` 额外采集运输录像帧。截图流程会拒绝黑屏采集，隐藏窗口的黑屏不能作为画面验收。
