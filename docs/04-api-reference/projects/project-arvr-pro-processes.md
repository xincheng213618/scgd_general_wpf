---
knowledge_id: "projects.arvr-pro-processes"
knowledge_type: "guide"
status: "current"
summary: "配置 ARVRPro 流程组、流程解析映射、实例 Recipe 与雷鸟切图，说明类型选择、结果快照、配置保存和有效迁移规则。"
aliases: ["ARVR 流程组", "流程解析映射", "ARVR Recipe", "实例 Recipe", "雷鸟切图", "PictureSwitchConfig", "ProcessWithRecipeBase", "ResultParserMetas", "ResultProcessResolver", "ProcessGroups.json", "MTFH07", "MTFV07", "MTFHV048", "W25", "导入旧版Recipe"]
code_paths: ["Projects/ProjectARVRPro/Process/", "Projects/ProjectARVRPro/Recipe/", "Projects/ProjectARVRPro/Services/PictureSwitchService.cs", "Projects/ProjectARVRPro/ARVRWindow.xaml.cs"]
test_paths: ["Test/ProjectARVRPro.Tests/ProcessManagerPersistenceTests.cs", "Test/ProjectARVRPro.Tests/EmbeddedRecipeConfigTests.cs", "Test/ProjectARVRPro.Tests/LegacyRecipeImporterTests.cs", "Test/ProjectARVRPro.Tests/ProcessStepProjectionTests.cs", "Test/ProjectARVRPro.Tests/MTF07DynamicResultBuilderTests.cs"]
related: ["projects.arvr-pro", "projects.arvr-pro-demura", "flow.templates", "ui.property-grid"]
---

# 配置 ARVRPro 流程、解析映射与 Recipe

ARVRPro 用流程组组织测试顺序，用处理类型解释 Engine 输出，用 Recipe 定义限值和修正。本页说明如何配置这些对象以及配置如何保存；项目装载、Socket 自动化和结果查询见 [ProjectARVRPro](./project-arvr-pro.md)。

## 区分流程与解析映射

打开 `ProcessManagerWindow` 后，先选择要维护的内容：

| 对象 / 页面 | 用途 | 执行关系 |
| --- | --- | --- |
| 流程组 / `ProcessGroup` | 保存某个产品或场景的有序步骤 | `ActiveGroup` 决定当前组；`ProcessMetas` 指向该组步骤 |
| 流程步骤 / `ProcessMeta` | 绑定 Flow 模板、处理类型、私有配置和可选切图 | RunAll 按顺序执行启用项 |
| 流程解析映射 / `ResultParserMetas` | 按 Flow 模板名配置处理类型与 Recipe | 提供解析查找，不额外加入 RunAll 的步骤序列 |

`FindProcessMetaForTemplate` 先在活动组查找，再查流程解析映射，模板名忽略大小写，取首个具有处理实例的匹配项。该查找不检查 `IsEnabled`；步骤是否执行与能否作为解析映射命中是不同判断。

运行中的结果优先使用该次启动选定的处理实例。历史记录优先根据保存的处理类型和配置快照恢复实例，不能假设修改今天的 Recipe 就会重新解释全部历史结果。

## 配置一个测试方案

操作前应已准备目标 Flow 模板、对应结果类型及设备配置；修改方案后不要立即在未获授权的设备上运行。

1. 在流程管理中选择或新增流程组，按产品或场景命名。
2. 新增步骤，选择 Flow 模板与处理类型。名称用于识别步骤，`FlowTemplate` 用于绑定引擎模板，两者各有用途。
3. 在 **Process** 中配置解析 Key、输出 Key 及该处理类型的行为参数，在 **Recipe** 中填写限值和修正系数。
4. 需要执行前切图时，在 **切图** 中启用并配置串口指令、期望返回值、超时和稳定时间。
5. 用启用框决定 RunAll 是否包含该步骤；通过上移、下移或拖动调整顺序。复制步骤或流程组后重新核对模板、Key 和切图指令。
6. 需要独立的模板解析规则时，切到 **流程解析映射** 添加映射，并编辑其自己的 Process/Recipe。不要用增加执行步骤的方式代替解析映射。
7. 检查保存错误提示和日志，导出配置留存，再在授权环境验证实际顺序、解析值和结果输出。

流程组、步骤和解析映射的编辑会触发持久化。Recipe 编辑器直接修改当前实例；关闭窗口不能作为撤销操作。

## 步骤字段

