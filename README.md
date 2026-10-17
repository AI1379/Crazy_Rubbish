# Crazy Rubbish · 涌现

2026-10-17：[体验流程修复](Docs/Development/17_体验流程修复.md)修复居民取货空路径，缩短开局准备，新增建设缺料采购、批量出售预估、停产诊断、自动化入门、界面缩放和存档列表。原有物资总量、工资周期及v1数值基线保留。

2026-10-15：[电动生产、输送与维护](Docs/Development/16_电动生产与输送维护.md)已接入电动/自动木工、逐格传送带、独立过滤接口和维护岗位。B打开建造页；点击机器安排操作/维护，点击输送设施查看供电、实物和过滤。加工、磨损、带上货物和施工预约随模式5存档保存，可继续读取模式2—4旧档。

2026-10-15：[自由建设与电网](Docs/Development/15_自由建设与电网.md)采用10月11日v1完整策划基线。B打开建造页，选择新设施后在地图放置，R旋转；点击已建设施可搬迁、拆除或出售。同类设施拥有独立库存、加工状态和工具槽。电线每件铺4格，按实际连通网络公平供电，完全空载不烧燃料。新局需接线到住房／路灯后实际发电才取得招募认可；旧档迁移保留原有资产及经济状态。完整其他产线和三区返程仍需后续接入。

垃圾回收与产线经营 Unity 项目，现有可试玩的首区 Demo。工作名称为 **Crazy Rubbish**，正式游戏名称待定。项目根目录就是本仓库目录。

最新物品操作见[格子物品栏与地面拾取](Docs/Development/14_格子物品栏与拾取.md)：I打开背包，点击仓库前往并打开；左键整叠拿放、右键拆半／放一份、拖拽或Shift快速转移。E默认尽量取整堆，附近显示余量，拿空后地面图标立即消失。容量仍按原体积规则计算。

2026-10-14最新调整为[稀疏开局与认识环境](Docs/Development/13_稀疏开局与认识环境.md)：保留两个洞、旧仓库和旧住房，物资分批到达，设施建成后出现。先认识地图对象，再在新物品／机制出现时展示用途教学。左键拖动地图、短按物品看用途；右键行走，滚轮缩放。旧存档不会重复获得保障物资。

2026-10-14第二轮 UI 改造采用[按需打开的管理与设施面板](Docs/Development/12_按需界面与场景检查.md)：地图使用完整窗口，场景常驻文字全部隐藏，停留 0.4 秒显示悬停提示。左键查看设施，右键／WASD 移动；B/P/I/H 开关管理页，Esc 优先收起面板。保存、读取收进菜单。

2026-10-14已改造[工坊 UI](Docs/Development/11_UI设计与验证.md)：顶部资源总览、石墨灰与琥珀色控件、现场工序快捷导航、设施／居民／物资卡片、可拖动滚动条和阶段目标菜单；保留原有操作及存档玩法。

2026-10-13已导入美术交付的71张PNG，配置为171个可引用精灵（含111个图集子精灵），位于`Assets/_Game/Art/Delivery_20261013`。Main试玩场景已换用新人物、场景、生产设备、垃圾堆和物品图标；角色接入四向与搬运/拾取/锯切姿势。使用方式、来源资料和当前接入边界见[美术素材导入](Docs/Development/10_美术素材导入.md)。

## 打开项目

1. 使用 **Unity 2022.3.62f3**。
2. Unity Hub → Add / 添加磁盘上的项目 → 选择本目录。
3. 打开 `Assets/_Game/Scenes/Main.unity`，点击Play，在菜单选择“开始试玩”。

2026-10-12已接入首区八项目标和阶段结算、食物/饮水供给与钱包、工作台工具装配、保护工资的替补采购、欠薪暴动和失败恢复。支持点击地图行走。推荐路线、验收数据与尚未实现内容见[首区Demo](Docs/Development/08_首区Demo.md)。

新游戏自动展示[新手欢迎页与实操引导](Docs/Development/09_新手教程.md)：先认识移动、交互和暂停，再通过左下角常驻任务卡完成12步操作。任务卡显示实际进度、目标和下一步按钮，覆盖拾取到demo结算；支持收起、跳过、H逐课复习和保存继续。

Main现在保存实际的World地图与Interface界面对象，不进入Play也能在Scene中查看。若视角偏离，使用`Crazy Rubbish → Focus World in Scene`；选中Interface后按F可聚焦UI。界面分为现场、人员、仓库、教程四页，地图支持滚轮缩放和中键拖动。编辑方式见[界面与场景编辑](Docs/Development/07_界面与场景编辑.md)。

下方为历史S5阶段说明；现行策划为2026-10-11 v1，第26章与JSON优先，取消固定整局小时数。当前工程采用32×28首区、0.8移速、现场输入输出、施工/生产预约、工具实例和实际人力配送，保留动态交易、工资并加入当前状态保存继续。操作和当前缺口见[S5调整说明](Docs/Development/06_S5调整与验证.md)，完整范围见[合并策划](Docs/Design/00_合并策划案.md)。

WASD／方向键移动，E就近交互，Shift+E附近整堆拾取，Q入库，F进入/退出木工岗位，Esc取消交互，空格暂停，1／2／3倍速，滚轮缩放。侧栏可实际投料/取出、交接工具、采购及交易，顶栏保存/读取。

## 基础依赖

