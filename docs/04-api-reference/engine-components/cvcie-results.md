---
knowledge_id: "engine.cvcie-results"
knowledge_type: "topic"
status: "current"
summary: "CVCIE POI 的非正值替换、色值重算与生效时机；区分本地测量、历史结果缓存、鼠标探针和原始文件。"
aliases: ["CVCIE计算负值", "CVCIEShowConfig", "WindowCVCIE", "启用非正值替换", "ClampNonPositiveValues", "MinimumValue", "0.0001", "POI负值", "CVCIE最小值", "CVCIE色温", "CVCIE主波长", "XYZ重算", "结果数值替换", "NormalizeXyz", "CalculateColorMetrics"]
code_paths: ["Engine/ColorVision.Engine/Media/WindowCVCIE.xaml.cs", "Engine/ColorVision.Engine/Media/WindowCVCIE.xaml", "Engine/ColorVision.Engine/Media/CVRawOpen.cs", "Engine/ColorVision.Engine/Services/POI/PoiMeasurementService.cs", "Engine/ColorVision.Engine/Templates/POI/AlgorithmImp/PoiResultCIExyuvData.cs", "Engine/ColorVision.Engine/Templates/POI/AlgorithmImp/PoiResultData.cs", "Engine/ColorVision.Engine/Templates/POI/AlgorithmImp/ViewHanlePOIXZY.cs", "Engine/ColorVision.Engine/Templates/POI/AlgorithmImp/ViewHanlePOIY.cs"]
test_paths: ["Test/ColorVision.UI.Tests/CvcieResultValueTests.cs", "Test/ColorVision.UI.Tests/PoiMeasurementServiceTests.cs"]
related: ["engine.file-io", "algorithms.poi-routes", "engine.opencv-helper-api", "engine.results", "ui.property-grid", "ui.configuration"]
---

# CVCIE POI 结果数值

本主题说明 CVCIE/内存 CIE 的 POI 测量值如何进入结果列表、标注与 CSV，以及非正值处理的适用范围。`CVCIEShowConfig` 是应用级设置，不是每张图像的配置；它不会修改 CVCIE 文件或数据库原始 `Value`。

## 调整结果数值

前提是图像视图已经加载可测量的 CIE 数据。打开与通道选择见 [CV 文件读取](./ColorVision.FileIO.md)；单纯的显示底图不保证存在测量缓冲。

1. 在图像中准备圆形或矩形测量区域，右键选择 **POI**，打开 `WindowCVCIE` 结果窗口。
2. 点击结果窗口的齿轮，在 **计算结果** 分类调整以下选项。
3. 返回图像，再次执行 **POI**，查看新结果窗口。设置不会重新计算已打开窗口里的结果或旧标注。
4. 需要 CSV 时，在新结果窗口点击保存图标；导出使用该窗口的全部结果，不会重新测量。

| 配置 | 默认值 | 行为 |
| --- | --- | --- |
| **启用非正值替换** `ClampNonPositiveValues` | 开启 | 对三通道 X、Y、Z 分别处理；单通道只处理 Y |
| **最小值** `MinimumValue` | `0.0001` | 替换值，接受大于或等于 0 的有限数；负数、NaN 或无穷大不写入，保留先前设置；关闭替换时隐藏此字段 |

齿轮使用直接编辑模式：字段写入设置对象后，关闭窗口不会撤销；它也没有独立的文件保存步骤。持久化遵循[应用配置保存](../ui-components/configuration.md)，旧配置缺少这两个字段时使用默认值。配置重载后，已开结果窗口仍持有初始化时的设置引用，应重新打开结果窗口再调整，避免编辑旧对象。

## 替换规则与色值重算

“最小值”是替换值，不是所有结果的下限。开启时，`CreateValueNormalizer` 按 `!(value > 0)` 判断：

