---
knowledge_id: "projects.arvr-pro-processes"
knowledge_type: "guide"
status: "current"
summary: "配置 ARVRPro 流程组、流程解析映射、实例 Recipe、外部相机参数与雷鸟切图，说明运行时覆盖、结果快照、配置保存和有效迁移规则。"
aliases: ["ARVR 流程组", "流程解析映射", "ARVR Recipe", "实例 Recipe", "外部相机参数", "相机参数覆盖", "FlowCameraParameterOverrideConfig", "ReadCanvasSnapshot", "雷鸟切图", "PictureSwitchConfig", "ProcessWithRecipeBase", "ResultParserMetas", "ResultProcessResolver", "ProjectARVRProProcessGroups.json", "ProcessGroups.json", "MTFH07", "MTFV07", "MTFHV048", "VR MTF 分区", "VRMtfPartitionProcess", "VRMTFH", "VRMTFV", "W25", "鬼影解析", "RingGhostProcess", "RingGhostTestResults", "GMR1", "GMR2"]
code_paths: ["Projects/ProjectARVRPro/Process/", "Projects/ProjectARVRPro/Recipe/", "Projects/ProjectARVRPro/Services/PictureSwitchService.cs", "Projects/ProjectARVRPro/ARVRWindow.xaml.cs", "Engine/ST.Library.UI/NodeEditor/STNodeCanvasSnapshot.cs"]
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
| 其他配置 | 设置 LUX 设备号、VID 相机、测量方式、限值和 K/B | 修改后自动保存到原项目配置；这些参数不加入 RunAll 步骤 |
| 流程解析映射 / `ResultParserMetas` | 按 Flow 模板名配置处理类型与 Recipe | 提供解析查找，不额外加入 RunAll 的步骤序列 |

`FindProcessMetaForTemplate` 先在活动组查找，再查流程解析映射，模板名忽略大小写，取首个具有处理实例的匹配项。该查找不检查 `IsEnabled`；步骤是否执行与能否作为解析映射命中是不同判断。

运行中的结果优先使用该次启动选定的处理实例。历史记录的解析、判定和导出优先根据保存的处理类型和配置快照恢复实例，不能假设修改今天的 Recipe 就会重新解释全部历史结果；结果图片回看在当前流程仍映射到相同处理类型时，使用当前实例的显示配置，使绘制开关和标签模板可以立即调整。

流程和批次重放通过 `IProcessExecutionContext` 使用该批次的数据库读取采集图、算法主记录及明细。屏幕缺陷、POI、FOV、MTF、畸变和 Demura 等解析沿用此连接。切换数据库后，同号的图像或明细不会补入正在处理的批次；原库数据缺失或读取失败时按该解析器的缺测规则处理。独立调用无参上下文或 `DemuraAoiParser.ParseAsync(int, …)` 仍使用已配置的连接。

## 配置一个测试方案

操作前应已准备目标 Flow 模板、对应结果类型及设备配置；修改方案后不要立即在未获授权的设备上运行。

1. 在流程管理中选择或新增流程组，按产品或场景命名。
2. 新增流程项时先选择 Flow 模板。名称默认沿用模板名，在当前流程组内遇到重名时依次尝试 `（1）`、`（2）` 等后缀，比较时忽略大小写。可手动改名，切换模板时保留手填名称。确认名称后点击 **下一步** 选择处理类型。
3. 需要让操作员在 Flow 外调整单相机曝光或校正模板时，在流程模板下勾选 **覆盖相机参数**。界面会从当前模板同步节点值，再显示可编辑项。
4. 在 **Process** 中配置解析 Key、输出 Key、结果图显示内容及该处理类型的行为参数，在 **Recipe** 中填写限值和修正系数。
5. 需要执行前切图时，在 **切图** 中启用并配置串口指令、期望返回值、超时和稳定时间。全局串口参数与手动调试从顶部 **雷鸟连接与切图** 进入；**保存连接设置** 只保存串口、波特率、超时和切图时自动连接选项，不连接设备、不发送指令。实际连接和手动切图仍需分别操作，并确认设备授权。
6. 用启用框决定 RunAll 是否包含该步骤。上移、下移和拖动均需确认保存，保存成功后应用新顺序。复制步骤或流程组后重新核对模板、外部相机值、Key 和切图指令。
7. 需要独立的模板解析规则时，切到 **流程解析映射** 添加映射，并编辑其自己的 Process/Recipe。不要用增加执行步骤的方式代替解析映射。
8. 检查保存错误提示和日志，导出配置留存，再在授权环境验证实际顺序、解析值和结果输出。

