# 创始人现场 / 图片制作记录

使用内置 `image_gen`，没有使用 API/CLI fallback。参考图由用户提供；文字和合同数据由运行时 UI 显示。最终资产及生成文件对应见 Art-Provenance.json，尺寸、透明度和哈希见 Art-Inventory.json。

## 角色图集的共同要求

Production-ready transparent character expression atlas for a realistic industrial science fiction visual novel. Precisely a 3-column by 2-row equal-cell grid, six separate waist-up cutouts of the SAME adult person, consistent identity, costume and body scale. Arms and torso fully within each cell, transparent gutters. Top row: calm, thinking, questioning. Bottom row: tense, tired, relieved. Photorealistic cinematic concept illustration, natural mature faces, soft stable lighting, graphite and warm white with subtle copper accents. True alpha background. No text, labels, borders, scenery, logos or watermark. Landscape atlas.

| 文件 | 人物与追加提示 |
|---|---|
| lin.png | Adult Chinese male founder Lin Xu, 32; short untidy black hair, faint stubble, focused slightly tired eyes; graphite technical work jacket with subtle copper seams, grey undershirt, old wristwatch, blank employee badge. Identity and costume follow the supplied founder concept sheet. |
| shen.png | Adult Chinese female founder Shen Zhiwei, 30; short asymmetrical dark brown bob tucked behind one ear, composed eyes, natural mature features; structured graphite work jacket, ivory shirt, old watch and blank employee badge. Identity and costume follow the supplied founder concept sheet. |
| client_studio.png | Distinct Chinese woman, 35, independent game studio director; tied-back dark hair, warm thoughtful face, oatmeal knit shirt under charcoal casual blazer, tablet held at waist. |
| client_gaming.png | Distinct Chinese man, 43, gaming cafe owner; close-cropped hair, practical stocky build, dark casual overshirt and plain warm grey t-shirt, approachable experienced expression. |
| client_research.png | Distinct Chinese woman, 41, AI and power-systems researcher; shoulder-length dark hair, thin metal glasses, charcoal laboratory technical jacket with ivory undershirt, calm precise expression. |

角色均以用户概念图为参考，NPC 只参考美术风格。图集由 Unity 按相等网格取 Sprite，未用 Python 修改生图结果。

## 环境图共同提示

Final in-game visual novel environment background, wide 16:9 landscape. Realistic industrial science fiction cinematic concept illustration, photographic material fidelity, emotionally clear single visual focus, graphite grey, warm white, desaturated cyan with sparing copper orange. Match the supplied reference's credible manufacturing world and restrained lighting. Concentrate important environmental storytelling on left and center; right 40–45 percent quieter for waist-up character overlays. Bottom quarter dark and uncluttered for runtime dialogue. Beautiful physically plausible materials, cinematic depth, legible silhouettes. No UI, no readable text, no logos, no watermarks, no neon cyberpunk, no excessive holograms, no oversized foreground people. Environment asset, not a screenshot or mockup.

| 文件 | 场景提示与叙事重点 |
|---|---|
| workshop.png | Intimate early graphics-card testing workshop at dusk. Brushed metal bench, unbranded dual-fan GPU, copper heatsinks, antistatic mat, test leads and warm desk lamp. Small production line, glass partition, distant city and power pylons. First batch; trust and responsibility before growth. |
| acceptance.png | Sample acceptance space beside a factory testing line. Metal desk, dual-fan GPU, clipped test records without legible writing, contract folder. Warm lamp and cool afternoon factory windows. Trust before a first contract. |
| first_product.png | Heroic but believable close-up of the first unbranded dual-fan GPU leaving inspection on an antistatic tray. Subtle cyan pass indicators without writing, packaging, tiny machining imperfections, beautiful copper parts. Pride earned through work. |
| studio.png | Small independent game studio at evening. Three modest workstations rendering a warm imaginative game scene; GPU PCs, delivery carton, thank-you note with no readable text, empty chairs, coffee and creative props. Our product helps someone create. |
| gaming.png | Newly opening local gaming cafe in a near-future Chinese city. Practical PCs, unopened GPU cartons, installation tools, daylight and warm tungsten light. Growing from a sample to a repeatable promise. |
| ai_lab.png | Research computing validation lab. Credible four-card open rack, power electronics, measured cable routing, charcoal acoustic panels and frosted glass; tiny background researchers. Abstract engineering render without text. Reliable operation opens a new industry. |
| compute.png | Newly commissioned computing room. Small practical server cluster, GPU trays, copper power rails, cooling pipes and soft cyan status lights; empty walkway and warm industrial windows. A maker becomes an operator of an enduring system. |
| design.png | Startup design studio with unfinished dual-fan chassis, removable backplate, copper paint samples, PCB drawings with no writing, calipers and engineering tools. Clean pedestal zone for the actual runtime-designed product. First product with our own name. |
| energy.png | Joint energy-material station at night. Small unfamiliar crystal in a mechanically credible shielded chamber, instrumentation and thick power cables; faint amber inner geometry; city power grid outside. A question still unanswered, not magical free energy. |
| energy_control.png | Future Earth energy control facility, power electronics, battery storage behind glass, substations and city skyline at dawn. Maintenance platform overlooking real infrastructure. Responsibility for reliable energy. |
| orbital.png | Credible orbital industrial factory above sunlit Earth, viewed through an operations-platform window. Manufacturing ring, trusses, robotic bays, thermal radiators, solar arrays and restrained copper markings. Same career, much larger responsibility. |
| deep_space.png | Industrial research outpost near an airless moon. Through a window, an immense silent nonhuman geometric structure against space and distant sunlight; human robotic equipment and compact energy module establish an industrial link. Mysterious, hopeful discovery; no hostile aliens, weapons or magical holograms. |

## 使用边界

12 张共享环境 CG，5 张六表情透明图集，共 17 个 PNG。男女主角使用同一组现场背景、各自立绘；这与用户最新的联合创始人方向一致。能源控制、轨道和深空图用于明确标注的后续场景预览。自定义 SKU 在设计现场叠加真实 3D 预览，概念产品不代替配置展示。
