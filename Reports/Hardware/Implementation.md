# 显卡经营切片实施记录

## 范围
M0 运行基线 → M1 显卡战役 → M2 订单采购与成本 → M3 俯视驾驶 → M4 uGUI HUD 与表现 → M5 独立包验收。
沿用 Unity 6000.4.0f1、URP、固定 Tick、Gap 物流和物品 ID。新战役独立 v2 存档；旧 EP04 v1 继续使用旧规则。
不修改 EP01–EP03 场景、左侧输送布局，不购买资产或发布远端。

## 基线
- 分支：codex/gpu-business-slice。
- 初始未跟踪文件和受保护资产哈希见 BaselineStatus.txt、BaselineHashes.json。
- 历史 EditMode 98/98；EP04 专项 12/12、PlayMode 1/1。不是本轮结果。
- 历史全量 PlayMode 的 EP01、EP02 因 Build Profile 未包含场景而失败。
- 历史独立包隐藏窗口截图为黑屏，不构成画面或帧率证据。
- 当前 Editor.log 已轮换，不能从历史摘要的 1 error / 6 warnings 推断根因；本轮构建保存详细 BuildReport。

## 验收状态
实施中。编译、测试、画面、可玩性、性能分别验收；外部玩家意愿测试不能由自动走查替代。