流程组、步骤和解析映射的编辑会触发持久化。Recipe 编辑器直接修改当前实例；关闭窗口不能作为撤销操作。排查配置写入时间时，在反馈的应用日志中搜索“已保存 ARVRPro 流程配置”。

流程模板按名称关联，忽略大小写。模板改名或删除后，原配置名称会保留；配置列表中的模板名显示为红色并带警告图标，右侧说明缺失的模板及修改入口。在流程步骤中点击 **重新选择流程模板**，或在解析映射中点击 **编辑映射**，选择实际模板后提示解除。编辑器不会用列表第一项替代找不到的旧模板；仅修改处理类型时保留原模板名。配置窗口打开期间，模板增删或改名会刷新提示。运行时遇到缺失模板会终止本次测试，报错直接指出流程项、模板名和重新配置的位置。

## 步骤字段

| 字段 | 含义 |
| --- | --- |
| `Name` | 步骤显示名称 |
| `FlowTemplate` | Engine Flow 模板名称 |
| `ProcessTypeFullName` | 处理类型完整名称，用于持久化和恢复 |
| `IsEnabled` | 是否进入启用步骤序列，默认 `true` |
| `SocketCode` | `Text` 模式下 LUX 命令 `T00XX` 的末两位，默认空；独立于步骤顺序，`00`、`01`、`31` 为专用命令 |
| `ConfigJson` | 当前处理实例的行为配置与内嵌 Recipe |
| `PictureSwitchConfig` | 步骤启动前的切图配置 |
| `FlowCameraParameterOverrideConfig` | 该步骤运行时使用的外部曝光和校正模板覆盖，默认关闭 |

Flow 执行、项目解析和最终判定分别有状态。`IProcess.Execute` 返回的布尔值表示处理是否完成；测量项与聚合结果的 PASS/FAIL 仍由结果数据及 Recipe 决定，不能只看方法是否返回成功。

处理配置的 **输出Key** 用于结果字典、CSV `Test_Screen`、批次指标及报表分组，部分类型将它保存为 `Name`。普通光学中心与“光学中心（回包）”使用 `Key`，缺省或留空时为 `Optical_Center`。Key 随结果快照保存，三轴回包的数值和设备前缀独立于该字段。

## 外部相机参数覆盖

**覆盖相机参数** 位于流程模板下方。勾选时，界面加载当前模板，从默认 Start 的执行链中找到唯一的 `LVCameraNode` 或 `LocalCameraNode`，把节点的曝光与校正模板同步到步骤配置，再显示节点同款的数值编辑器和校正模板选择器。取消勾选会隐藏编辑项，并恢复为关闭、曝光 `100 ms`、空模板；切换流程模板也会关闭旧覆盖，防止参数与新模板错配。仅切换流程步骤不会重新覆盖已经保存的外部值。

单流程与 RunAll 都在已加载的执行链上查找相机。同一流程只支持从默认 Start 可达的唯一 `LVCameraNode` 或 `LocalCameraNode`，孤立相机和其他相机节点不参与。

关闭开关时不扫描节点。开启后如找不到执行入口或目标、存在多台受支持的取图节点、曝光值无效，或者本地相机节点找不到所填校正模板，只记录跳过原因，原 Flow 继续执行，不把这类条件当成检测失败，也不发生部分覆盖。L/BV 节点沿用既有语义，将模板名交给远端相机服务确认；本机模板列表不能作为远端能力的硬门禁。多相机的目标选择与逐节点参数留待后续扩展。

覆盖只应用于本次运行，模板不保存。校正模板按目标 `DeviceCode` 选择，留空表示本次不校正。L/BV 的模板名由远端服务解释，本地节点在执行前校验归属。

