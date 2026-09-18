---
knowledge_id: "ui.cie-analysis"
knowledge_type: "topic"
status: "current"
summary: "统一 CIE 窗口的样品、色差、色域覆盖与导出契约；区分实测 XYZ、RGB 推算与仅色坐标。"
aliases: ["色度分析", "色度分析工作台", "CIE 色度图", "色差计算", "色域覆盖率", "CIEDE2000", "JNCD", "CieSampleAnalysisView", "CieAnalysisMath", "CieGamutGeometry", "WindowCIE", "ManualColorGamutView"]
code_paths: ["UI/ColorVision.ImageEditor/Cie", "Engine/ColorVision.Engine/Media/CvcieDiagramEditorTool.cs"]
test_paths: ["Test/ColorVision.UI.Tests/CieAnalysisTests.cs", "Test/ColorVision.UI.Tests/TestData/Cie/ciede2000testdata.txt"]
related: ["ui.image-editor", "engine.cvcie-results"]
---

# CIE 色度与样品分析

使用一个 `WindowCIE`，分为 **色度图 / 色域计算 / 样品与色差** 三个页签，不再提供独立工作台。色度图用于当前点查看，色域计算保留唯一的 RGB 三原色比较界面，样品与色差嵌入 `CieSampleAnalysisView`，保留多样品、色差、导入导出和样品会话。原有取点公共接口继续可用。计算与界面位于 ImageEditor，不反向依赖 Engine 或仪器服务。

## 使用流程

1. 切换到 **样品与色差**，在右侧选择输入空间，填写名称、分组和分量，点击 **添加样品**；或用 **导入 CSV / 粘贴表格** 追加数据。**模板 / 演示数据** 提供明确标为示例的参考白和样品。
2. 在底部选中目标并 **设为参考**，所有兼容样品相对于它计算色差。表格选择联动图中高亮、连线与右侧结果。
3. **读取所选** 将 XYZ（仅色坐标为 xy）装入编辑区，修改后 **更新所选**。普通选择不覆盖输入草稿。
4. **保存会话** 留存样品数据与条件，或 **导出数据 / 导出报告 / 保存图像** 交付结果。选中样品后使用 **更多… → 用于色域** 指定 R、G 或 B，自动切换到色域计算并保留其他原色；传递未舍入的 1931 xy。**计算说明** 在当前页展开，返回后保留编辑内容。

顶部页签与当前页工具栏共用一行；缩小、放大、适应使用统一的主题线条图标与悬停说明。正常窗口切换到色域页或样品页时按所需空间自动扩大（分别为 940×680、1080×780 DIP），以所在屏幕可用工作区为上限；切回色度图不强制缩小，最大化状态不变。

双击附近样品点可选择；双击其他色度位置填入 xy，仍需点击添加才记录。支持 xy、1960 uv、1976 u′v′ 图，分析面板可折叠，样品区域可拖动分隔条调节。筛选只影响图与表，统计和导出包含全部样品。

三个坐标系的图表背景跟随应用主题：浅色主题为白底，深色主题为黑底，坐标、网格和波长标注同步调整；图中色彩与计算值不变。主题切换保留当前缩放，位图缓存分别保存深浅版本，PNG 和报告图像使用当前图表主题。样品分析的下拉框及其选项列表使用应用主题样式。

色度图右下角鼠标读数分两排：坐标，以及近似 CCT / Duv、主／补波长、激发纯度（紫色区域为紫线方向比例）。左下角 **当前点** 展开后显示相同色度指标及距当前参考白的 Δu′v′；若源点提供 XYZ，再显示 XYZ、Y、Lab、Luv、彩度和色相。仅 xy 不显示虚构亮度，sRGB 来源标明推算，白点及光谱外点的波长/纯度显示为空；清除选择同时清除派生读数。

顶部 **计算参考白** 下拉框默认 D65，选择 D50、D55、D60、D75、A、C、E 等预设立即生效；选择 **自定义** 打开显示选项，在 **计算参考白** 中填写 xy 与 Yn 后点击 **应用参考白**。要求有限数值、x>0、y>0、x+y<1、Yn>0，无效输入保留已应用条件。绝对样品 Yn 默认 100 cd/m²，相对样品始终使用参考白 Y=100。参考条件仅属于当前 CIE 窗口，当前点、鼠标读数与“样品与色差”页同步；样品页应用条件也同步回色度图，会话沿用现有白点与亮度字段保存。新建会话恢复默认，各 CIE 窗口相互独立。

