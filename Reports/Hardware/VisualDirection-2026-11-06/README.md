# 显卡品牌创业：视觉方向补充

日期：2026-11-06。状态：设计建议与概念图，未实施 Unity 美术替换。

## 已确认方向

用户选择：显卡品牌创业、轻松成长与爽感、大胆的整体设计方向；视觉采用精致微缩工业，清楚、耐看，突出显卡和机械细节。

本建议补充此前的新品诞生、客户回应、产品系列与历史墙方案。视觉应让玩家愿意欣赏自己的产品，也能看懂产品是怎样造出来的。

Concept.png 是 AI 生成的美术目标参考，不是当前游戏截图、可直接使用的三维模型、已验证画质或厂房布局设计。

## 视觉优先级

1. 主角显卡：先制作一款能承受近景展示的双风扇显卡。重点包括弯曲扇叶、轮毂、鳍片、热管、PCB 边缘、金手指、接口挡板和品牌铭牌。外壳、散热和背板采用可替换模块，延伸出后续产品家族。展示模型保留独立可动部件；运输模型采用简化和合批版本。
2. 两个招牌工位：精修总装机与检测机。总装动作依次为定位、夹紧、散热/外壳落位、工具点动、放行；检测动作为插卡通电、风扇启动、扫描、屏幕响应和结果确认。动作读取真实进度与结果。
3. 材质和灯光：保留石墨、浅灰、灰蓝、青色基调，以暖白外壳、拉丝铝、铜热管、PCB、橡胶和玻璃区分表面。用柔和主光、补光和接触阴影强调结构；青色强调运行，琥珀色强调注意事项。总览保持清楚，近景才加强景深。
4. 特效：窄扫描线、通电灯光顺序、状态屏波形、合格确认、封箱贴标和新品揭幕。电子装配以精密洁净为准，火花只用于实际适合的工艺。普通生产反馈克制，型号首件与重要里程碑具有更完整的演出。
5. 成长外观：升级后增加可辨认的机械结构、检测模块和品牌铭牌；首款样机、畅销型号与后继产品进入陈列墙，呈现品牌历史。

## 第一轮制作范围

完成一张高质量显卡、总装与检测两台设备、一套局部材质灯光和四项核心效果（通电、风扇启动、检测扫描、通过确认），形成可循环观看的约 15 秒展示片段。展示片段可截取生产中的关键时刻，不作为固定生产时长。

同一组资源覆盖产品设计器、首件亮相、检测工位和历史墙。先验收这一小段，再扩展到全厂。

## 源码核查

- HardwareCardVisuals.cs 已按型号生成风扇数、卡长、配色、背板与灯带；当前展示纹理为 320×200。应在提升模型后同步改善预览清晰度和专属布光。
- HardwareFactoryVisuals.cs 与 HardwareFactoryView.cs 已有倒角盒体、机械细节、进度动作、材质合批和产出反馈。独立风扇与拆装展示需要保留可动结构，不能直接依赖合并后的货物模型。
- HardwareLens.cs 已包含 Bloom、景深、暗角与径向模糊；HardwareShowcase.cs 已有夜景。后续以调校已有能力为主。
- 历史视觉截图来自 Reports/Hardware/VisualIteration（2026-10-23），仅用作历史基础。2026-11-04 报告未完成本轮新增交互与画面验收。本轮没有运行 Unity，不给当前实际画面打分。

## 实施边界与验收

保留 EP03 左侧零件输送区。替换美术时保持设备占地、端口和输送接口；需要空间的新展示设施使用现有空白区域。不得为落实这份视觉建议调用全局布局重建。

- 常用游戏视角能区分设备、物料阶段、运行与故障状态。
- 产品近景能看清扇叶、鳍片、外壳和 PCB 的结构及材质差异。
- 生产动作在暂停、1×、2×、4×、8×及读档后与模拟结果一致；高倍速保留关键动作，避免频闪和机械抖动。
- 展示动画不推进经济；成功特效只由真实结果触发。
- 多条产线同时运行时实测帧时；依据近远景使用不同模型细节和特效密度，重复特效复用实例。
- 新模型应分别检查总览与特写；确认无穿插、悬浮、粉色材质、控制台新增错误或重复首件演出。