勾选时只读取模板的画布快照，无设备或 MQTT 命令。预计时间缓存区分外部相机参数，实际耗时仍取决于曝光与校正。

从流程下拉框手动执行时，只有该模板唯一对应一个处理项才使用其外部相机参数；同一模板对应多个处理项时保持原流程参数。批量执行始终使用当前处理项的配置。

## Recipe 的范围与修正

内置的有 Recipe 处理类型通过 `ProcessWithRecipeBase<TConfig, TRecipeConfig>` 返回 `Config.RecipeConfig`。每个流程步骤和解析映射持有自己的配置实例；复制时通过配置序列化建立独立副本，同类型实例可使用不同限值。

`RecipeBase` 使用 `Min` / `Max` 表示限值，`Fix` / `B` 表示线性修正，`Apply(value) = value × Fix + B`。具体 Process 决定对哪些值应用修正和怎样聚合判定，不能把所有客户判定都推断为统一公式。

Recipe 按 `[下限] ≤ x ≤ [上限]` 判定修正值 `x`，某侧限值为 `0` 表示该侧不限。

MTF 处理在应用点位或分区的 Recipe 修正后检查数值。上游快照明确记录归一化比例或百分数时，范围分别为 0～1 或 0～100。旧服务缺少明确输出尺度时只检查修正值是否有限。越界会使该次解析失败，日志记录点位、原值、修正值及 K/B。

单次测试和一键执行使用同一入口生成 `Code`：按 `CodeUseSN` 拼接产品 SN 与 `CodeDateFormat` 时间部分后，规范为 Windows 文件名片段，再创建 MySQL 批次、诊断记录及启动 Flow。预处理、算法服务、结果解析和完成回调都使用此 `Code`；`SN` 保留原值用于显示和 Socket 回传。汇总 CSV 文件名也单独规范化，文件内容继续保留原始 SN。不要在 Socket 接收时把原始产品 SN 全局替换。

W255 与按 Key 亮色度共用均匀性来源规则。**从修正后POI重算均匀性** 默认关闭：优先按配置的 TName 读取已有 `PoiAnalysis` 亮度/色度均匀性，缺少的项目从当前 POI 的原始 Y、u′、v′ 补算，不覆盖已读取的另一项；开启时忽略模板均匀性，使用已应用 Recipe 修正的 POI 重算两项。亮度均匀性为最小 Y / 最大 Y，色度均匀性为所有点对的最大 Δu′v′；两种来源都继续应用均匀性项目自己的 Recipe K/B 与限值，参与 PASS/FAIL。缺少 POI 分析节点不需要手动打开重算开关；需要本地计算时点数不足或数值无效则解析失败，不保留默认零值作为有效结果。YW 亮色度始终按两组 POI 本地计算。

固定和动态棋盘格优先使用匹配的 `PoiAnalysis.result.Value`，仅在找不到匹配模板时按当前 POI 本地计算对比度。本地计算要求每个点有有限实测亮度 `Y`，模板结果要求有限实测 `Value`。明确记录的零值可以参与计算，两条路径均先检查实测数据，再应用 Recipe K/B 和限值。模板值有效且辅助 POI 不完整时，判定继续使用模板值，辅助 CSV 只保留有效点位。

W255 和按 Key 亮色度的 **显示配置** 提供独立的 **亮度均匀性格式**、**色度均匀性格式**，可选择百分比或小数；默认分别为百分比、小数，旧配置省略这两个字段时保持原显示。百分比将 `TestValue` 乘 100 并使用 `%` 单位，小数使用比例值且单位为空；新结果的 `TestValue` 只含数字，单位单独存入 `Unit`，W255 读取历史带 `%` 的数值文字时只显示一次单位。`Value`、Recipe K/B、限值与判定仍保持比例数值，导出与 Socket 的格式化文本和单位随结果保存，不重写历史结果。

例如同一 `MTFH07Process` 用于两个画面时，为两个步骤分别设置输出 `Key` 与 Recipe；修改其中一个实例不应更改另一个实例的阈值。历史结果保存的配置快照也包括内嵌 Recipe。

