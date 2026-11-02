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
4. 设置 **报表保存目录**；留空时使用结果输出目录。LUX CSV 文件名按 SN 生成，无需填写报表项目名称。
5. 核对流程的输出 Key、Recipe 和实际 POI，再用同一 SN 完成多条命令，确认 `C_<SN>.csv` 更新为该 SN 的累计结果；换 SN 产生另一个文件。

普通 Pro 测试完成时，客户 CSV 与标准 CSV 分别受自己的保存开关控制。LUX 文本流程选择上述格式后，每条成功的普通测量命令会在**报表保存目录**更新 `C_<SN>.csv`，取代该次内置 C_ 导出；未开启或未选 LUX 客户格式时，保留原有 C_ 导出路径。VID 的 B_ 与光通量的 D_ 文件不受此选择影响，仍写入结果输出目录。TCP 回包格式与时机仍由测量协议决定，CSV 写入错误单独记录日志。

## 格式与数值

表头保持：

```text
Test_Screen,Test_item,Test_Value,unit,lower_limit,upper_limit,Test_Result
```

| 内容 | Current | Legacy |
| --- | --- | --- |
| W255 | 沿用旧 Current 字段顺序与可见字段 | 使用旧兼容固定字段集合，包含对角 FOV |
| W255 亮度均匀性 | 数值及限值由比值转换为百分数，单位 `%` | 同样转换数值及限值，单位沿用旧 Legacy 的空值 |
| AR 048 MTF | `P1_H1`、`P1_horizontal` 等点号名称 | `0F_Center_H1`、`0.4F_LeftUp_horizontal` 等位置名称，单位 `%` |

MTF 数值沿用现有 MTF 结果；Legacy 的 `%` 标签不触发额外乘以 100。格式投影不修改测量值、Recipe 或主程序的总判定。

棋盘格从本次流程保存的快照读取 POI 明细、对比度和限值，避免只有汇总值。默认分组按点数对应旧 `Chessborad_4x4` / `Chessborad_5x5` / `Chessborad_6x6`；自定义输出 Key 保留。W51 的 FOV 与 W255 数据合并到旧 W255 分组，畸变与光学中心保留旧项目名称。

POI 没有独立限值时，按默认 `0/0`（两侧不限）计算 `pass/fail`。旧固定字段没有对应结果时，沿用旧 LUX 模型的零值与默认限值；不额外恢复逐点 Recipe 或原始 MTF 分量计算。非有限数据输出失败。客户 CSV 不使用 `None` 作为判定或单位。

同 SN 的重测覆盖原文件，不追加重复表头；文件名替换 Windows 非法字符，数值使用小数点，含逗号或引号的字段按 CSV 规则转义。

## 维护与验证

`Exports/Lux/` 专门维护 LUX 投影、固定字段契约和 CSV 命名。客户报表注册、设置、输出目录及空间保护仍由共享导出服务管理；金星 XLSX 继续按原格式保存。旧 `IsSaveCustomXlsx` 等配置字段保留，已有客户配置不会自动切换到 LUX。

本地测试保护两种字段口径、比值与限值转换、POI 默认判定、同 SN 覆盖、文件名和转义、旧配置及异步导出设置快照；这些验证不能代替现场客户解析器和真实设备验收。现场需将 Current/Legacy 各导出一份，与此前同类结果核对项目名、单位、限值和判定。
