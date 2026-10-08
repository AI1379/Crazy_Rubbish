# Crazy Rubbish · 涌现

垃圾回收与产线经营游戏的初始 Unity 项目。工作名称为 **Crazy Rubbish**，正式游戏名称待定。项目根目录就是本仓库目录。

## 打开项目

1. 使用 **Unity 2022.3.62f3**。
2. Unity Hub → Add / 添加磁盘上的项目 → 选择本目录。
3. 打开 `Assets/_Game/Scenes/Main.unity`，点击Play，运行M2起步原型。

现行策划为2026-10-08合并版。工程推进至起步切片：地面拾取→加权仓库→新版木工及碎屑→工作台/拆解→发电与燃料→认可招募→居民走到木工岗位→动态外销→到期账单与首次手动发薪。满地自然来料实际堆高；暂停/倍速统一计时。当前操作、临时简化和验证见[M2起步说明](Docs/Development/04_M2起步说明.md)，完整比赛范围见[合并策划](Docs/Design/00_合并策划案.md)。

WASD／方向键移动，E 拾取，Shift+E 整堆，Q 入库，F 开工／停工，空格暂停，1／2／4 改变速度。简单素材和复杂对象的白盒标注已加入。

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

四份输入原文保存于Docs/Source，S4为当前基线。旧延期范围与17天排期仅保留历史；新版3区域与恢复的经营系统已纳入开发计划。首版数值与未完成内容明确区分。

## 本地工具与仓库

```powershell
.\Tools\Unity.ps1 -Action Import
.\Tools\Unity.ps1 -Action Setup
.\Tools\Unity.ps1 -Action Test
.\Tools\Unity.ps1 -Action Build
.\Tools\Unity.ps1 -Action Smoke
.\Tools\Unity.ps1 -Action Open
```

脚本按 `ProjectVersion.txt` 查找对应 Editor；自定义安装位置可以加 `-UnityPath`。`Import` 在 Editor 关闭本项目时以批处理模式导入、解析依赖并检查编译，日志写入 `Logs/import.log`。参数依据 [Unity 2022.3 命令行文档](https://docs.unity3d.com/2022.3/Documentation/Manual/EditorCommandLineArguments.html)。

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