共享 `ProcessBase<TConfig, TRecipeConfig>` 与根 `RecipeConfig` 容器仍是兼容接口；内置流程不使用这个共享基类。

## 结果图显示配置

结果图的内容选择属于每个处理实例，保存在该步骤或解析映射的 `ConfigJson` 中，不读取 CVCIE 全局显示配置。回看历史结果时，若当前流程仍映射到相同处理类型，图层绘制读取当前实例的显示配置；历史解析、判定和导出仍读取执行快照。`BlackProcess`、`PoiDynamicProcess`、`White255Process`、按 Key 亮色度、YW 亮色度和两种棋盘格处理在 **Process > 显示配置 > 显示内容** 中复用 CVCIE 字段选择器，可选 XYZ、xy、u′v′、CCT、主波长及各自小数位。空模板表示不绘制详细数据，不改变点位名称的全局开关。

旧配置没有 `DisplayTemplate` 时，普通亮色度关注点保留全部 CIE 字段和原小数位，YW 与棋盘格只显示 `Y` 两位小数。MTF、畸变和缺陷图层各自定义显示内容。

在 **ProcessManager → 流程解析映射 → Process → 显示配置 → 图像文字颜色** 可为对应的 MTF-HV 058 实例选择数值文字颜色。重新打开结果图时应用所选颜色，适用于重新绘制的图层。已保存的标记图包含绘制后的像素。

`White51Process` 按结果类型读取 FOV 和四个发光区角点。固定 W51 和按 Key 视场角均要求结果文件包含有限的 `clolorVisionH_Fov`、`clolorVisionV_Fov`、`D_Fov`。实测零值参与正常判定，缺测时判解析失败并清除该输出。**绘制FOV** 默认开启，使用历史角度和角点绘制四边形、中轴与对角线。关闭时只绘制发光区边界。

普通和动态畸变默认绘制点阵连线。仅在所选九点完整、有限、互异且顺序有效时连线，包括只有坐标的旧结果。多点阵可选 `Point9` 代表点。关闭辅助连线或点序无效时保留原点标记。四角边框只是实测点连线。

普通与动态畸变从本地点阵 V2 的 `Analysis.Geometry` 读取 `MaximumTiltDegrees`（最大倾斜角，°）和 `MaximumEdgeLengthDifferencePercent`（最大边长差比例，%）。角度包含整体旋转，边长差使用两对边平均长度，与 P9 输出和梯形项修正无关。**Recipe > 四角几何** 对两项分别应用 K/B，默认上下限均为 `0`。显示使用 `ShowConfig`，单位固定为 ° 和 %。结果保存在普通结果对象或动态 `Items`，用于详情、文本、标准 CSV、批次指标和嵌套项目 JSON。字段缺失时省略对应测试项，原始值非有限、为负或倾斜角大于 90° 时解析失败。Legacy 固定字段维持原格式。

十字 RGB 分离使用独立的 `RgbCrossProcess`，与原十字定位/光学中心放在同一个“光学与几何”分类。执行时按当前批次读取唯一 `FindCross=63`、`version=2.0` 主记录，再由公共明细的 `ResultFileName` 找到 JSON，原图取该记录的 `ImgFile`。没有手填 JSON、模板名称筛选或原图覆盖入口。零条、多条、失败状态或不完整明细均报错，不扫描目录或回退到旧批次；原单十字解析跳过 2.0 九点记录。

Process 的“输出Key”默认 `RgbCross`，用于 CSV 分组和 `ObjectiveTestResult.DynamicRgbCrossResults` 字典的 Key。不同画面配置不同 Key，可并存十字测量、几何与判定状态；相同 Key 本次执行替换对应项，失败时写入 DATA_ERROR，避免残留旧成功结果。输出 Key 存入当前结果快照，修改配置不重命名历史导出。使用单独类型的字典保留空测量和 MEASURED/INVALID 状态，不把未判定数据压成普通布尔 PASS。

