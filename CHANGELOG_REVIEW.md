# TPVPA 更新整理与 Independent 移植评估

整理日期：2026-10-09。范围：Discord changelog 论坛中 2025-04-04 至 2026-10-04 的 **176 篇主帖（含 1 篇置顶 Wiki）**。这是公告合并与源码对照，不是作者服务器的实测规则表。未逐一阅读所有讨论回复；图片、含糊描述、未公开配置不能据此还原。

本地基线：`feature/independent-core`，Independent 1.1.13，提交 `9cd9bc9`。上游公开分支：`PylonTeam/PvPAdventure:erkys-features`，1.3，提交 `7816cc891cbd6e3d401741ce3dfb8e4fa37470fe`（2026-10-05）。服务器配置对照为本轮检查时的快照；后续配置可能改变。

补充核实：GitHub 默认分支已为 `Public-Test-Branch`，版本 1.3，提交 `7b371c85f1ad57ff2ca41c01387e218ff149502c`（2026-10-04）。`erkys-features` 在它之后仅多 `49b0260`（床保存、计分栏悬停修复）和 `7816cc8`（CI 修复），不是另一套新版玩法。旧 `master` 的最新提交仍为 2026-04-02，不能代表当前默认分支。

## 先看结论

### 实施进度（2026-10-09）

- 用户调整范围：暂不取消随机传送，暂不实现灵体赛跑开局；其余前十部分继续逐项移植。
- 本地 1.1.15 管理员规则（尚未部署）：服务端自动授予角色名精确为 `Reisenberg` 的在线玩家管理员权限，口令登录仍可用。角色名是授权依据，不是经过验证的账号身份。

- 第一批已加载服务器配置：开局 4 分、击杀 1 分、指定 Boss 奖励、哥布林上限 200、火星上限 300；原地图及存量分数保留。
- 第二批 1.1.14：42 项物品伤害、3 项弹幕伤害、19 项物品百分比穿甲、3 项弹幕百分比穿甲。配置默认值位于 [WeaponBalanceDefaults.cs](Core/Config/WeaponBalanceDefaults.cs)。没有填入 Rainbow Rod 的含糊伤害值或标为实验的穿甲值。
- 现有 [StatisticsProjectile.cs](Common/Statistics/StatisticsProjectile.cs) 原本已经记录武器，但只保存原物品引用、没有同步。现改为生成时克隆、子弹幕继承、通过 ExtraAI 同步；百分比穿甲表已接入命中路径，配置对应武器时替代旧固定穿甲。
- 隔离世界运行检查通过 127 项，包含发射后原物品变更、子弹幕、网络序列化、倍率组合、穿甲及 PvE 不受影响。尚未进行两客户端实战验收。
- 以下对照表保留整理时的 1.1.13 基线；以上进度优先。免疫组、装备重做、自动推进等仍未完成，不能把第二批称为整个新版战斗移植完成。

新版差异最大的部分是战斗底层，不能只把武器伤害百分比填进旧配置：新版主要依赖武器免疫组、百分比穿甲、弹幕来源和距离规则；Independent 仍保留旧式受击免疫与部分硬编码效果。

建议顺序：

1. 先明确 PvP 免疫组、百分比穿甲、伤害与减益距离规则，以及服务器对命中的裁决，再调整武器数值。
2. 移植甲虫能量、十字项链保护、鞭子与召唤物协作；幽魂的新主动释放机制单独实现。
3. 优先拿公开源码可独立实现的配方、掉落、手套攻速不叠加和显示改进。
4. 自动推进、取消随机传送、赛跑开局、计分与 Boss 强度单独决定。这些会改变比赛节奏。

用户已说明目前没人正式战斗，战斗平衡可以重新调整。本文只提供清单，**没有应用这些修改**。

### 怎么读状态

- **已有**：找到当前对应源码或配置，不等于完成双客户端实测。
- **部分**：已有基础，但数值、触发或清理规则不同。
- **待移植**：当前对照未找到对应新版实现。
- **待确认**：公告缺参数、前后矛盾、实验性质或无法从公开源码核实。
- 文中的“最新”指最后一条明确公告；不保证等于作者当前部署配置。

### 公开代码的边界