| 字段 | 含义 |
| --- | --- |
| `Name` | 步骤显示名称 |
| `FlowTemplate` | Engine Flow 模板名称 |
| `ProcessTypeFullName` | 处理类型完整名称，用于持久化和恢复 |
| `IsEnabled` | 是否进入启用步骤序列，默认 `true` |
| `ConfigJson` | 当前处理实例的行为配置与内嵌 Recipe |
| `PictureSwitchConfig` | 步骤启动前的切图配置 |

Flow 执行、项目解析和最终判定分别有状态。`IProcess.Execute` 返回的布尔值表示处理是否完成；测量项与聚合结果的 PASS/FAIL 仍由结果数据及 Recipe 决定，不能只看方法是否返回成功。

## Recipe 的范围与修正

内置的有 Recipe 处理类型通过 `ProcessWithRecipeBase<TConfig, TRecipeConfig>` 返回 `Config.RecipeConfig`。每个流程步骤和解析映射持有自己的配置实例；复制时通过配置序列化建立独立副本，同类型实例可使用不同限值。

`RecipeBase` 使用 `Min` / `Max` 表示限值，`Fix` / `B` 表示线性修正，`Apply(value) = value × Fix + B`。具体 Process 决定对哪些值应用修正和怎样聚合判定，不能把所有客户判定都推断为统一公式。

例如同一 `MTFH07Process` 用于两个画面时，为两个步骤分别设置输出 `Key` 与 Recipe；修改其中一个实例不应更改另一个实例的阈值。历史结果保存的配置快照也包括内嵌 Recipe。

共享 `ProcessBase<TConfig, TRecipeConfig>` 与根 `RecipeConfig` 容器仍是兼容接口；内置流程不使用这个共享基类。独立的 [ProjectLUX](./project-lux.md) 采用自身的 Recipe/Fix 管理器，不能套用本页的实例存储规则。

## 选择处理类型

类型由 `ProcessManager.LoadProcesses` 从已加载程序集发现，界面分类由 `ProcessTypeCatalog` 提供。下表用于定位处理实现，实际能否测量还取决于绑定的 Flow 输出、设备和 Recipe。

| 能力 | 类型 / 位置 | 选择要点 |
| --- | --- | --- |
| 固定白场 | `White255Process`，`W255/` | 白场亮色度、均匀性等结果 |
| 固定视场角 | `White51Process`，`W51/` | 读取 FOV 输出并写入 W51 结果 |
| 黑场与 FOFO | `BlackProcess`，`Black/` | 对比度计算依赖聚合结果中已有的 W255 白场数据 |
| 按 Key 亮色度 | `LuminanceChromaticityProcess`，`KeyedResults/LuminanceChromaticity/` | 独立输出 Key、中心点 Key；`Key=White` 同时写入 W255 兼容结果 |
| YW 亮色度 | `LuminanceChromaticityYWProcess`，同上 | 12×7 与 8×7 两组 POI、均匀性及独立 Recipe；同批次同点数候选重复时取结果主表 Id 最大的后生成结果 |
| 按 Key 视场角 | `FieldOfViewProcess`，`KeyedResults/FieldOfView/` | 水平、垂直、对角 FOV；`Key=White` 同时写入 W51 兼容结果 |
| 棋盘格 | `ChessboardProcess` / `ChessboardDynamicProcess` | 固定或动态点位的棋盘格对比度 |
| 畸变 | `DistortionProcess` / `DistortionDynamicProcess` | 固定或动态点位的几何结果 |
| 光学中心 | `OpticCenterProcess` / `OpticCenterDynamicProcess` | 固定或动态点位的中心结果 |
| 动态 POI | `PoiDynamicProcess`，`POI/` | 运行时点位的亮色度解析与显示 |
| 通用 MTF | `MTFProcess`，`MTF/` | 通用 MTF 结果解析 |
| HV 特殊图案 | `MTFHVProcess` / `MTFHV048Process` / `MTFHV058Process` | 分别对应 0368、048、058 点位方案 |
| 动态 HV | `MTFHVDynamicProcess` | HV 特殊图案的动态区域 |
| 058 条纹 | `MTFHProcess` / `MTFVProcess` | 横条纹或竖条纹的 058 方案 |
| 07 条纹 | `MTFH07Process` / `MTFV07Process` | 中心 0F 和四角 0.7F，共五个点位 |
| 缺陷与 AOI | `DetectScreenDefectsProcess` / `AOIProcess` | 屏幕缺陷或 OLED AOI 结果 |
| Demura | `DemuraProcess` / `DemuraAoiProcess` | 补偿准备与[烧录](./project-arvr-pro-demura.md)，或补偿后的 AOI 判定 |
| 空处理 | `BlankProcess` | 无业务解析的回退处理，不作为普通处理类型选项展示 |

### 解析 Key 与输出 Key