通用 artifact JSON 的算法 ID 为 `colorvision.display.rgb-cross-registration`。算法版本 `1.1.0` / `1.2.0` / `1.3.0` 读取 `RGB-cross-separation` 与 `rgb-cross-regions`，`1.4.0` / `1.5.0` / `1.6.0` / `1.6.2` 读取结构化测量并核对算法版本与执行标识。其他算法版本及现场验证脚本的 `measurements.json` 不接受。

当前对接 schema `1.2.0` 见十字 RGB 的导出契约。项目继续接受 3×3 `1.0.0` 和可变布局 `1.1.0`，旧结果从通道几何推导 R-G、B-G。结果快照固定布局，判定按配置点数核对完整性。`capabilityProfile` 仅标识测量能力。 [十字 RGB 导出契约](../algorithms/detectors/rgb-cross.md)

独立实现方也可使用精简 JSON：根字段 `width/height/points`，默认九点 `id=1..9`，其他布局需增加 `grid: {rows, columns}` 并连续编号到全部点数，各点 `R/G/B` 通道含 `horizontal/vertical` 的 `[x,y,width,height]` 矩形，`separation` 为最大对应边缘差。无效通道为 `null`，点位分离为 `null` 且必须给出 `reason`。此格式不要求提供宿主项目、数据库或算法实现元数据；解析后沿用相同几何校验、判定和渲染，保留原格式兼容。

JSON 文件限制为 32 MiB；解析检查点位数量与配置布局、编号、行列、有效性、有限数值、RGB 几何及分离值一致性。v1 还检查原图尺寸、搜索区和汇总计数。有效几何均为原图像素坐标，绘制每通道横臂和竖臂框，可分别关闭 RGB 边缘或点号数值；原图尺寸不匹配或坐标越界时跳过叠图。v1 没有指定原图时可按保存的尺寸用空白底图显示，旧通用 JSON 不含尺寸时数据库记录必须关联原图。原图由同批次数据库记录绑定；若 JSON 有 SHA-256，执行解析时校验原图文件摘要。

**Recipe > 在本项目判定** 默认关闭，运行消息为 `Completed`，结果文本显示“完成”；快照保留 `MEASURED`/`INVALID` 测量有效性；此设置是业务配置，不是对外授权开关。开启时使用与其他项目相同的 `RecipeBase` 编辑器：下限、上限、K、B。各点的 R-G、B-G 两个最大绝对边缘偏移分别按 `原值 × K + B` 后调用 `ObjectiveTestItem` 的上下限规则；某侧限值为 0 表示该侧不限，边界包含等于。上下限与 K/B 必须有限，两侧都非零时下限不得高于上限。配置的全部点位均有效且修正值满足范围才为 `PASS`，超限为 `FAIL`，存在无效点为 `INVALID`。解析失败为 `DATA_ERROR`。旧单上限配置不迁移，使用者重新配置 `EdgeSeparation`。R-G、B-G 原始值与修正值分别保存在 `Comparisons` 字典的 `Value` / `JudgedValue`，旧三通道极差保留在 `MaximumEdgeSeparation`，不得修改输入 JSON 或通道几何。关闭判定不改变已有总结果，包含部分无效测量也完成执行并保留空值；开启判定时无效点才导致项目判定失败。文件/解析错误始终失败，不把错误显示成完成，不用零代替缺失值。CSV 每点输出 `Pn_R-G`、`Pn_B-G` 两行；动态指标导出使用同样的两组键，并保留各自状态。

标签显示点号和各通道对的 px 数值，缺失用 `—`。开启项目判定后，右侧结果区才显示逐点判定。

历史 `ViewResultJson` 保存来源主记录 ID、原测量、JSON 摘要、原图绑定、本次使用的 Recipe 副本、修正值和逐点状态，回看/导出读取快照。当前显示开关可控制叠图，修改 Recipe 不会静默重新判定历史数据。 现场验收需核对实际数据库、原图身份与客户许可。

## 选择处理类型

仅需执行 Flow、无需业务指标解析时，选择 **空流程 → 空流程**。它仍执行绑定的模板，正常完成后直接通过，不添加业务指标或 CSV 项，无需 Process/Recipe 配置。