| 包 | 固定版本 | 用途 |
|---|---|---|
| 2D Sprite | 1.0.0 | 精灵导入与编辑 |
| 2D Tilemap Editor | 1.0.0 | 网格地图与瓦片编辑 |
| Input System | 1.14.0 | 后续键鼠／手柄输入 |
| Unity UI (uGUI) | 1.0.0 | 后续游戏界面 |
| TextMesh Pro | 3.0.7 | 后续文字排版 |
| Test Framework | 1.1.33 | 后续规则测试基础设施 |
| Visual Studio Editor | 2.0.22 | C# 工程与 IDE 集成 |
| Unity Version Control | 2.7.1 | Unity Editor 内的 Plastic SCM／UVCS 集成 |

版本依据本机 2022.3 Editor 附带包和 2D 模板选取，并固定在 `Packages/manifest.json` 和 `packages-lock.json`。保留内置渲染管线；编辑器默认 2D；Input Handling 设为 Both。M1 使用 Unity Input Manager 和 uGUI，后续可切换 Input System／TMP。中文字体为可随包分发的 OFL 字体子集，授权随构建复制。

2D 编辑包见 [Unity Sprite 文档](https://docs.unity3d.com/2022.3/Documentation/Manual/com.unity.2d.sprite.html) 和 [Tilemap 文档](https://docs.unity3d.com/2022.3/Documentation/Manual/com.unity.2d.tilemap.html)；输入开关依据 [Input System 安装说明](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.14/manual/Installation.html)。

## 策划资料

- [文档索引与整理规则](Docs/README.md)
- [现行合并策划](Docs/Design/00_合并策划案.md)
- [设计总览](Docs/Design/01_设计总览.md)
- [资源与设施目录](Docs/Design/02_资源与设施目录.md)
- [首批产线与科技](Docs/Design/03_首批产线与科技.md)
- [待确认事项与文档差异](Docs/Design/04_待确认事项与文档差异.md)
- [实施顺序与验收](Docs/Development/01_实施顺序与验收.md)
- [项目结构与协作](Docs/Development/02_项目结构与协作.md)
- [M1 原型说明](Docs/Development/03_M1原型说明.md)
- [M2起步说明](Docs/Development/04_M2起步说明.md)
- [CI与Windows发布](Docs/Development/05_CI与发布.md)
- [当前S5调整与验证](Docs/Development/06_S5调整与验证.md)
- [首区Demo与推荐试玩路线](Docs/Development/08_首区Demo.md)

输入原文保存于Docs/Source，10月11日v1为当前基线，S1—S5保留历史。全部新增前期默认规则已纳入计划，已实现与尚未接入分开记录，不能把文档填完当成整局平衡验证。

## 本地工具与仓库

```powershell
.\Tools\Unity.ps1 -Action Import
.\Tools\Unity.ps1 -Action Setup
.\Tools\Unity.ps1 -Action Test
.\Tools\Unity.ps1 -Action Build
.\Tools\Unity.ps1 -Action Release -ReleaseVersion '0.2.0'
.\Tools\Unity.ps1 -Action Smoke
.\Tools\Unity.ps1 -Action Open
```

脚本按 `ProjectVersion.txt` 查找对应 Editor；自定义安装位置可以加 `-UnityPath`。`Import` 在 Editor 关闭本项目时以批处理模式导入、解析依赖并检查编译，日志写入 `Logs/import.log`。参数依据 [Unity 2022.3 命令行文档](https://docs.unity3d.com/2022.3/Documentation/Manual/EditorCommandLineArguments.html)。

GitHub Actions在main推送/PR时检查workflow和打包脚本；发布Release时运行Unity测试，构建Windows x64正式包，在Windows runner无图形验收后上传ZIP和SHA-256到该Release。支持手动构建快照，手动运行只产生Actions artifacts。Personal构建需UNITY_LICENSE、UNITY_EMAIL、UNITY_PASSWORD三个Secrets；本轮按用户要求先上传workflow，云端Unity构建待配置凭据后验证。完整操作见[发布说明](Docs/Development/05_CI与发布.md)。

当前主要通过 **Git + Git LFS** 同步到公开的 [AI1379/Crazy_Rubbish](https://github.com/AI1379/Crazy_Rubbish)，分支为 `main`。首次克隆需要安装 Git LFS：

```powershell
git lfs install
git clone https://github.com/AI1379/Crazy_Rubbish.git
cd Crazy_Rubbish
git lfs pull
```

图片、模型、音视频和字体按 `.gitattributes` 进入 LFS；代码、文档与 Unity 文本场景／配置／`.meta` 使用普通 Git。`.gitignore` 排除缓存、日志、测试结果、构建包及 `.plastic`。提交资源时同时提交 `.meta`，并在首次提交新类型的大文件前检查是否需要新增 LFS 规则。字体授权文件也随源码和构建保留。

**Plastic SCM／UVCS 暂时保留**：已有工作区 `Crazy_Rubbish-Crazy_Rubbish`、云仓库 `Crazy_Rubbish/Crazy_Rubbish`、分支 `/main` 和 Unity 集成包均保留。Unity 中可通过 `Window/Unity Version Control` 打开版本控制窗口，也可使用 Plastic 桌面客户端。Git Push 与 Plastic Checkin 是独立操作，提交一个系统不会自动同步另一个。

`ignore.conf` 继续排除 Plastic 的缓存、构建产物和 `.git`。源码范围为 Assets、Docs、Packages、ProjectSettings、Tools、README 与仓库配置。空资源目录用 `.gitkeep` 保持 Git 克隆后的目录与 `.meta` 一致。两个版本系统的本机元数据互相排除，不上传。