解析 Key 用于匹配 Engine 已产生的结果名称；输出 Key 用于区分项目聚合结果中的不同画面，不能互相替代。

- `LuminanceChromaticityProcessConfig` 默认 `Key=White`、`CenterKey=P_5`。若现场 W25 模板以 `P_9` 为中心，可设置 `Key=W25`、`CenterKey=P_9`；必须以实际 Flow 点位为准。
- MTF 0368 使用 0F/0.3F/0.6F/0.8F，048 使用 0F/0.4F/0.8F，058 使用 0F/0.5F/0.8F。048 的第二组是 0.4F。
- H 表示横条纹，V 表示竖条纹；HV 是独立特殊图案。07 的每个点都有解析 Key 与 Recipe，结果按画面 Key 写入 `MTFH07TestResults` 或 `MTFV07TestResults`。
- 同一结果字典中的重复输出 Key 会写到同一项。不同画面需要分别留存时，应配置不同输出 Key。

## 雷鸟切图

| 配置项 | 默认值 | 含义 |
| --- | --- | --- |
| `IsEnabled` | `false` | 是否在步骤启动前切图 |
| `Mode` | `Thunderbird` | 当前支持雷鸟串口 |
| `SendCommand` | `PIC1` | 发送指令，可按画面选择预设 |
| `ExpectedResponse` | `succeed` | 期望返回值 |
| `TimeoutMs` | `1000` | 指令等待毫秒数，属性至少为 1 |
| `SuccessDelayMs` | `500` | 成功后的稳定等待毫秒数，属性至少为 0 |

`PictureSwitchService` 在执行 Flow 前运行。串口未连接时，仅在全局 `ThunderbirdAutoConnect` 开启且串口号已配置时尝试连接。切图失败会记录 `PictureSwitchFailed`，不会启动该步 Flow；RunAll 根据 `AllowTestFailures` 决定继续下一步或结束。

## 保存、导入与恢复

默认文件为 `%APPDATA%\ColorVision\Config\ProcessGroups.json`，路径由 `ViewResultManager.DirectoryPath` 提供。版本 3 格式一起保存活动组序号、流程组、独立解析映射及兼容 Recipe 容器；每项 `ConfigJson` 保存自身配置和 Recipe。

写入先生成同目录临时文件，刷新后替换正式文件；目标已存在时保存前一版为 `.bak`。普通保存失败会记录 `保存ProcessGroups失败`；Recipe 编辑还会提示“Recipe 已修改，但保存 ProcessGroups.json 失败”。这时内存值可能已改变，磁盘仍是旧内容，应处理路径、权限或空间问题后重试，不能仅看界面值判断保存成功。

| 入口 / 来源 | 当前行为 |
| --- | --- |
| 导出配置 | 生成 `.arvrprocess.json`，包含流程组和解析配置，供对应项目导入 |
| 导入配置 | 读取并校验配置后应用；持久化失败时保留原内存和磁盘配置，见 `ProcessManagerPersistenceTests` |
| 导入旧版Recipe | 将匹配类型的限值复制到各流程组和解析映射实例，不能理解为建立共享引用 |
| 启动加载 | 优先读取 `ProcessGroups.json`；仅当它不存在时才尝试从 `ProcessMetas.json` 迁移到 Default 组 |
| 低于版本 3 的组配置 | 从各组按模板名补建解析映射，跳过空处理及已有映射，并保存迁移结果 |

新格式文件损坏时不会自动回退旧 `ProcessMetas.json` 或 `.bak`。没有有效流程组时可能出现 Default 空组；应先检查 `加载ProcessGroups失败` 日志和原文件，再决定如何恢复，不能把空组视为配置从未存在。

历史解析由 `ResultProcessResolver` 优先使用记录中的类型完整名和 `ProcessConfigJson`；找不到完整名时仅接受唯一的同类名类型。配置快照无法恢复时会记录警告并使用默认配置，解析类型不可用时才回退到模板映射。因此“历史记录能打开”不等于使用了原 Recipe，应连同警告日志核对。

## 验证入口

- `ProcessManagerPersistenceTests`：独立复制、顺序、保存重载、解析映射及导入失败保护。
- `EmbeddedRecipeConfigTests`：实例 Recipe、空值兼容、配置快照、原子替换及备份。
- `LegacyRecipeImporterTests`：旧 Recipe 导入；`ProcessStepProjectionTests`：启用步骤投影；`MTF07DynamicResultBuilderTests`：07 结果构建。

这些测试位于 `Test/ProjectARVRPro.Tests/`。串口切图、Flow 实际输出和客户阈值仍需在授权环境验证；测试入口的存在不代表该方案已通过现场验收。