**ARVR → 缺陷检测 → 圆环鬼影** 使用 `RingGhostProcess`，对应 Flow 的 [圆环鬼影节点](../algorithms/detectors/display-metrology.md)。流程可配置为开始 → 取图/打开线性 RAW 或 TIFF → 圆环鬼影 → 结束，再为该 Flow 选择“圆环鬼影”解析。当前节点不以 CVCIE 主缓冲计算亮度，不应接到把主缓冲切换为 CIE 的校正步骤之后。每个解析步骤必须对应当前批次唯一、成功的 `Ghost` / `ring-ghost/1.0` 结果；零条、多条、失败或明细/文件不完整均报错，不回退旧批次或远端 Ghost2.0。

对外测量项只有 `GMR1`、`GMR2`，单位 `%`；正常未检出按 0 输出，定位、饱和、分级或数据错误保持空项并置 `DATA_ERROR`，同时清除该输出 Key 的旧成功值。节点当前采用“一阶必有，单环归一阶”的方案约定，默认外侧一阶、内侧二阶，仍需客户核对光路。候选、背景、径向曲线和测量区域属于内部结果，不增加客户测量参数。输出 Key 默认 `RingGhost`，不同画面可配置不同 Key，项目 JSON 写入 `ObjectiveTestResult.RingGhostTestResults[Key]` 的两个 `ObjectiveTestItem`，标准 CSV、批次指标和自定义值查找也只导出这两项。

每个实例独立配置 GMR1/GMR2 Recipe，默认上限分别为 1%、0.5%，等于上限时通过；零限值仍表示该侧不限。非零测量沿用 K/B 修正，正常未检出的 0 保持 0。`Execute` 完成与配方 PASS/FAIL 分开，超限可完成解析但总结果为失败。历史记录保存原始测量、源主记录、已应用限值和修正值，导出按快照恢复；修改当前 Recipe 不重新判定历史。绘制测量区域默认开启，只重放保存的原图几何，不重新运行算法，图像尺寸不符时跳过叠图。 现场验收需核对 MySQL、测量硬件与瑞淀对照输出。

类型由 `ProcessManager.LoadProcesses` 从已加载程序集发现，界面分类由 `ProcessTypeCatalog` 提供。下表用于定位处理实现，实际能否测量还取决于绑定的 Flow 输出、设备和 Recipe。

