# main 与 release/0.1.0 合并核对（2026-10-19）

## 范围与结论

核对基线为 `main` / `origin/main` 的 `9846652ee2e2bbe71502f3c209d4a029f82d168e`，及 `origin/release/0.1.0` 的 `346114a36f7d5252d81bb21134cfe79dca9a3b3a`。共同祖先为 `e07cf1be8682527b72287e5fb7c34473d40df198`（10月12日），main 独有5个提交，release独有1个大提交。两端直接比较1904个文件变化，345409行增加、635881行删除，其中大量为序列化场景与生成网格，不能按行数判断功能增减。

技术上可以合并，但需显式解决入口、共用资源与验收边界。此候选在 `codex/release-010-integration` 隔离工作树中准备，保留两条分支历史，未改主工作区、未推送、未创建线上Release。新版Plan作为默认入口，原main的Prototype作为可选择的旧版入口保留。没有把旧存档转换成新版，也没有声称两条实现线的独有交互已全部互相移植。

## 两条实现线

| 项目 | 原main | release的新默认玩法 | 候选处理 |
|---|---|---|---|
| 规则与目录 | 10月11日v1；197设计物品（运行时另有old_container、old_lamp两个兼容条目）、49设施、211配方、49科技 | S6食水v1.1；201物品、53设施、216配方、54科技 | 两份资源与实现独立保留；策划索引明确S6是新默认玩法的基线 |
| 核心 | `PrototypeSimulation`、`DesignCatalog`、`ItemId` | `PlanSimulation`、`PlanConfig`、字符串物品ID | 保留main全部核心文件；新增Plan文件没有同名核心类覆盖 |
| 默认场景 | Prototype根对象 | Plan根对象，旧对象被关闭 | 场景从main的已保存表现建立，加入Plan根对象并关闭旧根；不能同时推进两套模拟 |
| 地图 | 原main区域布局 | 250×250洞室、河流、水塘、三区 | 默认用Plan地形；旧版仍使用原布局 |
| 背包与拾取 | 格子、拆分拖拽、整堆拾取 | 列表式操作；E拾取一份 | 旧版操作保留在旧入口；这些差异没有伪装成已移植到Plan |
| 美术 | `Delivery_20261013`及main人物方向约定 | `Local`、洞室素材及另一套人物方向约定 | 原`M1Appearance.asset`保留；新增`PlanAppearance.asset`，分别使用各自人物银行和映射 |
| 存档 | `Saves`；模式2—6迁移 | `S6PlanSaves`；schema1及S6配置版本校验 | 目录、序列化与读取器分离；新标题提供“旧版游戏 / 存档”，也支持`-legacyGame` |
| 生产、三区与返程 | 原main的完整通用产线、生活和结局 | Plan的完整产线、采水种植、物流、三区与返程 | 各自测试和运行验收覆盖；不在两套规则间共享在途货物、预约或经济台账 |

## 策划数据的实际增量

对两份JSON按稳定ID逐项比较：S6没有删除任何原有物品、设施、配方或科技；211条旧配方内容保持一致。新增4种物品（滤料、合格原水、块茎种子、块茎）、4种设施（取水、净水、厨房、自动供水）、5条配方和5项科技。另修改food、water、industrial_water、sprouts的说明或属性，以及种植设施/科技和农业技术出售条目。食水消费频率、工资相关基线等既有生活数值仍保留；新增食物储备、主角计入生活人口等明确规则。可重现的详细键差异保存在`Artifacts/MergeReview/catalogue-diff.json`。

相同素材数据在Delivery与Local两个美术银行之间有71组相同PNG哈希。此次保留两个GUID空间以维护两边已保存资源引用；不在合并过程中进行跨银行去重。

## 冲突与修复

`git merge-tree --write-tree origin/main origin/release/0.1.0`实际产生19个冲突文件。

- `.gitattributes`：合并两侧Source输入快照的字节保留规则，以及main的Design JSON规则；LFS规则保留。
- `Resources.meta`：保留main既有目录GUID，两个资源子目录同时存在。
- `CrazyRubbish.Editor.asmdef`：保留两侧依赖并加入新版需要的`UnityEngine.UI`。
- `PrototypeHud.cs`与`PrototypeWorld.cs`：保留main的完整界面、背包、自由建设、扩区和动画实现。release对这些旧类的美术修饰由独立Plan表现实现，无需替换main的较新结构。
- `PrototypeAppearance.cs`：同时保留main的嵌套人物类型（up/down/left/right）与Plan的独立人物类型（down/up/left/right），保留原字段名以维护序列化引用。
- `M1Appearance.asset`与`Main.unity`：不选择整份release覆盖；保留main资产并由Unity重建包含两个入口的新场景。Plan安装器只修改独立的`PlanAppearance.asset`。
- `PrototypeProjectSetup.cs`和`PrototypePresentationEditor.cs`：保留main美术安装及旧场景构建，同时安装Plan；自动刷新和新版素材验证不再重写旧版美术。旧版美术场景验证改为查找未激活的旧对象。
- 字体：release字体1512个映射，main字体1463个映射；release缺少main的33个字符。使用项目原有Noto字体源生成并集，并改进字体工具，使重跑时保留现有字符映射，支持重复`--include-font`参数。候选1553个映射，覆盖两边已有字符。
- README、Docs索引和5份Design文档：新版采用release的S6基线，保留main的来源快照、开发文档及旧版说明；修正release遗留的“仍未推送”和“只有一区二区”表述。
- `Tools/Unity.ps1`：保留main的美术导入/验证/场景操作与测试筛选，同时保留Plan验证和SmokePlan；旧版运行测试显式使用隔离存档目录。
- `Tools/CI/SmokeWindows.ps1`：原自动合并会混用Plan和Prototype报告字段。候选默认只检查当前Plan发布标准，`-Legacy -Automation -FullGame`单独检查旧版教程、电动生产、维护和传送带，避免误验收或误失败。工作流默认运行Plan的完整验收。

