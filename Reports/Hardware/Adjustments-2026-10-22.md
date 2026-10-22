# EP04 调整记录 · 2026-10-22

本轮修改入口为 `Assets/_AssemblyZero/Scenes/Hardware_Business.unity`（星铸显卡经营切片）。无须重新生成场景。旧 EP04 的单次皮卡采集规则保持兼容。

## 行为

- 手动驾驶暂时冻结：WASD/E 和接管命令不再启用驾驶；Tab 打开运输页。旧驾驶实现、命令编号和存档字段保留，旧驾驶中存档载入后自动返厂卸货。
- 运输页规划循环采集，或点击已发现矿点。皮卡自动寻路、采满返厂、卸货并继续。矿空等待再生，仓满保留货物等待；停止任务后返厂卸货并待命。全局暂停/倍速统一生效。
- 自提采购改用自动装运。采购界面在空闲皮卡可用时派遣；忙碌时可在运输页另行派遣。超过车容量的货物分次取回，取完后待命。救援结束后自动卸货。
- 八类节点改为纯 Box 色块搭建，不再实例化旧机器或机械臂资源。各自采用货架、炉体烟囱、冲压机、贴装台、双臂装配台、检测门、封箱机及交付棚轮廓。产品风扇也使用方块表现。
- 左上角目标卡根据实际状态提示接单、补齐散热器/铝材、规划上游采集、当前生产工序、首款产品下线、预留与交付。“下一步”打开对应页面、预选采购缺口或定位工序，不代替玩家确认采购与交付。
- 首款产品包装完成时记录持久里程碑并显示通知；取消/超期重接订单、缺失设备和读取存档时按当前状态重新推导提示。完成首单后引导静音版订单。

## 验证

- Unity 6000.4.0f1 随附 C# 编译器，使用项目 Bee response files 独立编译 Domain、Runtime、EditMode tests、PlayMode tests、Editor 五份程序集：通过。输出仅写入 `Temp/EP04Adjustments`，未替换 Unity 缓存或旧 Player 的程序集。警告为原有 Unity API 弃用提示。见 `Adjustments-Compile.txt`。
- 使用 Mono + 原 NUnit 运行 `HardwareBusinessTests` 与 `EP04BusinessTests`：35/35 通过。覆盖多趟自提、带货改派拒绝、自动采集循环、仓满等待、停止返厂、救援、旧驾驶存档恢复、首款产品里程碑、缺料提示、首单与静音订单以及旧 EP04 回归。
- 独立运行修改后的四条正式配置业务走查：配送/自动自提均在约 248.38 模拟秒首张显卡入库，并在脚本设定的 1650 秒完成两单；自制散热器首单、零现金自动采矿恢复完成。未注资、注入物品或额外解锁。结果见 `Adjustments-DomainChecks.txt`。
- 独立测试使用 DataContract JSON 替代原生 `JsonUtility` 做字段往返，不能据此宣称 Unity 原生 JSON 或 PlayMode 已通过。复验脚本为 `VerifyAdjustments.ps1`，替代序列化器和运行器为 `AdjustmentsHarness.cs`。
- Unity 正式 EditMode 测试首次启动在编译子进程遇到 Win32 1455；重试停在 Editor 初始化、未生成测试结果。已结束本轮启动的停滞进程，未关闭其他项目编辑器。未完成本轮图形运行、实际键鼠、截图验收或 Windows 新包构建；旧构建和旧截图不代表本轮版本。
- `git diff --check` 通过。EP03、现有场景布局、ProjectSettings、Packages 无修改，未调用全局布局生成或输送线路重建工具。
- 按用户授权尝试 `codegraph init -i`，本机未安装/未暴露 `codegraph` 命令，索引未能建立；当前会话也没有 CodeGraph MCP 工具。

原生环境恢复后，可执行：

```powershell
unity test . --mode EditMode --filter 'AssemblyZero.Tests.HardwareBusinessTests;AssemblyZero.Tests.EP04BusinessTests' --output Reports/Hardware/Adjustments-EditMode.xml
unity test . --mode PlayMode --filter AssemblyZero.Tests.HardwarePlayTests --output Reports/Hardware/Adjustments-PlayMode.xml
```

打开既有 Hardware_Business 场景 Play 即可检查新外观与目标卡；不需要执行场景生成菜单。Windows 新包仍使用既有 `Build Hardware Windows Player` 菜单。