| 能力 | 类型 / 位置 | 选择要点 |
| --- | --- | --- |
| 固定白场 | `White255Process`，`W255/` | 白场亮色度、均匀性等结果 |
| 固定视场角 | `White51Process`，`W51/` | 读取 FOV 输出并写入 W51 结果 |
| 黑场与 FOFO | `BlackProcess`，`Black/` | 已有 W255 时，普通 FOFO 使用白场中心亮度和 `Key_Center` 指定的黑场中心亮度。缺少该黑场实测点时失败。`IsUsingNing` 使用全点亮度之和 |
| 按 Key 亮色度 | `LuminanceChromaticityProcess`，`KeyedResults/LuminanceChromaticity/` | 独立输出 Key、中心点 Key；`Key=White` 同时写入 W255 兼容结果 |
| YW 亮色度 | `LuminanceChromaticityYWProcess`，同上 | 12×7 与 8×7 两组 POI、均匀性及独立 Recipe；同批次同点数候选重复时取结果主表 Id 最大的后生成结果 |
| 按 Key 视场角 | `FieldOfViewProcess`，`KeyedResults/FieldOfView/` | 水平、垂直、对角 FOV；`Key=White` 同时写入 W51 兼容结果 |
| 棋盘格 | `ChessboardProcess` / `ChessboardDynamicProcess` | 固定或动态点位的棋盘格对比度 |
| 畸变 | `DistortionProcess` / `DistortionDynamicProcess` | 至少需要一个有限实测指标。TV 比值按水平和垂直成对读取。旧点数和几何格式中缺省的可选指标不生成测试项，实测零值保留 |
| 光学中心 | `OpticCenterProcess` / `OpticCenterDynamicProcess` | 固定或动态点位的中心结果 |
| 光学中心（回包） | `OpticCenterLuxProcess`，`OpticCenter/` | 继承普通光学中心；选择此类型即通过回包接口返回本次三轴值，不限定设备号或流程命令，切换类型后核对 Recipe |
| 十字 RGB 分离 | `RgbCrossProcess`，`OpticCenter/` | 与原十字/光学中心同属“光学与几何”；读取 JSON、绘制 RGB 边缘，可选在本项目判定 |
| 圆环鬼影 | `RingGhostProcess`，`Ghost/` | 在“缺陷检测”分类读取本地圆环结果，对外输出 GMR1/GMR2，正常未检出为 0 |
| 动态 POI | `PoiDynamicProcess`，`POI/` | 运行时点位的亮色度解析与显示 |
| 通用 MTF | `MTFProcess`，`MTF/` | 判定 MTF 2.0 的 `result[].mtfValue`，至少需要一个实测点。缺测或非有限值使步骤失败并清除该输出 Key。实测零值按配方判断 |
| VR MTF 分区 | `VRMtfPartitionProcess`，`MTF/VRPartition/` | H/V 实例分别配置网格、圆环及输出 Key，各区 Min/Max/Average 使用独立 Recipe |
| HV 特殊图案 | `MTFHVProcess` / `MTFHV048Process` / `MTFHV058Process` | 分别对应 0368、048、058 点位方案 |
| 动态 HV | `MTFHVDynamicProcess` | 至少需要一个命名的 `resultChild` 分组，每组须有有限的 `horizontalAverage` 和 `verticalAverage`。H/V 各按配方判定，实测零值有效。缺测或无效数据清除该输出 Key 并判失败 |
| 058 条纹 | `MTFHProcess` / `MTFVProcess` | 横条纹或竖条纹的 058 方案 |
| 07 条纹 | `MTFH07Process` / `MTFV07Process` | 中心 0F 和四角 0.7F，共五个点位 |
| 屏幕缺陷 | `DetectScreenDefectsProcess` | 读取批次最新匹配的检测主记录及最新明细。内联或文件结果须含非负整数 `DefectCount`。实测为零且无缺陷框时通过，正计数判失败且缺陷框可省略。结果缺失、损坏或计数为零但存在缺陷框时，解析失败并清除当前输出 |
| AOI | `AOIProcess` | OLED AOI 结果 |
| Demura | `DemuraProcess` / `DemuraAoiProcess` | 补偿准备与[烧录](./project-arvr-pro-demura.md)，或补偿后的 AOI 判定 |
| 空处理 | `BlankProcess` | 无业务解析的回退处理，不作为普通处理类型选项展示 |

### VR MTF 分区

在 MTF 分类选择 **VR MTF 分区**（`VRMtfPartitionProcess`），读取 MTF 2.0 结果并按自然点名顺序逐行铺成正方形网格，再按到中心的距离划分互斥的 A/B/C/D 圆环。每区输出 Min、Max、Average，保留原测量点；边界点归入半径较小的一圈，最外圈之外的点不参与区域汇总。结果图按 A灰/B绿/C蓝/D黄/圈外红着色；区域归属随结果保存，后来修改半径不会重新划分历史图。

H/V 使用两个独立实例和不同输出 Key，如 `VRMTFH`、`VRMTFV`。迁入旧方案时，两者均为 30×30、中心 14.5；H 的四个半径为 1/4/9/15，V 为 1/2/4/7.5。批次有多份 MTF 结果时，按实际结果名筛选为唯一候选；旧 LUX 配置不自动导入。原数值保持 MTF 输出尺度，不乘 100；旧 Fix 字段原本未参与区域计算，不作为已生效修正迁入，新实例按 Pro 的每项 Recipe 配置 K/B 和上下限。

区域测量使用实例 Recipe 判定，并并入当前流程总结果；这与旧 LUX 只写区域项限值、未合并总判定的行为不同。网格点数不符、非有限数值、无效分区参数或空区域会使解析失败，并清除该输出 Key 的旧成功值。结果进入 Pro 的标准 CSV、结果详情和历史快照导出；不额外写旧版可选的 `SN_MTFH_Result.csv` / `SN_MTFV_Result.csv`。原点名统一保留来源下划线，旧 H 曾删除下划线的命名不再沿用。