本轮仅新增本报告和 Concept.png；未改代码、场景、模型、包或项目设置，未运行编译或游戏测试。

## 使用的参考

本轮采用 unity:craft-game-floors 的视觉评估路线，阅读 art-direction.md、scene-lighting.md、vfx.md；没有实施外观修复、运行时评分或 Unity Play 检查。使用 imagegen/SKILL.md 的内置工具模式生成一张风格草案。艺术风格和具体建议属于本项目设计判断。

Unity 6.4 文档：

- [Understand volumes in URP](https://docs.unity3d.com/6000.4/Documentation/Manual/urp/Volumes.html)
- [Bloom Volume Override reference](https://docs.unity3d.com/6000.4/Documentation/Manual/urp/post-processing-bloom.html)
- [ObjectPool API](https://docs.unity3d.com/6000.4/Documentation/ScriptReference/Pool.ObjectPool_1.html)：重复使用实例可减少频繁创建与销毁开销。

原技能中的 docs.unity.com Volume/Bloom 地址在工具中无法访问，改查相同编辑器版本的 docs.unity3d.com 页面。

## 概念图生成记录

方式：内置 image_gen 工具。原图保留在 Codex generated_images 目录，副本保存为同目录 Concept.png。图像已目视检查：双风扇显卡、总装及检测局部场景、石墨/浅灰/青色基调清楚。机械结构仍需三维制作阶段验证，图中屏幕数字属于概念表现。

最终提示词：

```text
Use case: stylized-concept. Create a polished art-direction concept board for an existing Chinese GPU factory management game called Assembly Zero. This is preview-only art, not a gameplay screenshot or a factory layout plan. Landscape composition, two complementary panels with a narrow clean gutter, the larger left panel about 60% width showing a close isometric three-station GPU assembly and inspection vignette, the right panel showing a large gorgeous finished graphics card hero product. A small restrained heading in Chinese: “精致微缩工业 · 视觉概念”. Style: premium miniature industrial diorama, attractive clean stylized 3D modeling with credible machined geometry, slightly emphasized silhouettes, suitable for a readable isometric PC management game, bright welcoming atmosphere. Existing palette: graphite and blue-gray bases, warm off-white painted machine bodies, brushed aluminum, emerald PCB, restrained cyan operating indicators and tiny amber safety accents. Left vignette: short neatly organized conveyor carrying actual bare PCBs and assembled GPU cards, one precision assembly cell with a small credible articulated robot positioning a cooler on a PCB fixture, one testing cell with a transparent safety cover, a fine cyan scan stripe over a graphics card, real monitor with a tiny abstract game benchmark image, one finished product tray. Show machinery layered panels, vents, joints, cables routed neatly, obvious material distinction between matte paint, metal, dark rubber and smoked glass. No surrounding full factory or inferred master layout. Right hero product: off-white and graphite dual-fan graphics card, sculpted shroud, realistically curved individual fan blades, visible densely spaced aluminum cooling fins and copper heatpipes, dark green PCB edge and gold PCIe fingers, machined rear IO bracket, small understated cyan status accent, tabletop hero shot that makes player want to collect this object. Small matching packaging behind it is acceptable. Lighting: soft warm key light, cool soft fill, gentle contact shadows and deliberate edge highlights, readable shapes, restrained depth of field only behind hero subject, all important assembly mechanics in focus. Refinement comes from geometry, materials and light. Avoid broad neon glow, hologram clutter, explosions, welding sparks, smoke, cyberpunk darkness, dirty worn dystopia, floating parts with impossible mechanics, flat basic cube-only assets, dense UI, real hardware brand logos. Image is aspirational but plausible as carefully produced game art, not a photograph.
```