参考白影响主／补波长、纯度、距白点 Δu′v′、Lab/Luv 及其色差、彩度和色相；不会重写已有样品 XYZ，不作隐式色适应，不改变 xy/uv、CCT/Duv 或色域面积与覆盖率。**白点 / 光源** 勾选仅控制图层显示，与计算参考白分开；POI、光谱模块的原有计算与导出不随本窗口参考改变。

## 数据来源与尺度

| 来源 | 语义 |
| --- | --- |
| xyY、XYZ、u′v′Y | 显式选择相对或绝对尺度，存储 XYZ |
| Lab、Luv | 以当前参考白转换为 XYZ 后存储；以后换白点不会重新解释当初的输入 |
| sRGB 0–255 整数 | 使用 sRGB 传递函数与 D65 矩阵推算相对 XYZ，来源标为 `sRGB 推算 / D65` |
| 仅 xy、旧 `ChangeSelect(x,y)` / `SetSelectedMarker` | `ChromaticityOnly`；内部归一化 XYZ 仅用于转换，不把 Y 当成亮度，不报告 Lab/Luv 色差 |
| CVCIE 图片 POI 取点 | Engine 的 `CvcieDiagramEditorTool` 传入 `PoiMeasurementResult.X/Y/Z`，标为绝对 XYZ，不从预览 RGB 还原测量值 |
| POI 结果窗口 | 通过 **显示到 CIE 图** 联动当前点；有效 XYZ 为绝对亮度，其他可显示色坐标保留仅 xy 语义。在“样品与色差”中加入当前点快照，不改变既有 POI CSV，详见 [POI 结果数值](../engine-components/cvcie-results.md#在-cie-查看与分析) |

**加入色度图当前点** 创建快照，后续取点或清除当前点不改已加入的样品。三个页签切换保留样品、参考和色域输入；关闭 CIE 时对未保存样品会话提供保存/放弃/取消。产生样品会话后解除 WPF Owner 关系，避免父窗口关闭绕过会话的保存提示。其他仍仅提供色坐标的调用者（包括已有 Spectrum 选点链）保持仅色坐标语义，不虚构亮度。光谱标定与设备控制不属于本窗口。

相对样品参考白 Y=100；绝对样品 Y 单位为 cd/m²，参考白亮度可配置。XYZ 必须为有限非负值。全黑 XYZ=(0,0,0) 可比较 Lab 色差，但色度与波长不定义。仅色坐标或相对/绝对尺度混合时可比较 Δu′v′，不生成完整色差判定。

## 计算约定

- 使用 CIE 1931 2° 体系与预设/自定义白点；所有样品在共同白点下计算，不隐式进行色适应。更换白点不改变已有 XYZ。
- ΔE76 为 Lab 距离；ΔEuv 为 Luv 距离；ΔE00 为 CIEDE2000（kL=kC=kH=1）；ΔE94 使用 graphic arts 参数；CMC 同时显示 1:1 和 2:1。CIE94 / CMC 具有方向性，第一色始终为参考样品。
- Δu′v′ 为样品对在 1976 平面的距离；Duv 为单点到黑体轨迹在 1960 uv 平面的有符号距离。JNCD=Δu′v′/步长，默认 0.004 是可修改约定。ΔE00 默认阈值 2 也是比较条件，不代表行业认证。统计排除参考点和不能比较的样品。
- 样品分析、色度图当前点与鼠标读数的 CCT/Duv 使用近似 Planckian 轨迹在 1960 uv 平面的分段投影，范围 1667–25000 K；距离大于 0.05 或最近点落在端点时不报告。旧公共估计接口保留兼容，POI 表格的原生 CCT / Wave 不受影响；各模块的色温算法不能混为一谈。
- 主波长取参考白向样品射线与光谱轨迹的交点；紫色区域报告反向射线的补色波长。纯度为白点至样品距离 / 白点至边界距离，紫色区域以紫线为边界。白点自身和光谱边界外不报告波长。轨迹采用现有嵌入 CSV 与线段插值。
- 色温参考线在 1960 uv 平面构造法线后转换到当前图，是近似辅助线。图面色彩仅供屏幕示意。

依据：[Sharma、Wu、Dalal 的 CIEDE2000 说明与补充数据](https://hajim.rochester.edu/ece/sites/gsharma/ciede2000/)、[CIE TN 013:2022](https://www.cie.co.at/publications/terms-related-planckian-radiation-temperature-light-sources)、[Colour CMC 文档的独立数值例](https://colour.readthedocs.io/en/develop/generated/colour.difference.delta_E_CMC.html)。

## 色域分析

色域页由唯一的 `ManualColorGamutView` 承载，输入始终为三原色的 1931 xy，可以选择 xy / u′v′ 计算平面并与多个标准比较。

| 指标 | 定义 |
| --- | --- |
| 面积比 | 样品面积 / 标准面积，可超过 100% |
| 覆盖率 | 交集面积 / 标准面积，范围 0–100% |
| 交集面积 | 当前平面的交集多边形面积；选中结果行显示交集填充 |

二维面积不是三维色域体积，坐标平面决定数值。原色重复或共线拒绝计算。CSV 保留两组三原色、面积比、覆盖率、交集面积和平面。旧 `DefaultCieColorGamutCalculator.Calculate` 的 `CoveragePercent` 保持历史 xy 面积比语义；新覆盖使用 `CieGamutGeometry.Compare`。

## 导入与保存

CSV 使用 UTF-8、小数点 `.`；Excel 粘贴为 TSV。支持下列表头（列名区分大小写）：

```csv
Name,Space,V1,V2,V3,Group,Source,Basis
参考白,XyY,0.31271,0.32902,100,示例,手动输入,Relative
样品 A,XyY,0.3187,0.3434,100,示例,手动输入,Relative
```

`Space` 为 `XyY`、`XYZ`、`UvY`、`Lab`、`Luv`、`SRgb`、`Xy`；`Basis` 为 `Relative`、`Absolute`、`ChromaticityOnly`，缺省 Relative。也接受 `X,Y,Z`、`x,y,Y`、`x,y` 简表。Name、Group、Source 可选。支持引号、字段内换行与 BOM；整批验证后才追加，错误显示行号。最多 5000 点、16 MiB 输入文本/文件。

- CSV 导出 XYZ 原值、尺度与派生结果；仅色坐标重新导入不会变成实测亮度。自由文本字段输出时防止以公式开头。
- `.cie-session.json` 版本 1 保存样品 ID、名称、分组、来源、XYZ、尺度、参考 ID、白点、阈值与图层设置；拒绝未知版本、重复 ID、失效参考和非有限数值。新建、打开、关闭时可保存未保存的会话。
- PNG 捕获当前图表视图，包含筛选与缩放；HTML 报告内嵌该 PNG 并注明视图范围，另列全部样品与条件，可离线查看、打印。
- 色域页独立导出，不包含在样品会话 JSON 中；未提交输入草稿、缩放和其他主程序窗口状态也不保存在会话中。

## 验证与排障

运行 `CieAnalysisTests` 与 `CieWindowCompositionTests`，覆盖 34 组公开 ΔE00 数据、CIE94/CMC 独立数值、黑色与分段转换、uv/xy 往返、CCT 适用性、主/补波长、色域交集、尺度隔离、CSV/会话往返、报告转义与 WPF 交互；原窗口测试覆盖组成、布局与缩放保留。

设置 `COLORVISION_CIE_PREVIEW_OUTPUT` 可输出深浅主题、正常/最小窗口的 WPF 渲染图。这不代替实机高 DPI、仪器数据或客户验收，也不证明设备校准正确。完整色差空值检查亮度与尺度，CCT 空值检查距黑体轨迹是否过远；导入失败检查表头大小写、列数和小数点。样品页白点与阈值编辑后需点击 **应用计算条件**；色度图预设即时应用，自定义输入需点击 **应用参考白**。参考切换测试覆盖窗口隔离、两个页面同步、源 XYZ 保留、静止鼠标读数刷新、亮度尺度、无效输入与会话恢复。