[上游 build.txt](https://github.com/PylonTeam/PvPAdventure/blob/erkys-features/build.txt) 依赖 ErkySSC、PvPFramework、Pylon；[依赖清单](https://github.com/PylonTeam/PvPAdventure/blob/erkys-features/.github/dependencies.json) 指向相应仓库。当前公开访问不能取得这三个依赖，CI 也要求专用依赖令牌。因此部分核心行为需自行实现或请作者提供接口/源码。

但不能说所有新版内容都没公开：掉落、配方、十字项链与手套等仍在 PvPAdventure 公开分支里，可逐项参考。

## 1. 战斗底层

| 最新公告/行为 | Independent 对照 | 判断与注意事项 |
|---|---|---|
| 2025-12-15 大幅移除通用 PvP 无敌帧，改用武器/弹幕免疫组；同时使用百分比穿甲 [S101] | [CombatPlayer.cs](Common/Combat/CombatPlayer.cs) 仍有旧免疫逻辑；配置默认近战与每攻击者免疫均 8 帧 | 待移植。公告不足以确定每个组的成员与冷却，不能把所有无敌帧直接设为 0 |
| 武器击退属性参与 PvP 击退的改动于 2026-05-16 回滚；神盾果永久免疫击退 [S142] | 仍有 PvPVariableKnockback，服务器全局击退倍率 0.8 | 旧逻辑需重审；神盾果行为待移植 |
| 移除随机散布，随机弹幕数量固定为平均值 [S148] [S149] | 未找到统一替换 | 待移植；公告没有所有武器的散布角度 |
| 制导武器右键正常追踪、左键非追踪；部分武器命中后才追踪 [S161] | 仅部分武器自定义制导 | 部分。逐武器核对输入，不要颠倒左右键 |
| 减益距离衰减：前段 56 格、系数 3 [S170] | 有伤害距离衰减，没有统一减益距离衰减 | 待确认公式与适用减益。系数 3 不能填入现有伤害衰减配置代替 |
| 施加者死亡清除其施加的减益，彩虹枪也清理 [S169] | 部分鞭子减益已跟踪来源与清理 | 部分；霜焰 II 的特定清理不能代表所有减益 |
| 修复弹幕来源、多段命中等 [S106] | 有旧弹幕钩子 | 待逐路径验证，尤其子弹幕、多人同时命中与死亡清理 |

服务器当前 Damage、ArmorPenetration、Falloff 表为空，Immunity 使用默认值；这不代表所有伤害等于原版，因为还有硬编码钩子。现有 ArmorPenetration 配置是否完整接入每条命中路径也需验证，不能假定填表即可生效。

## 2. 最新明确武器伤害

这里把“减少 X%”写成原伤害的剩余百分比。它们是公告数值，**不是已经安装的数值**；也不能当作最终实际伤害（还涉及防御、穿甲、来源与特殊效果）。较早数值可能受后来笼统平衡影响，需用代码或配置复核。

| 武器/弹幕 | 剩余伤害 | 来源 |
|---|---:|---|
| Flying Knife | 55% | [S174] |
| Daedalus Stormbow | 35% | 同上 |
| Chlorophyte Arrow | 50% | 同上 |
| Tactical Shotgun | 80% | 同上 |
| Chlorophyte Shotbow | 63% | 同上；原文方向描述有笔误，终值明确 |
| Pulse Bow | 67% | 同上 |
| Tsunami | 40% | 同上 |
| Light Disc | 80% | 同上，覆盖此前 75% |
| Tungsten Pickaxe | 20% | 同上 |
| Bubble Gun | 53% | [S172] |
| Starlight | 30% | 同上 |
| Flamethrower | 100% | 同上 |
| Scourge of the Corruptor | 90% | 同上 |
| Venus Magnum | 50% | 同上 |
| Staff of Earth | 75% | 同上 |
| Piranha Gun | 38% | 同上 |
| Grenade Launcher | 75% | 同上 |
| Nail Gun | 85% | 同上 |
| The Axe | 65% | 同上 |
| Razorblade Typhoon | 38% | [S169] |
| Stynger | 82% | [S170] |
| True Night's Edge | 90% | [S168] |
| Pew-matic Horn | 75% | 同上 |
| Uzi | 70% | 同上 |
| Spirit Flame | 60% | [S167] |
| Possessed Hatchet | 60% | [S166] |
| Heat Ray | 28% | 同上 |
| Venom Staff | 80% | [S158] |
| Xenopopper | 32% | [S162] |
| Nightglow | 17% | 同上 |
| Pearlwood Bow | 80% | 同上；原文增减方向与数值不一致 |
| True Excalibur | 87% | [S151] |
| Paladin's Hammer | 90% | 同上 |
| Eventide | 30% | 同上 |
| Sniper Rifle | 33% | [S129] |
| Shadowbeam Staff | 48% | 同上 |
| Toxic Flask | 40% | 同上 |
| Laser Machinegun | 24% | 同上 |
| Rocket Launcher | 52% | 同上 |
| Inferno Fork | 72% | 同上 |
| Keybrand | 75% | 同上 |
| Kraken | 50% | 同上 |
| Electrosphere Launcher 初始命中 | 53% | 同上 |
| Electrosphere 持续区域 | 20% | [S122]，不能和初始命中混用 |
| Boomstick | 60% | 同上 |

Rainbow Rod 在 9 月 12 日的“65% -70%”写法不够明确，70% 暂作候选；同帖还有作者忘记名称的武器，不擅自对应。3 月 22 日有“多数中后期武器再减少”的笼统描述，不能凭它补齐整张表。

## 3. 百分比穿甲

| 武器/弹幕 | 明确穿甲 | 来源 |
|---|---:|---|
| Meteor Staff | 45% | [S170] |
| Clockwork Assault Rifle | 40% | [S167] |
| Crystal Vile Shard | 33% | [S166] |
| Crystal Storm | 75% | 同上 |
| Sky Fracture | 50% | 同上 |
| Chain Knife | 33% | [S169] |
| Onyx Blaster、Bubble Gun | 各 33% | [S161] |
| Piranha Gun | 50% | [S158] |
| Flamethrower | 20% | [S130] |
| Tactical Shotgun | 35% | 同上，覆盖旧 50% |
| Nightglow | 50% | [S122] |
| Starlight、Eventide | 各 25% | 同上 |
| Toxic Flask | 50% | 同上 |
| Laser Machinegun、Xenopopper | 各 39% | 同上 |
| Heat Ray | 29% | 同上 |
| Venus Magnum | 33% | 同上 |
| Cool Whip 雪花 | 100% | [S129] |
| Forbidden 沙暴 | 33% | [S157] |
| Bone Helm 暗影手 | 50% | [S170] |
| Mushroom Spear、Space Gun | 各 100%（实验） | [S122] |
| Leaf Blower | 70%（实验） | 同上 |
| Rainbow Gun | 100%（实验） | [S129] |

百分比穿甲与旧版“减去若干防御点”不是一回事。实现时要避免物品与弹幕重复应用；未提到的武器不能自动归零。

## 4. 武器特殊行为

| 行为 | 状态 |
|---|---|
| Stormbow 弹幕出生后前 5 tick 不命中玩家 [S174] | 待移植 |
| Spirit Flame 鼠标制导；待机遇方块滑行，松手后还保留 1 秒相关行为 [S166] [S174] | 待移植，需核对碰撞细节 |
| Staff of Earth 新免疫组 [S174] | 待确认组与间隔，不能保留旧速度减伤后再盲叠 |
| Stynger 7 月允许弹体加碎片，8 月又限制直击后的碎片追加命中 [S161] [S165] | 以后一条为准；确切分组待确认 |
| Nightglow 增加普通追踪模式 [S165] | 本地已有部分制导，缺新版切换 |
| Piranha Gun 可以吸附敌方玩家 | 已有；新版倍率/穿甲另算 |
| Keybrand 对玩家应用原 PvE 特效；Butcher's Chainsaw 每秒 10 次命中并产生火花 [S121] | 待移植/确认完整命中限制 |
| Clockwork 把 musket/tungsten 子弹转换为高速弹 [S167] | 待移植 |
| Venom Staff 发射 6 枚而非 7 枚 [S162] | 待移植 |
| Orange Zapinator 1% 概率造成 10 倍伤害 [S167] | 明确但与去随机方向不同，是否采用单独决定 |
| Bone Helm：50 tick 冷却、20 基础伤害 [S170] | 待移植 |

## 5. 护甲与饰品：不要移植已经回滚的版本

| 项目 | 合并后的最后明确行为 | 本地对照 |
|---|---|---|
| 甲虫攻击套 | 近战 PvP 伤害积攒能量、每 tick 衰减；最初阈值 600/1440/3000、上限 3600。后来增益恢复每层原版 10% 伤害/攻速；最后公告能量获得倍率 6→9 [S127] [S134] [S142] | [旧实现](Common/Combat/PvP/PvPBeetleArmorPlayer.cs) 是击杀叠层，需重写。阈值是否仍沿用需核实 |
| 幽魂套 | 攻击玩家/Boss 积攒 wisps，Boss 获得量降低；护甲技能键或未绑定时双击上下主动释放，对最近友军治疗/敌人攻击；不同治疗对象有倍率，伤害 wisp 碰方块消失、闲置过期 [S174] [S175] | 旧版自动治疗，服务器治疗配置为 0。新版完整待移植，数值不全 |
| 十字项链/星星面纱 | PvP 受伤积能量、随时间衰减；达到半最大生命阈值时减伤 50% [S157] [S165] | 待移植；有公开源码，见下文 |
| 神圣套 | 也获得上述能量保护，公告标为实验 [S169] | 待移植，不能简单恢复原版闪避 |
| 英雄护盾 | 死亡产生 10 个漂浮心，限队友拾取 [S157] | 待移植；回血量/消失时限待确认 |
| 海龟套 | 移除旧 CHOJ 与非近战减伤害；近战攻速惩罚最终 -20% [S154] [S174] | 旧移动惩罚不是同一效果 |
| 减伤叠加 | 蠕虫围巾、冰冻龟壳、海龟套、叶绿面具相关减伤相乘 [S151] | 待逐钩子核对，不能线性相加 |
| 蘑菇矿套 | 受伤不再打断隐身；头盔同时按弹药和武器判定加成 [S149] | 待移植 |
| 手套 | 多件手套的攻速不叠加 [S143] | 待移植，有公开实现 |
| 贤者之石/神话护身符 | 防换装利用；后来补死亡换装漏洞，并加 heartreach 效果 [S155] [S157] | 已有旧防换装，后续修补需逐项对照 |
| 闪亮石 | 早期加队友治疗，后来改治疗曲线，9 月 25 日移除队友治疗 [S132] [S168] [S174] | 保留最新个人效果；不要把队友治疗抄回来 |
| 远古凿子 | 曾卸下锁饰品槽 15 分钟，2025-11-22 已移除 [S047] [S088] | 不移植旧惩罚 |
| 黑腰带/Master Ninja Gear/Strange Brew 等 | 解除禁用不代表原版 PvP 闪避有效 [S009] [S152] | 需结合新版免疫/保护实现，不靠“解禁”推断效果 |

[CrossNecklaceChanges.cs](https://github.com/PylonTeam/PvPAdventure/blob/erkys-features/Common/Combat/EJ/CrossNecklaceChanges.cs) 使用受伤 ×2、每 tick -1、阈值最大生命；与公告受伤 ×1、每 tick -0.5、阈值半生命是等价的整体缩放。[GodsProtection.cs](https://github.com/PylonTeam/PvPAdventure/blob/erkys-features/Content/Buffs/GodsProtection.cs) 确认最终伤害 ×0.5。能量保护条件适用于项链/面纱或完整神圣套。

[GlovesDontStack.cs](https://github.com/PylonTeam/PvPAdventure/blob/erkys-features/Common/Combat/EJ/GlovesDontStack.cs) 对 Feral Claws、Power Glove、Berserker's Glove、Mechanical Glove、Fire Gauntlet 每多一件扣除 0.12 近战攻速，其他手套效果并未全部取消。

## 6. 鞭子、召唤与减益

| 最新明确变化 | 本地差异 |
|---|---|
| 基础标记持续时间减半、护甲/饰品持续加成翻倍 [S157] | 当前基础仍 5 秒；倍率组合方式需核对 |
| Tiki 最后明确 2 倍，Bee/Spider/神圣召唤头也有加成；Obsidian 2 倍，Forbidden 延长标记 [S141] [S166] | 当前 Tiki 3.5 倍，缺多项 |
| Hercules Beetle 持续时间乘 1.33；Pygmy Necklace 最终 +33% [S162] [S170] | 待移植 |
| 饰品额外鞭子范围已移除；部分召唤套取消范围惩罚 [S098] | 当前部分相符，后续套装需核对 |
| 召唤物/哨兵可利用 PvP 鞭子标记，施加者自己不能利用 [S096] [S085] | [PvPWhipDebuffs.cs](Common/Combat/EJ/PvPWhipDebuffs.cs) 还排除召唤物 |
| Kaleidoscope：固定标记伤害 12、倍率 15% [S151] | 待同步 |
| Firecracker 爆炸最低 50 伤害，过期也触发 [S157] | 旧阈值不能照抄；触发来源、避免自伤与持续时间一起核对 |
| Cool Whip 追踪雪花，10 tick 冷却、100% 穿甲；持续时间影响雪花与霜焰 II [S124] [S129] [S169] | 仅部分；施加者死亡清霜焰 II 已有特定实现 |
| 召唤物/哨兵在玩家处生成而非鼠标处，消耗 0 魔力 [S138] | 待移植 |
| Tempest Staff 召唤物不能靠接触伤害命中玩家 [S144] | 待核对 |
| 武器必须保留在背包，主人死亡清理召唤物 | 已有列举式实现，不保证覆盖所有新增召唤物 |

## 7. 比赛计分与推进

### 最后明确计分，不沿用已经废除的旧奖励

2026-04-10 公告取消除邪教徒外的 Boss 分数，后来才逐项恢复部分奖励。不能从 2025 年公告直接搬回猪鲨/光女 20 分、石巨人/飞碟 4 分。[S131]

| 项目 | 最后明确公告 | Independent 当前 |
|---|---|---|
| 开局分 | 4 [S176] | 5 |
| 玩家击杀 | 1 [S131] | 2 |
| 各队首次 WoF | 4 [S176] | 统一 Boss 2 |
| 各队首次每只机械 Boss | 2，同上 | 统一 Boss 2，没有此处完整首次规则 |
| 骷髅王 | 5 [S144] | 统一 Boss 2 |
| 世纪之花 | 2 [S165] | 统一 Boss 2 |
| 邪教徒 | 10 [S141] | 统一 Boss 2 |
| 其他 Boss | 4 月清零后未找到明确恢复的，不默认有分 | 当前仍统一计分 |

公开分支默认开局 12、世纪之花 3，和公告的 4/2 不一致。上表用于决策，不宣称作者服务器现在一定如此；重复击杀、各队首次、额外分数和胜负结算仍需逐项确认。

| 节奏/机制 | 公告合并结果 | 判断 |
|---|---|---|
| 比赛默认时间 | 曾改 3 小时 [S135] | 主办方选择，不强制改 |
| 自动推进 | 40 分钟困难、85 分钟丛林、110 分钟地牢、130 分钟邪教坛；后来由自动击杀改为在对应世界位置生成 Boss [S147] [S154] | 待选择；需要真实生成 Boss，不只改世界旗标 |
| 取消 RTP、兔子赛跑开局 | 无限跳但逐次减速、免伤，只能首次离开出生区使用；快速坐骑/箱子/进入地下等结束 [S150] | 可选玩法；当前随机传送不因此自动移除 |
| 开局配装 | 新版约 70 件商品，预算 10 gold，价格经历多次调整 | 当前仅 3 个模板，预算机制已补，服务器暂禁用 |
| 悬赏商店 | 移除前世纪之花兑换、后续移除茶/生命果等旧内容 | 当前空目录；不要用早期商品表当新版目录 |
| 竞技场/Scrims | 两队投票、轮次 Boss、自定义配装、胜负统计 | 后续独立范围，不把已有框架当成完整实现 |

## 8. Boss 与进度

| 项目 | 最新明确变化与限制 | 本地判断 |
|---|---|---|
| 团队 Boss 血量 | 曾统一共享系数 0.5；EoW 后来改 100%，再移除其团队血量 [S090] [S155] | 已有团队血量框架，但 live BossBalance 为空；不能所有 Boss 一律套 0.5 |
| 重生前冻结 | 骷髅王/邪教徒 60 秒，世纪之花 30 秒 [S141] | 待移植/核对 |
| 防消失 | 400 格相关追踪范围、返回出生点、不普通脱战消失 [S154] | 需完整 AI，不直接关掉全部 despawn |
| 骷髅王 | 经典也使用专家 AI；最新生命“+50%”基数未明确 [S174] | 不直接写成原版 1.5 倍 |
| 蜂王 | 先 2.3 倍，再 -20%；最后相对哪个基数需确认 [S170] [S174] | 1.84 倍仅为推导候选 |
| WoF | 3 倍生命 [S162]；舌头不瞬杀为临时改动 [S155] | 临时规则须核实最终是否保留 |
| Queen Slime | 2 倍生命；2 件主掉落、2 件副掉落；旧接触伤害增益回滚 | 当前旧掉落数量需更新 |
| 世纪之花 | 5 倍生命；2 主、2 副、1 钥匙 [S142] [S152] | 已有钥匙，血量/掉落池需更新 |
| 石巨人 | 回原版生命；最终 2 主、2 副 [S145] [S154] | 不要用中间版本的一件掉落 |
| 猪鲨/光女 | 生命回原版、两件掉落；召唤饵改石巨人后获得 [S134] [S142] | 旧世纪之花后解锁已过时 |
| 光女白天 | 攻击翻倍、受到伤害 -75% [S103] | 本地白天受伤 ×0.4 和旧弹幕大幅削弱冲突；不能只叠新数值 |
| 机械 Boss | Prime 手臂不死、Destroyer 探针 2 HP/免击退为实验 [S144] | 单独决定 |
| 地下召唤 | 机械/Queen Slime/光女已重新允许地下召唤 [S142] | 旧表面限制不应当新版规则 |
| 双子 | 两只同时死亡 | 已有 |
| 团队归属掉落/袋子 | 按队分配 | 已有框架，新增池另算 |

## 9. 掉落、微光与配方

对照入口：[AdventureDropDatabase.cs](Common/Loot/DropRates/AdventureDropDatabase.cs)、[RecipeManager.cs](Common/Recipes/RecipeManager.cs)、[ShimmerRecipeGroups.cs](Common/Recipes/ShimmerRecipeGroups.cs)。

| 项目 | 最新明确行为 | 当前对照 |
|---|---|---|
| Snow Flinx | 额外保底 1 fur [S174] | 可从公开上游直接移植 |
| Deer Thing | 手工制作，5 魔矿+1 镜片+3 fur；移出箱子 [S170] | 配方公开，待同步 |
| Deerclops | 全武器表；牛排必掉为实验 | 不采用“掉几百万件”的旧玩笑/已撤回版本 |
| Ice Feather | 最终 50%，覆盖前一天保底 [S167] | 已有 1/2 |
| Turtle Shell | 1/2 [S151] | 当前 1/3 |
| Ectoplasm | 3–5 | 已有 |
| 机械掉落 | 40 souls；锭最少 20 | 当前 souls 40 已有，锭 18–30 不符 |
| Temple power cell | 最后明确 1/30；箱子 3 个 | 当前旧概率需改；不可沿用 1/10 中间值 |
| Biome key molds | 1/100；钥匙加机械魂配方 [S152] | 配方部分已有，概率需核对 |
| Plantera | Axe/Thorn Hook 保底，钥匙 1 | 钥匙已有，其他待核对 |
| 鲨鱼/猪鲨 | 鲨鱼 7–8 鳍、猪鲨 14 鳍 | 待同步 |
| Spirit Lamp / Frost Staff / Ice Sickle | 1/10、1/10、1/15 | 待同步 |
| Poison Staff / Uzi / Rally / Chain Knife | 1/20、1/15、1/6、11% | 待同步 |
| 地牢武器 | 多次保底调整；Kraken/Morning Star 后改 5% | 不能全设保底；当前 Morning Star 1/10 是旧值 |
| Skeletron Marrow | 早期“平均一件”不等于保底 | 本地 1/1000 ×1000 的旧规则应专门审查 |
| Goblin 入侵 | 困难后两天触发，覆盖旧立即触发 | 旧事件触发需更新 |
| Goblin Warlock | 团队血量/掉落；3 种 broken 武器，工匠修复；生命翻倍，最后哥布林可生成工匠 | 当前普通武器掉落，待移植 |
| Biome Mimic | 主/副两池，副掉落保底；Clinger Staff/Crystal Vile Shard 移副池 | 当前仍旧分池 |
| Duke/EoL 翅膀 | 最后移出随机掉落，但仍可微光制作 | 当前 Duke 配方组不含翅膀；EoL 组仍含已移除的 Terraprisma |
| 微光兑换 | Golem 主2/副2；Saucer2；Gold/Jungle2；Goblin 武器2；Biome Mimic 副2；普通 Mimic2 | 现有部分要求 3 件，需逐组更新 |
| Chlorophyte / Spectre | 叶绿 4 ore；幽魂 3 bars +1 ectoplasm | 已有 |
| Shroomite | 每锭 1 mushroom [S144] | 当前 3 |
| Mana Crystal | 3 stars [S134] | 待移植 |
| Worm / Eye | 6 chunks+10 powder；3 lenses | 已有 |
| Hellforge | 30 Hellstone+Furnace，在铁砧；不可微光回收 | 待移植 |
| 翅膀配方 | Demon/Angel 用 5 对应魂、3 feathers；Fairy 最后 45 pixie dust | 待核对，不能用旧 25 dust |
| 双向转换 | lenses/black lens、Titan/Feral、Shoe Spikes/Climbing Claws、Putrid Scent/Flesh Knuckles、Endless Quiver/Magic Quiver | 部分/待移植；转换数量仍查具体配方 |
| Ancient Manipulator | 全制作站之外还提供营火/树妖/心灯/蜂蜜效果 | 制作站已有，光环效果不等于 adjacency |

本节主要交叉来源：[S122] [S129] [S134] [S144] [S147] [S150] [S152] [S154] [S155] [S157] [S161] [S162] [S165] [S166] [S167] [S168] [S169] [S170] [S174]。实际移植前应逐项打开对应原帖、对照配方组及所有掉落调用路径。

## 10. 世界生成、便利功能与传送

| 项目 | 新版公告合并结果 | Independent |
|---|---|---|
| 草药 | 生长最终 3 倍；萌发生长先改 3 倍再减半，推导为 1.5 倍 | 待移植，1.5 为推导而非最终配置实测 |
| 天气 | 雨/沙暴 20 分钟，沙暴频率提高 | 旧雨时长 5–15 分钟，待核对 |
| 矿物 | 增加高阶/邪恶矿后又减少 | 参数不明；世界生成改动需要新地图 |
| 药水/锭战利品 | 分阶段移除罐中 buff 药水、箱中部分药水及锭；药水不再微光回材料 | 需逐表对照，不清空所有箱子药水 |
| 入侵区域/数量 | 主要出生区；最后 Goblin 200、Martian 300 | 旧数量/区域待核对 |
| 新传送门 | 每队一个，10 秒；阶段 HP 50/150/300，主人死亡销毁，替代旧镜子，不能直接传玩家 [S139] | 自研版 500 HP、主人死不消失；仍有旧 G 镜子 4 秒和 RTP，明显不同 |
| 起始法力 | 最后 100 | 当前 100，已有 |
| 重生时间 | 各原因统一 20 秒 [S124] | 待移植 |
| 永久信息饰品 | Gold Watch/Sextant/DPS 显示，商店移除对应商品 | 待移植 |
| UI | 小中大缩放、不可用图标变灰、门所在生物群落提示、队伍伤害文字、敌友 Hunter、保护能量显示 | 部分已有，不等于新版全套 |
| 队伍/床/地图/位置保存、队聊 | 多次修补 | 有 SSC 和队伍框架，需确认细节而非重写所有功能 |
| 虚荣显示 | 控制自己/别人显示 | 部分，当前不能等同新版选项 |
| 原版 loadouts | 2026-05-22 移除 | 玩法选择，待核对 |
| 一 Steam ID 每世界一个角色 | 作者身份绑定方案 | 与当前独立 SSC 身份方案不同，单独设计 |
| 暂停保存/断线处理/传送中断 | 公告多次修补 | 必须双客户端验证取消、死亡、掉线和重连 |

本节主要交叉来源：[S124] [S139] [S143] [S147] [S150] [S154] [S161] [S165] [S166] [S167] [S168] [S169] [S172]。

我们自行添加的 PowerBomb、完整钩爪跳跃、Shift 十次制作标为 `VANILLA145_BACKPORT`。这类 1.4.5 回移与作者 TPVPA 新版平衡是两条来源，不混在一起。当前 PowerBomb 的叶绿开采在全机械后允许，公告目标与“丛林躁动后”不一致，需单独决定。

## 11. 暂不搬的内容与未解信息

- 账号、Discord 身份、皮肤商店、线上奖励、作者后台比赛记录/成就/回放，不接入当前独立版；本地统计不是这些服务的替代实现。
- 不照搬公告中的特定 Steam ID 减伤、个人特例或玩笑规则。
- “作者忘了哪个武器”“某人改了点东西”“秘密道具”等不足以实现，保留待确认。
- 旧队友闪亮石治疗、远古凿子卸装惩罚、杀怪型甲虫叠层、旧 Boss 高额奖励等不能和最终版叠加。
- 部分实验规则无后续确认；列入候选，不当成正式规格。
- 没有全量作者运行配置、私有框架和每篇回复，无法精确复刻整个最新版。

## 12. 可执行的后续批次

| 批次 | 内容 | 验证标准 |
|---|---|---|
| A：战斗基础 | 免疫组、穿甲、伤害来源、距离衰减 | 两客户端记录每次命中、冷却组、实际防御/伤害；覆盖子弹幕与死亡清理 |
| B：装备 | 十字项链/神圣、甲虫、手套、海龟、鞭子 | 能量增减与阈值日志；换装/死亡/召唤物不重复生效 |
| C：幽魂 | 积攒、主动释放、友敌选择、碰撞/过期 | 玩家/Boss 两条积攒路径；多人目标与输入一致 |
| D：武器表 | 按上述最新明确表同步 | 先确认 A，逐类检查，旧硬编码不得重复减伤 |
| E：资源/QoL | 配方、掉落、显示、草药 | 配方清单与掉落池核对；生成类使用新世界 |
| F：比赛规则 | 计分、自动推进、RTP/赛跑、门、商店 | 用户选择规则后再实施，完整开局/暂停/结束/重连流程 |

## 附录：全部主帖索引

按线程创建顺序排列；日期使用页面显示日期。链接直接指向原帖。索引包含无参数公告、皮肤、玩笑和 Wiki，这些不是全部都应移植。

| 来源 | 日期 | 标题 |
|---|---|---|
| [S001] | 2025-04-04 | PHM claims update |
| [S002] | 2025-04-04 | Minor Weapon Balance Changes |
| [S003] | 2025-04-06 | Iframe Changes |
| [S004] | 2025-04-06 | Loudout Lock is now an included mod |
| [S005] | 2025-04-06 | Flyingh knife |
| [S006] | 2025-04-06 | Persistant teams |
| [S007] | 2025-04-07 | Iframes with bosses should now feel Vanilla |
| [S008] | 2025-04-09 | Shiny Stone Re-Added and Reworked |
| [S009] | 2025-04-10 | The shotgun update |
| [S010] | 2025-04-13 | Performance, Polish, and Pausing |
| [S011] | 2025-04-13 | PVE Nerfs Secret Documents Leaked |
| [S012] | 2025-04-13 | Binoculars always drop from EoC |
| [S013] | 2025-04-14 | Hit-Sounds Kill-Sounds and pTerraBlade Nerf |
| [S014] | 2025-04-17 | minor update |
| [S015] | 2025-04-19 | Experimental Points, jetpack nerf, cursor weapon changes |
| [S016] | 2025-04-20 | The Stupid Fucking Monkey Update |
| [S017] | 2025-04-21 | The Dungeon Rush Update |
| [S018] | 2025-04-24 | bulb and spawn rates adjustements |
| [S019] | 2025-04-26 | Temple Rework, Guide Dunking, and Balance |
| [S020] | 2025-05-05 | New Bounty, Fishron Fix, and MegaShark |
| [S021] | 2025-05-13 | Endgame point Balance and fixes |
| [S022] | 2025-05-15 | Sandstorm in a bottle change |
| [S023] | 2025-05-24 | Major Update: Shimmer craft-able boss drops |
| [S024] | 2025-05-27 | The community pushback update |
| [S025] | 2025-05-28 | Minor update |
| [S026] | 2025-05-30 | the hot and sloppy update |
| [S027] | 2025-06-02 | minor update |
| [S028] | 2025-06-06 | Experimental changes |
| [S029] | 2025-06-06 | duke hp buff |
| [S030] | 2025-06-08 | MAJOR UPDATE pt1: Beetle Rework PT1 and Visibility |
| [S031] | 2025-06-08 | MAJOR UPDATE pt2: |
| [S032] | 2025-06-08 | MAJOR UPDATE pt3: Summoner and Mage Changes |
| [S033] | 2025-06-08 | a minor update: THE CRASHOUT UPDATE |
| [S034] | 2025-06-10 | Endgame armor adjustments etc |
| [S035] | 2025-06-17 | balance changes |
| [S036] | 2025-06-19 | Real changelog |
| [S037] | 2025-06-21 | Major Balance Changes (Beware) |
| [S038] | 2025-06-24 | silly little balance update (fuck tanks for now) |
| [S039] | 2025-06-25 | New Mods and fixes |
| [S040] | 2025-06-25 | addressing the controversy... :hot: |
| [S041] | 2025-06-26 | TITLE |
| [S042] | 2025-07-01 | minor update |
| [S043] | 2025-07-12 | THE Empress Update pt 1 |
| [S044] | 2025-07-12 | THE Empress Update pt2 |
| [S045] | 2025-07-14 | quick fixes |
| [S046] | 2025-07-26 | change log |
| [S047] | 2025-08-02 | THE Ancient Update |
| [S048] | 2025-08-03 | wihp buffs update |
| [S049] | 2025-08-07 | CHANGE LOG |
| [S050] | 2025-08-09 | The largest update in TPVPA history |
| [S051] | 2025-08-09 | shunner srecirpes for wooden c hest items |
| [S052] | 2025-08-10 | guh |
| [S053] | 2025-08-16 | Shroomite availability |
| [S054] | 2025-08-23 | Recipes and their Happenings |
| [S055] | 2025-08-25 | Part 2 |
| [S056] | 2025-08-30 | Major Update (still not per team boss hp) |
| [S057] | 2025-08-31 | Major Update Update paort 2 |
| [S058] | 2025-09-03 | Cool rebalances update |
| [S059] | 2025-09-06 | PER TEAM BOSS HP (Finally) :fire: |
| [S060] | 2025-09-07 | Mattes Morning Balancing fiasco |
| [S061] | 2025-09-13 | late game boss balance |
| [S062] | 2025-09-13 | Feeling cute, might delete claims |
| [S063] | 2025-09-14 | quiver now only drops post plant and is unbanmned |
| [S064] | 2025-09-19 | The Founder (of TPVPA) |
| [S065] | 2025-09-20 | The 'both change and log' fallacy |
| [S066] | 2025-09-21 | the last minute update |
| [S067] | 2025-09-27 | rocket launcher pvp dmg nerf |
| [S068] | 2025-09-27 | Josh from paykings |
| [S069] | 2025-10-06 | little tiny baby update |
| [S070] | 2025-10-11 | FUCK FLASKS |
| [S071] | 2025-10-11 | more consistent endgame armor and zopper tweaks |
| [S072] | 2025-10-12 | THE SUMMONER UPDATE |
| [S073] | 2025-10-14 | The secret item |
| [S074] | 2025-10-15 | Spectre armor changes part 1 |
| [S075] | 2025-10-15 | THE SUMMONER UPDATE ACT 2 |
| [S076] | 2025-10-17 | The rupdate |
| [S077] | 2025-10-18 | The "What are we even doing" update |
| [S078] | 2025-10-19 | quickie |
| [S079] | 2025-10-26 | small little chloj on a rainy morning |
| [S080] | 2025-10-26 | RaR's melee nerf |
| [S081] | 2025-10-30 | THE EVENTS UPDATE PART 0 |
| [S082] | 2025-11-03 | Change log test |
| [S083] | 2025-11-06 | The Summoner Update's Third Quartile + Team Instanced Drops |
| [S084] | 2025-11-09 | UPDATE, GO |
| [S085] | 2025-11-13 | The Super Super DUPER Vanilla Update - Part 1 |
| [S086] | 2025-11-16 | The Ponky Protocol |
| [S087] | 2025-11-22 | The Don’t Bee Mad Update |
| [S088] | 2025-11-22 | The bullshit update |
| [S089] | 2025-11-26 | The Claims Update - Part 12 |
| [S090] | 2025-11-27 | Engame pve changes |
| [S091] | 2025-11-30 | The anecdotal update |
| [S092] | 2025-12-01 | The Nostalgia Update |
| [S093] | 2025-12-06 | The Artificial Intelligence Update |
| [S094] | 2025-12-07 | Weapon Balance Changes |
| [S095] | 2025-12-09 | The tmod update update |
| [S096] | 2025-12-09 | The Hairy Agressive Suicidal Articial Intelligent Explosive Changelog / The Summoner Update Part 2.5 |
| [S097] | 2025-12-10 | The jetpack update |
| [S098] | 2025-12-10 | Small chloj on a magical night |
| [S099] | 2025-12-11 | this one |
| [S100] | 2025-12-13 | The Calm Before The Storm |
| [S101] | 2025-12-15 | The Storm |
| [S102] | 2025-12-17 | Only the worthy may view |
| [S103] | 2025-12-17 | The Radioactive Changelog |
| [S104] | 2025-12-18 | small points changes |
| [S105] | 2026-01-15 | BIG changes |
| [S106] | 2026-02-02 | Child Based Projectile Crash fix! |
| [S107] | 2026-02-04 | Vanity is back! (but only if you want) |
| [S108] | 2026-02-04 | SSC now saves Map data! |
| [S109] | 2026-02-04 | no more change logs (thanks @playering)! |
| [S110] | 2026-02-06 | Team Colored Bed outlines/ and loot |
| [S111] | 2026-02-06 | Toggle for team chat and all chat |
| [S112] | 2026-02-09 | Fixed k/d overflowing |
| [S113] | 2026-02-09 | Added match history |
| [S114] | 2026-02-10 | TPVPA Achievements |
| [S115] | 2026-02-16 | Balance Changes + PPP |
| [S116] | 2026-02-17 | I'm back PART 1 |
| [S117] | 2026-02-17 | I'm back PART 2 |
| [S118] | 2026-02-19 | Discord names in game + bug fixes |
| [S119] | 2026-02-19 | THE update (claims, balance, and new systems) |
| [S120] | 2026-02-25 | The Wiki (pinned post) |
| [S121] | 2026-02-27 | The XYurets is calling update |
| [S122] | 2026-02-28 | Weapon Balance Changes (calm before the super storm)) PART 1 |
| [S123] | 2026-03-10 | The Grand Return (Potion) Part 1 |
| [S124] | 2026-03-10 | The Grand Return (Partion 2) |
| [S125] | 2026-03-15 | The Canned Worm (Portion) 3 |
| [S126] | 2026-03-20 | The First Tower (Dark Are The Days Coming Fourth) |
| [S127] | 2026-03-20 | THE BEETLE ARMOR REWORK UPDATE |
| [S128] | 2026-03-20 | HYPEREXPIRIMENTAL CHANGELOG (might delete later) |
| [S129] | 2026-03-22 | small little chloj on a dark and stormy night while the trees whistle in the wind and the albatross |
| [S130] | 2026-03-29 | balance changes |
| [S131] | 2026-04-10 | THE CHANGELOG |
| [S132] | 2026-04-10 | Shiny Stone 7x7 |
| [S133] | 2026-04-22 | The Reckoning of G |
| [S134] | 2026-04-22 | Goop War XVIII |
| [S135] | 2026-04-22 | the Time has come |
| [S136] | 2026-04-26 | The Water |
| [S137] | 2026-04-28 | The Great Schism - Part 1 |
| [S138] | 2026-04-29 | The Great Schism - Part 2 |
| [S139] | 2026-04-30 | New Portal Travel System |
| [S140] | 2026-05-01 | A Brief Interlude |
| [S141] | 2026-05-15 | The Great Schism - Part 3 |
| [S142] | 2026-05-16 | Over the Hill with the Swords of a Thousand Men |
| [S143] | 2026-05-22 | The changelog when i  fixed               my github |
| [S144] | 2026-05-25 | Barbary Coast |
| [S145] | 2026-05-29 | Payback |
| [S146] | 2026-05-31 | Visualization |
| [S147] | 2026-05-31 | To You, The Worthless Excuse |
| [S148] | 2026-06-03 | The Great Schism - Part 4 |
| [S149] | 2026-06-05 | Plan A |
| [S150] | 2026-06-20 | The Great Schism - Finale |
| [S151] | 2026-06-21 | Kafkaesque |
| [S152] | 2026-06-23 | Park The Car In Harvard Yard |
| [S153] | 2026-06-25 | The River Styx |
| [S154] | 2026-06-27 | The Happiness of All Mankind - Part 1 |
| [S155] | 2026-06-30 | The Happiness of All Mankind - Part 2 |
| [S156] | 2026-07-06 | The changelog of madness |
| [S157] | 2026-07-12 | Reach Heaven; (Part 1) |
| [S158] | 2026-07-14 | Your Savior: |
| [S159] | 2026-07-15 | ⚔️Arenas⚔️ |
| [S160] | 2026-07-17 | teensy tiny changes that i wont even ping for thats how teensy they are |
| [S161] | 2026-07-20 | Through Violence; (Part 2) |
| [S162] | 2026-07-22 | Because, Where We're Going; (Part 3) |
| [S163] | 2026-07-23 | New UI |
| [S164] | 2026-07-27 | Small changes |
| [S165] | 2026-08-01 | There Will Be No Sadness; (Part 4) |
| [S166] | 2026-08-12 | There Will Only Be; (Part V) |
| [S167] | 2026-08-13 | Sneaky Changelog |
| [S168] | 2026-08-29 | The Name Of The Game (Part I) |
| [S169] | 2026-09-01 | The Rigor Mortis Update |
| [S170] | 2026-09-05 | The Annoying Mosquito Update |
| [S171] | 2026-09-11 | 5 new skins |
| [S172] | 2026-09-12 | Matte Sevai does a changelog |
| [S173] | 2026-09-15 | 3 new skins and one resprite |
| [S174] | 2026-09-25 | ANTEDILUVIAN - Part 1 |
| [S175] | 2026-10-01 | Mors Vincit Omnia |
| [S176] | 2026-10-04 | A chloj so micro im not even pinging for it |

[S001]: https://discord.com/channels/801552433800806450/1357581540707536947
[S002]: https://discord.com/channels/801552433800806450/1357597572956557385
[S003]: https://discord.com/channels/801552433800806450/1358145345015517265
[S004]: https://discord.com/channels/801552433800806450/1358147000402710817
[S005]: https://discord.com/channels/801552433800806450/1358153119149588767
[S006]: https://discord.com/channels/801552433800806450/1358154822150525120
[S007]: https://discord.com/channels/801552433800806450/1358514413627506979
[S008]: https://discord.com/channels/801552433800806450/1359250148206575767
[S009]: https://discord.com/channels/801552433800806450/1359563057789141286
[S010]: https://discord.com/channels/801552433800806450/1360683286606643301
[S011]: https://discord.com/channels/801552433800806450/1360806112294600916
[S012]: https://discord.com/channels/801552433800806450/1360823688223526963
[S013]: https://discord.com/channels/801552433800806450/1361077719085093006
[S014]: https://discord.com/channels/801552433800806450/1362106634738077696
[S015]: https://discord.com/channels/801552433800806450/1362883052539482132
[S016]: https://discord.com/channels/801552433800806450/1363222911741067395
[S017]: https://discord.com/channels/801552433800806450/1363784098199506964
[S018]: https://discord.com/channels/801552433800806450/1364636771597422612
[S019]: https://discord.com/channels/801552433800806450/1365567681511686235
[S020]: https://discord.com/channels/801552433800806450/1368651881085145220
[S021]: https://discord.com/channels/801552433800806450/1371673550641037393
[S022]: https://discord.com/channels/801552433800806450/1372242390630404198
[S023]: https://discord.com/channels/801552433800806450/1375679497436659774
[S024]: https://discord.com/channels/801552433800806450/1376709275971358822
[S025]: https://discord.com/channels/801552433800806450/1376986582120140851
[S026]: https://discord.com/channels/801552433800806450/1377711703378235513
[S027]: https://discord.com/channels/801552433800806450/1378784315537883157
[S028]: https://discord.com/channels/801552433800806450/1380295957647724645
[S029]: https://discord.com/channels/801552433800806450/1380304863036178516
[S030]: https://discord.com/channels/801552433800806450/1381016926482075669
[S031]: https://discord.com/channels/801552433800806450/1381017304082546688
[S032]: https://discord.com/channels/801552433800806450/1381024670500786268
[S033]: https://discord.com/channels/801552433800806450/1381217673232519268
[S034]: https://discord.com/channels/801552433800806450/1381769210866307213
[S035]: https://discord.com/channels/801552433800806450/1384329746703192174
[S036]: https://discord.com/channels/801552433800806450/1384955969125224591
[S037]: https://discord.com/channels/801552433800806450/1385848978716753960
[S038]: https://discord.com/channels/801552433800806450/1386779974639550485
[S039]: https://discord.com/channels/801552433800806450/1387122947029729362
[S040]: https://discord.com/channels/801552433800806450/1387127793367912478
[S041]: https://discord.com/channels/801552433800806450/1387626787420504134
[S042]: https://discord.com/channels/801552433800806450/1389321721646940170
[S043]: https://discord.com/channels/801552433800806450/1393455515128037496
[S044]: https://discord.com/channels/801552433800806450/1393455599143878686
[S045]: https://discord.com/channels/801552433800806450/1394019081795010730
[S046]: https://discord.com/channels/801552433800806450/1398519187831390368
[S047]: https://discord.com/channels/801552433800806450/1401089787854852159
[S048]: https://discord.com/channels/801552433800806450/1401521721751896136
[S049]: https://discord.com/channels/801552433800806450/1402732109495533678
[S050]: https://discord.com/channels/801552433800806450/1403564848834678814
[S051]: https://discord.com/channels/801552433800806450/1403696989354332310
[S052]: https://discord.com/channels/801552433800806450/1403817783153918073
[S053]: https://discord.com/channels/801552433800806450/1406082214382407803
[S054]: https://discord.com/channels/801552433800806450/1408580467988365342
[S055]: https://discord.com/channels/801552433800806450/1409253392555184198
[S056]: https://discord.com/channels/801552433800806450/1411234145107181568
[S057]: https://discord.com/channels/801552433800806450/1411417214795387070
[S058]: https://discord.com/channels/801552433800806450/1412651033888297020
[S059]: https://discord.com/channels/801552433800806450/1413660926950903939
[S060]: https://discord.com/channels/801552433800806450/1413916794137219222
[S061]: https://discord.com/channels/801552433800806450/1416194678604169320
[S062]: https://discord.com/channels/801552433800806450/1416315344963764305
[S063]: https://discord.com/channels/801552433800806450/1416498882153676900
[S064]: https://discord.com/channels/801552433800806450/1418335507943002172
[S065]: https://discord.com/channels/801552433800806450/1418649157807378693
[S066]: https://discord.com/channels/801552433800806450/1419033755024363591
[S067]: https://discord.com/channels/801552433800806450/1421200363637313567
[S068]: https://discord.com/channels/801552433800806450/1421206446934065253
[S069]: https://discord.com/channels/801552433800806450/1424447942521127094
[S070]: https://discord.com/channels/801552433800806450/1426313643301142700
[S071]: https://discord.com/channels/801552433800806450/1426401977780142171
[S072]: https://discord.com/channels/801552433800806450/1426628853354332261
[S073]: https://discord.com/channels/801552433800806450/1427511133304262706
[S074]: https://discord.com/channels/801552433800806450/1427761916180172901
[S075]: https://discord.com/channels/801552433800806450/1427768891903443014
[S076]: https://discord.com/channels/801552433800806450/1428535005143105556
[S077]: https://discord.com/channels/801552433800806450/1429017729993674802
[S078]: https://discord.com/channels/801552433800806450/1429173120949813373
[S079]: https://discord.com/channels/801552433800806450/1431706031129497833
[S080]: https://discord.com/channels/801552433800806450/1431707725624119499
[S081]: https://discord.com/channels/801552433800806450/1433146412190793868
[S082]: https://discord.com/channels/801552433800806450/1434610635005759589
[S083]: https://discord.com/channels/801552433800806450/1435693963964317706
[S084]: https://discord.com/channels/801552433800806450/1436793699882237983
[S085]: https://discord.com/channels/801552433800806450/1438436353925971968
[S086]: https://discord.com/channels/801552433800806450/1439330882740683004
[S087]: https://discord.com/channels/801552433800806450/1441673134628667494
[S088]: https://discord.com/channels/801552433800806450/1441698731639439423
[S089]: https://discord.com/channels/801552433800806450/1443140876619616326
[S090]: https://discord.com/channels/801552433800806450/1443292201110077621
[S091]: https://discord.com/channels/801552433800806450/1444409191266652332
[S092]: https://discord.com/channels/801552433800806450/1444939087013482689
[S093]: https://discord.com/channels/801552433800806450/1446752541919547472
[S094]: https://discord.com/channels/801552433800806450/1446928550002163713
[S095]: https://discord.com/channels/801552433800806450/1447659471823179859
[S096]: https://discord.com/channels/801552433800806450/1447865420949225534
[S097]: https://discord.com/channels/801552433800806450/1448020363953836083
[S098]: https://discord.com/channels/801552433800806450/1448231583969448057
[S099]: https://discord.com/channels/801552433800806450/1448374592824545402
[S100]: https://discord.com/channels/801552433800806450/1449299997618212915
[S101]: https://discord.com/channels/801552433800806450/1450037793505148988
[S102]: https://discord.com/channels/801552433800806450/1450636741747671151
[S103]: https://discord.com/channels/801552433800806450/1450758918790844567
[S104]: https://discord.com/channels/801552433800806450/1450927081776087254
[S105]: https://discord.com/channels/801552433800806450/1461056875092119582
[S106]: https://discord.com/channels/801552433800806450/1467755831633117381
[S107]: https://discord.com/channels/801552433800806450/1468322611900584008
[S108]: https://discord.com/channels/801552433800806450/1468340657390092343
[S109]: https://discord.com/channels/801552433800806450/1468341156126658673
[S110]: https://discord.com/channels/801552433800806450/1469156297990475939
[S111]: https://discord.com/channels/801552433800806450/1469156698110427167
[S112]: https://discord.com/channels/801552433800806450/1470105776830808282
[S113]: https://discord.com/channels/801552433800806450/1470158726081155093
[S114]: https://discord.com/channels/801552433800806450/1470672930399584350
[S115]: https://discord.com/channels/801552433800806450/1472666899228790845
[S116]: https://discord.com/channels/801552433800806450/1473208456642302016
[S117]: https://discord.com/channels/801552433800806450/1473239548275331102
[S118]: https://discord.com/channels/801552433800806450/1473753203458707477
[S119]: https://discord.com/channels/801552433800806450/1473754134355251421
[S120]: https://discord.com/channels/801552433800806450/1476005375739433100
[S121]: https://discord.com/channels/801552433800806450/1476862622552031323
[S122]: https://discord.com/channels/801552433800806450/1477131001275744367
[S123]: https://discord.com/channels/801552433800806450/1480831310023889017
[S124]: https://discord.com/channels/801552433800806450/1480831551561400321
[S125]: https://discord.com/channels/801552433800806450/1482564298495492156
[S126]: https://discord.com/channels/801552433800806450/1484307733112033300
[S127]: https://discord.com/channels/801552433800806450/1484476735339888651
[S128]: https://discord.com/channels/801552433800806450/1484479523599810742
[S129]: https://discord.com/channels/801552433800806450/1485238782742757407
[S130]: https://discord.com/channels/801552433800806450/1487530121244971220
[S131]: https://discord.com/channels/801552433800806450/1491913742236123176
[S132]: https://discord.com/channels/801552433800806450/1491919559131664476
[S133]: https://discord.com/channels/801552433800806450/1496242371564929114
[S134]: https://discord.com/channels/801552433800806450/1496394823974916106
[S135]: https://discord.com/channels/801552433800806450/1496397040580694016
[S136]: https://discord.com/channels/801552433800806450/1497671803030470837
[S137]: https://discord.com/channels/801552433800806450/1498598593286766662
[S138]: https://discord.com/channels/801552433800806450/1498834166618390550
[S139]: https://discord.com/channels/801552433800806450/1499104567781228634
[S140]: https://discord.com/channels/801552433800806450/1499704593842704435
[S141]: https://discord.com/channels/801552433800806450/1504757552913580053
[S142]: https://discord.com/channels/801552433800806450/1505112979820249148
[S143]: https://discord.com/channels/801552433800806450/1507356478648877146
[S144]: https://discord.com/channels/801552433800806450/1508383649643364402
[S145]: https://discord.com/channels/801552433800806450/1509849534702813355
[S146]: https://discord.com/channels/801552433800806450/1510341690534137980
[S147]: https://discord.com/channels/801552433800806450/1510619817944682649
[S148]: https://discord.com/channels/801552433800806450/1511608275639074816
[S149]: https://discord.com/channels/801552433800806450/1512409321462304818
[S150]: https://discord.com/channels/801552433800806450/1517678609408589854
[S151]: https://discord.com/channels/801552433800806450/1517926844933083298
[S152]: https://discord.com/channels/801552433800806450/1518690543175991517
[S153]: https://discord.com/channels/801552433800806450/1519606950923468821
[S154]: https://discord.com/channels/801552433800806450/1520362218863067156
[S155]: https://discord.com/channels/801552433800806450/1521457233131470878
[S156]: https://discord.com/channels/801552433800806450/1523407449627955251
[S157]: https://discord.com/channels/801552433800806450/1525778194458869791
[S158]: https://discord.com/channels/801552433800806450/1526475416448012368
[S159]: https://discord.com/channels/801552433800806450/1526741512405127260
[S160]: https://discord.com/channels/801552433800806450/1527701719700738218
[S161]: https://discord.com/channels/801552433800806450/1528671551572938803
[S162]: https://discord.com/channels/801552433800806450/1529509613475725323
[S163]: https://discord.com/channels/801552433800806450/1529520039731003532
[S164]: https://discord.com/channels/801552433800806450/1531010248108933452
[S165]: https://discord.com/channels/801552433800806450/1533018921404207164
[S166]: https://discord.com/channels/801552433800806450/1536896204078719096
[S167]: https://discord.com/channels/801552433800806450/1537406969885757510
[S168]: https://discord.com/channels/801552433800806450/1542970224071016568
[S169]: https://discord.com/channels/801552433800806450/1544281708915073176
[S170]: https://discord.com/channels/801552433800806450/1545700035029762118
[S171]: https://discord.com/channels/801552433800806450/1547753469148926043
[S172]: https://discord.com/channels/801552433800806450/1548146285775691849
[S173]: https://discord.com/channels/801552433800806450/1549175373210849360
[S174]: https://discord.com/channels/801552433800806450/1552989118454308914
[S175]: https://discord.com/channels/801552433800806450/1555173389830979675
[S176]: https://discord.com/channels/801552433800806450/1556238704446545962
