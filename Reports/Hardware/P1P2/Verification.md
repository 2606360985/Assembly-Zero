# 验证记录

2026-11-04，Unity 6000.4.0f1。

| 项目 | 结果 / 证据 |
| --- | --- |
| 领域、运行时、EditMode、PlayMode、Editor 五个程序集 | Unity 自带 Roslyn 与 Bee 引用编译全部通过，见 `Compile.txt` |
| 领域回归 | `Domain-final.txt`：85 通过、0 失败；包含 EP04、原显卡经营/采购、SKU/研发以及新增迭代测试 |
| 完整开局 | 无注资的领域脚本约第 25 模拟分钟研发到第二代，现金 2370.50 元；包含教学第二单的次品后补料 |
| 第二代新品 | 设计 A 核心/游戏市场型号 → 设备停机改造 → 采购匹配核心 → 生产分档 → 整单交付通过 |
| 存档与边界 | 研发/改造中续跑一致、旧存档迁移、跨代贬值不反弹、错误世代/体质不抵扣采购缺口、C 档分流、1000 代数值不溢出均通过 |
| Unity 原生全量 EditMode | 未取得有效完成结果。运行过程遇到未保存场景提示，取消后重试仍超时；`AllEditMode.json` 仅为失败尝试，不能作为通过证据 |
| PlayMode / 画面 | 本轮新增交互测试已编译；未完成执行和画面验收。电脑界面自动操作被用户停止后未继续操作 |
| 场景和设置 | `git diff --name-only -- Assets/_AssemblyZero/Scenes ProjectSettings` 无输出；未写入 EP03/Hardware 场景文件及项目设置 |

`Domain-final.txt` 使用 `RunDomainChecks.ps1` 的备用 Mono 测试入口；其 JSON 为 DataContract 替代实现，不能证明 Unity 原生 JsonUtility、真实点击或渲染效果。原生早期小组测试结果也不能覆盖本次最终版本，故没有用它替代全量结论。

编辑器诊断期间观察到 Hardware_Business 存在未保存的 `plymodels` 实例。取消测试后再次检查，当前场景已恢复为磁盘版本的三个根对象，层级中未再出现该实例。对应新增的 `Assets/plymodels.fbx` 和 `Assets/Untitled.ply` 及其 meta 文件均未修改或删除；没有将任何场景变更保存到正式文件。`EditorSceneBackup.backup` 是已有的 08:23 自动备份，不包含后加入的 plymodels，不能宣称可用它恢复该实例。

本记录证明代码层实现与领域闭环；原生交互、画面、真人试玩与长期平衡仍需要后续验收。