| 输入值 | 开启替换 | 关闭替换 |
| --- | --- | --- |
| 正有限数 | 保留，即使小于 `MinimumValue` | 保留 |
| 0、负数、NaN、负无穷 | 替换为 `MinimumValue` | 保留 |
| 正无穷 | 保留 | 保留 |

例如默认设置下，`-2` 和 `0` 变为 `0.0001`，`0.00001` 仍为 `0.00001`。开关不能作为“所有输出都有限或大于零”的保证；`MinimumValue=0` 也合法。

三通道通过 `NormalizeXyz` 比较替换前后的 XYZ，**只有至少一项实际变化才重算** x、y、u′、v′、CCT 与 Wave。它不单独替换这些衍生字段；XYZ 未变化时保留原来的色值，即使原值为负数或 NaN。单通道没有此重算步骤。

重算时，`CalculateColorMetrics` 先以 XYZ 中最大值归一化为浮点比例，再调用同一 POI 原生色温公式与主波长表，结果对象仍保留替换后的 XYZ。最大值不是正有限数时，衍生字段统一返回 NaN。例如负 XYZ 被替换为全零会触发此结果；原来就是全零、替换后也未变化时，则直接保留原衍生字段。原生公式还可能返回无效色值，主波长计算有 `-1`、`-99` 哨兵值，不能把重算完成当作有效测色判定。

## 生效时机与输出

| 路径 | 使用设置的时机与范围 |
| --- | --- |
| 图像右键 **POI** | 命令开始时取得一次配置快照，整批使用同一个数值处理函数；标注、列表、统计与 CSV 以这一批处理后的结果为输入 |
| 数据库结果转为 `PoiResultCIExyuvData(model)` / `PoiResultCIEYData(model)` | 每次构造显示对象时读取设置；不改模型中的原始 JSON，也不回写数据库 |
| 已加载的历史结果 | `ViewHanlePOIXZY` / `ViewHanlePOIY` 只在 `ViewResults` 为 null 时加载；再次选中同一项或执行 **保存数据列** 会复用已有值 |
| 鼠标探针、CIE 图探针、常规 `PoiMeasurementService.Calculate` | 使用常规测量路径，不读取此替换配置 |
| 真彩图像显示 | 使用独立的显示配置，不由 `CVCIEShowConfig` 控制 |

修改后要重新观察历史数据，可重新 **查询** 创建新的主结果对象，再选中对应记录；查询与导出入口的范围见[算法结果交接](./result-handoff-chain.md)。关闭替换只能显示现有原始数据，无法恢复已被上游计算替换或丢弃的负值。

本地图像 POI 先用 `CalculateRaw` 请求保留原生非正值，再应用上述设置，因此关闭开关时能保留本次测量的实际负值。此路径要求 DLL 支持相应 V2 标志，不会因关闭界面开关而改回旧调用；参数、固定下限和不兼容 DLL 的处理见 [POI Batch API](./opencv-helper-api.md#poi-batch-api)。

标注还受显示数量和格式控制：本地视图的图元总数达到 1000 时不更新文字，三通道还检查 `IsShowString` 并使用 `Template`，单通道文字固定为 `Y:F1`。因此文字缺失或舍入为零，不等于结果集合没有数值。CSV 直接使用结果对象并覆盖选定文件；导出不同于重新计算。

## 验证边界

| 测试 | 断言范围 |
| --- | --- |
| `CvcieResultValueTests` | 旧 JSON 默认值、非法设置、配置快照、已有对象不刷新、替换后色值重算、未变化 XYZ 保留衍生值；色值重算用例会调用 native |
| `PoiMeasurementServiceTests` | 点/圆/矩形的负 XYZ 保留、单通道 Y、常规路径保留既有替换行为与已释放缓冲拒绝；需要匹配的 native DLL |

这些测试不覆盖真实窗口的完整操作、配置落盘后重启、旧 DLL 组合或全部非有限数边界。源码规则、已有测试断言和实际设备/样本验收需分别核对。