配置完成后用现场 Flow 和测量批次核对点名排序及网格方向。

### 解析 Key 与输出 Key

解析 Key 用于匹配 Engine 已产生的结果名称；输出 Key 用于区分项目聚合结果中的不同画面，不能互相替代。

- 按 Key 亮色度默认 `Key=White`、`CenterKey=P_5`。固定 W255 默认 `Key_Center=P_5`。若 W25 模板使用 `P_9`，按实际点名配置。中心点必须包含有限的 `Y`、`x`、`y`、`u`、`v`、`CCT` 实测值。实测零值沿用既有非正值处理和配方判断。中心色温 K/B 修正后参与步骤判定，默认范围为 6000–7000 K，边界值通过，限值为 0 的一侧不限。
- MTF 0368 使用 0F/0.3F/0.6F/0.8F，048 使用 0F/0.4F/0.8F，058 使用 0F/0.5F/0.8F。048 的第二组是 0.4F。
- H 表示横条纹，V 表示竖条纹，HV 是独立特殊图案。07 的五个点各用自己的解析 Key 与 Recipe。五点均读到有限值后才按画面 Key 写入 `MTFH07TestResults` 或 `MTFV07TestResults`，实测零值参与正常判定。缺测或解析失败时，该画面判失败并清除同一输出 Key 的旧测量。
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

流程组工具栏的“添加组”和“重命名”分别打开 `ProcessGroupNameWindow`。重命名预填当前组名并全选；名称在确认时去除首尾空白，拒绝空名称、控制字符以及与其他组不区分大小写的重名。确认成功后新组成为当前组，重命名只修改目标组名并保留流程实例及参数；取消或关闭弹窗不写入任何修改。保存失败在弹窗内提示并保留输入以便重试，原内存状态、活动组和配置文件保持不变。

默认文件为 `%APPDATA%\ColorVision\Config\ProjectARVRProProcessGroups.json`，路径由 `ViewResultManager.DirectoryPath` 提供。版本 3 格式一起保存活动组序号、流程组、独立解析映射及兼容 Recipe 容器；每项 `ConfigJson` 保存自身配置和 Recipe，相机覆盖作为流程项的可选同级字段保存。旧文件缺少该字段时恢复为默认关闭。

写入先生成同目录临时文件，刷新后替换正式文件；目标已存在时保存前一版为 `.bak`。普通保存失败会记录 `保存ProjectARVRProProcessGroups.json失败`；Recipe 编辑还会提示对应文件保存失败。这时内存值可能已改变，磁盘仍是旧内容，应处理路径、权限或空间问题后重试，不能仅看界面值判断保存成功。

| 入口 / 来源 | 当前行为 |
| --- | --- |
| 导出配置 | 生成 `.arvrprocess.json`，包含流程组和解析配置，供对应项目导入 |
| 导入配置 | 读取并校验配置后应用；持久化失败时保留原内存和磁盘配置 |
| 启动加载 | 优先读取 `ProjectARVRProProcessGroups.json`；文件不存在时，只从能明确识别为 ARVRPro 的旧共享 `ProcessGroups.json` 或 `ProcessMetas.json` 复制迁移 |
| 低于版本 3 的组配置 | 从各组按模板名补建解析映射，跳过空处理及已有映射，并保存迁移结果 |

迁移不删除或改写旧共享文件；无法确认归属的旧文件会保留并跳过，避免同时安装 LUX 时互相误读。新格式文件损坏时不会自动回退旧 `ProcessMetas.json` 或 `.bak`。没有有效流程组时可能出现 Default 空组；应先检查加载日志和原文件，再决定如何恢复，不能把空组视为配置从未存在。

历史解析由 `ResultProcessResolver` 优先使用记录中的类型完整名和 `ProcessConfigJson`；找不到完整名时仅接受唯一的同类名类型。配置快照无法恢复时会记录警告并使用默认配置，解析类型不可用时才回退到模板映射。因此“历史记录能打开”不等于使用了原 Recipe，应连同警告日志核对。

## 验证入口


串口切图、Flow 实际输出和客户阈值应在授权环境验证。
