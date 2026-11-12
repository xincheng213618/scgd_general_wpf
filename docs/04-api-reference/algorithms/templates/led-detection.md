---
knowledge_id: "algorithms.led"
knowledge_type: "reference"
status: "current"
summary: "区分灯条、灯珠强类型与 JSON V2 模板、事件、POI 输入和结果限制。"
aliases: ["LED灯条和灯珠检测该用哪个模板","TemplateLedCheck","TemplateLEDStripDetectionV2","ViewHandleLedCheck"]
code_paths: ["Engine/ColorVision.Engine/Templates/LEDStripDetection/AlgorithmLEDStripDetection.cs","Engine/ColorVision.Engine/Templates/LedCheck/AlgorithmLedCheck.cs","Engine/ColorVision.Engine/Templates/LedCheck/ViewHandleMTF.cs","Engine/ColorVision.Engine/Templates/Jsons/LEDStripDetectionV2","Engine/ColorVision.Engine/Templates/Jsons/LedCheck2"]
test_paths: []
related: ["algorithms.index","algorithms.json-templates","engine.results"]
---

# LED 检测模板

## 亚像素级灯珠检测

算法设备的 **定位算法 → 亚像素级灯珠检测** 和流程的 **LED定位** 使用 `TemplateLedCheck2` JSON 模板。模板编码为 `FindLED`，字典号为 `18`。手动入口通过 `Event_OLED_FindDotsArrayMem_GetData` 发送模板、颜色、FDA 类型和四个固定 LED 点位。编辑 JSON 模板的方式见[模板菜单与配置宿主](./template-menu-entries.md)。

规则灯珠阵列的本地定位和不亮、偏暗检验见[灯珠定位与完整网格判定](../detectors/light-beads.md)。该流程使用本次运行的内存 POI 点集，可接本地 POI 测量。

## 历史模板与结果

灯条检测和像素级灯珠检测保留模板数据和历史结果读取路径，供旧流程包解析与历史结果排查。区分同编码模板时同时核对模板类型、字典号和请求事件。

| 历史模板 | 标识 | 请求区分 |
| --- | --- | --- |
| `TemplateLEDStripDetection` | `LEDStripDetection`，字典 `21` | `Event_LED_StripDetection` 的旧版参数 |
| `TemplateLEDStripDetectionV2` | `LEDStripDetection`，字典 `26` | 同事件，`Version = "2.0"`，可附 POI 模板 |
| `TemplateLedCheck` | `FindLED`，强类型 | `Event_LED_Check_GetData`，使用灯珠模板和 POI 模板 |

`ViewHandleLEDStripDetectionV2` 从历史明细引用的 JSON 结果文件读取点位，排查时先确认结果版本和文件可读性。`ViewHandleLedCheck` 的 `CanHandle` 为空，自动分发不接管其专用历史结果显示。
