---
knowledge_id: "projects.arvr-pro-lux-export"
knowledge_type: "guide"
status: "current"
summary: "在 ARVRPro 客户报表中配置 LUX Current/Legacy 逐项 CSV，说明单 SN 文件、默认判定和两种格式的兼容边界。"
aliases: ["LUX CSV", "LUX Current", "LUX Legacy", "LUX_Current_CSV", "LUX_Legacy_CSV", "旧Lux导出", "CSV导出格式", "保存客户报表", "一个SN一个文件"]
code_paths: ["Projects/ProjectARVRPro/Exports/Lux/", "Projects/ProjectARVRPro/Exports/CustomTestResultExportService.cs", "Projects/ProjectARVRPro/Exports/ITestResultCustomExporter.cs", "Projects/ProjectARVRPro/Exports/CustomTestResultExportOptions.cs", "Projects/ProjectARVRPro/ARVRWindow.CustomerExports.cs", "Projects/ProjectARVRPro/ARVRWindow.Lux.cs", "Projects/ProjectARVRPro/ViewResultManager.cs"]
test_paths: ["Test/ProjectARVRPro.Tests/LuxCustomerCsvExporterTests.cs", "Test/ProjectARVRPro.Tests/JinxingInspectionXlsxExporterTests.cs", "Test/ProjectARVRPro.Tests/ProjectSettingsSessionTests.cs"]
related: ["projects.lux-protocol", "projects.arvr-pro", "projects.arvr-pro-processes"]
---

# 配置 LUX 客户 CSV

ARVRPro 的客户报表提供旧独立 ProjectLUX 的 `Current`、`Legacy` 两种逐项 CSV 格式，覆盖现场使用的 W51、W255、棋盘格、MTF、畸变和十字/光学中心。两者均为七列、多行，按 SN 单独保存；与按天追加一行记录的金星 XLSX、旧版 ARVR 横向输出分别选择。

## 现场配置

1. 更新 ARVRPro 项目包，并满足随包 manifest 的宿主版本要求。
2. 打开 **模组检测设置 → 结果保存**，开启 **保存客户报表**。
3. **报表格式** 按旧 LUX 的 `CSV导出格式` 选择：原 `Current` 对应 `LUX_Current_CSV`，原 `Legacy` 对应 `LUX_Legacy_CSV`。
4. 选择 `LUX_Legacy_CSV` 时，按项目设置 **MTF 点位格式**：默认 **058（0F / 0.5F / 0.8F）**，需要旧名称的项目选 **048（0F / 0.4F / 0.8F）**。该设置保存到项目配置，只决定报表点位名称；实际数据和 Recipe 始终来自所选流程。
5. 按客户要求选择 **亮度均匀性格式**，然后设置 **报表保存目录**。留空时使用结果输出目录，LUX CSV 文件名按 SN 生成。
6. 核对流程的输出 Key、Recipe 和实际 POI，再用同一 SN 完成多条命令，确认 `C_<SN>.csv` 更新为该 SN 的累计结果；换 SN 产生另一个文件。

普通 Pro 测试完成时，客户 CSV 与标准 CSV 分别受自己的保存开关控制。LUX 文本流程选择上述格式后，每条成功的普通测量命令会在**报表保存目录**更新 `C_<SN>.csv`，取代该次内置 C_ 导出；未开启或未选 LUX 客户格式时，保留原有 C_ 导出路径。VID 的 B_ 与光通量的 D_ 文件不受此选择影响，仍写入结果输出目录。TCP 回包格式与时机仍由测量协议决定，CSV 写入错误单独记录日志。

## 格式与数值

表头保持：

```text
Test_Screen,Test_item,Test_Value,unit,lower_limit,upper_limit,Test_Result
```

| 内容 | Current | Legacy |
| --- | --- | --- |
| W255 | 沿用旧 Current 字段顺序与可见字段 | 使用旧兼容固定字段集合，包含对角 FOV |
| W255 亮度均匀性（百分比） | 数值及限值由比值转换为百分数，单位 `%` | 同样转换数值及限值，单位沿用旧 Legacy 的空值 |
| W255 亮度均匀性（小数） | 数值及限值使用比值，单位沿用 `%` | 数值及限值使用比值，单位沿用空值 |
| 048 / 058 MTF | `P1_H1`、`P1_horizontal` 等点号名称 | 按点位格式输出 `0.5F_LeftUp_horizontal`（058）或 `0.4F_LeftUp_horizontal`（048）等位置名称，单位 `%` |

两种数值格式都使用 `Luminance_Uniformity(min_max_100)`，供客户解析器按固定名称读取。旧配置继续使用百分比。此设置独立于流程的均匀性显示格式，并在下次生成 `C_<SN>.csv` 时生效。MTF 数值沿用现有 MTF 结果，Legacy 的 `%` 标签不触发额外乘以 100。角度符号 `°` 在客户 CSV 中统一为 `degree`，避免电子表格按不同编码打开时显示乱码。格式投影不修改测量值、Recipe 或主程序的总判定。

048、058 流程在客户报表中均只输出所选 LUX 格式的 54 项，不叠加 Pro 通用字段；水平、垂直平均值沿用当前结果和限值，原始四分量读取本次记录的流程快照。不同处理类型及各次未命名结果分别匹配，058 的自定义输出 Key 取最新快照。解析使用实际流程保存的点位 Key，报表名称选择不会改变采样位置。旧 LUX 曾以 `0.4F` 名称读取 `0.5F`；需要这种兼容名称时可选 048，普通 Pro 导出保持原有名称。原始分量不额外缩放或恢复旧独立修正参数；缺少原始快照时保持旧模型默认值。

W255 的九点取值兼容 `P1` 与 `P_1`，平均亮度由实际白场 POI 的亮度计算；没有 POI 时保持默认值，无效亮度不会被过滤成有效平均值。中心 u′ 统一为 `Center_CIE_1976Chromatic_Coordinates_u'`，只输出一项。棋盘格从本次流程快照读取 POI 明细、对比度和限值，默认分组按点数对应旧 `Chessborad_4x4` / `Chessborad_5x5` / `Chessborad_6x6`，自定义输出 Key 保留。W51 的 FOV 与 W255 数据合并到旧 W255 分组，畸变与光学中心保留旧项目名称。

POI 和原始 MTF 分量没有独立限值时，按默认 `0/0`（两侧不限）计算 `pass/fail`。旧固定字段没有对应结果时，沿用旧 LUX 模型的零值与默认限值，不额外恢复逐点 Recipe。非有限数据输出失败。客户 CSV 不使用 `None` 作为判定或单位。

同 SN 的 C_、B_、D_ 文件在新内容写完后才替换，写入失败时保留上一份报表。文件名替换 Windows 非法字符，数值使用小数点，含逗号或引号的字段按 CSV 规则转义。

## 维护与验证

`Exports/Lux/` 专门维护 LUX 投影、固定字段契约和 CSV 命名。客户报表注册、设置、输出目录及空间保护仍由共享导出服务管理；金星 XLSX 继续按原格式保存。旧 `IsSaveCustomXlsx` 等配置字段保留，已有客户配置不会自动切换到 LUX。

本地测试保护两种字段口径、比值与限值转换、POI 默认判定、同 SN 覆盖、文件名和转义、旧配置及异步导出设置快照；这些验证不能代替现场客户解析器和真实设备验收。现场需将 Current/Legacy 各导出一份，与此前同类结果核对项目名、单位、限值和判定。