Git自动合并并不能发现全部语义问题。额外处理了：main场景测试显式选择旧版根对象；旧版静态预览验证按main的渐进开局确认初始地面为空，不再沿用release历史Demo的立即堆满假设；Plan的EventSystem归属Plan根对象，切换旧版时不会留下第二个输入系统；新增真实Player检查从新标题点击旧版入口、唯一Canvas/EventSystem及旧版存读档。

## 验证

本轮使用工程要求的 **Unity 2022.3.62f3**。release原报告的2022.3.22f1验证不作为本轮测试替代品。

- Unity Import及编译：已通过。
- `ValidatePlan`：已通过；3区、201物品、53设施、216配方、54科技，地形版本6，491个地形分块。
- 两条来源快照逐字节核对：无变化；Assets重复GUID、缺失meta、未展开LFS指针：均为0。
- `GenerateDesignBaseline.py --check`：main基线输出一致。
- Unity完整EditMode：356/356通过；最终入口启用顺序与完整字表调整后，3项集成定向回归通过。
- 两套美术验证：原main 71张PNG/171个Sprite通过；Plan 74张PNG/234个Sprite通过。旧版静态预览945个SpriteRenderer、1199个按钮，符合渐进开局。
- PowerShell及打包脚本：26项检查通过；actionlint 1.7.12验证工作流通过。
- Windows正式构建：`0.1.0-merge.1`，非Development。最终构建的Plan无图形发布验收通过，1280×800与1920×1080图形运行验收均通过，并检查对应截图。
- 最终构建的旧版图形完整回归通过：教程、实物运输、建设、电网、电动产线、维护、传送带、通用加工面板、扩展配方、锅炉供料、两种蒸汽用水、两阶段取消爬升及实际文件保存/读取。报告包含3批电动加工、26件传送带到货、2种扩展配方实产、2个取消阶段、61游戏秒热力运行。
- 最终新标题→旧版入口验证通过：实际点击按钮进入，独立美术、唯一输入系统/Canvas及旧版磁盘存读档均通过。最终各组Player日志没有异常或重复EventSystem提示。
- 字体逐字表比较：main 1463与release 1512个原有映射均完整保留，候选字体1553个映射。

最终结构化结果为`Artifacts/MergeReview/verification-summary.json`，含各组实际JSON报告；完整规则报告为`TestResults/full-editmode.xml`，入口定向报告为`TestResults/merge-entry-editmode.xml`。

## 合并就绪状态

19个Git冲突均已解决，没有未合并索引项；main原有Core文件未作改写。两侧原始快照与资产GUID检查通过。候选具备并入当前main的条件；仍需按下述产品边界理解“双实现线兼容集成”。当前main及用户工作文件保持原样，候选保存在独立分支，未推送。

## 已知边界

1. 这是双实现线兼容集成。新版继续采用S6数值，旧版继续采用10月11日v1，不能把同名物品、资金或居民记录直接拼接。若要求所有main独有交互进入同一个Plan会话，需要另立移植任务和数据迁移设计。
2. 旧版入口只在新版尚未开始会话的标题页出现；返回新版重新启动程序。测试不写用户个人存档。
3. release已有的经济平衡限制仍在：从零到二区有脚本经营记录，三区及返程有功能回归，尚无从零到返程的整局经济验证。本轮合并测试不替代这一验证。
4. 云端Actions的Unity许可和跨平台构建仍取决于仓库环境；本轮证据来自本机62f3与Windows Player，不宣称已重新跑过云端发布。

## 复现

```powershell
.\Tools\CI\TestPipeline.ps1
python Tools/GenerateDesignBaseline.py --check
.\Tools\Unity.ps1 -Action ValidatePlan
.\Tools\Unity.ps1 -Action ValidateArt
.\Tools\Unity.ps1 -Action ArtValidate
.\Tools\Unity.ps1 -Action Test
.\Tools\Unity.ps1 -Action Release -ReleaseVersion '0.1.0-merge.1'
.\Tools\CI\SmokeWindows.ps1 -ExpectedVersion '0.1.0-merge.1' -TimeoutSeconds 240
.\Tools\CI\SmokeWindows.ps1 -Legacy -Automation -FullGame -ExpectedVersion '0.1.0-merge.1' -ResultsDirectory 'TestResults/legacy-ci-smoke' -TimeoutSeconds 240
.\Tools\CI\SmokeCompatibility.ps1 -WithGraphics
```

完整结果、日志和静态核对数据位于忽略目录`TestResults`、`Logs`和`Artifacts/MergeReview`；本文件记录可随源码审阅的摘要。
