# JND 模板

本页说明 `Engine/ColorVision.Engine/Templates/JND/` 的业务链路。JND 模板本身只保存少量算法参数，但执行时必须和 POI 模板一起使用，结果也按 POI 点位进行展示和导出。

## 适用范围

| 事项 | 当前实现 |
| --- | --- |
| 模板代码 | `OLED.JND.CalVas` |
| 模板类 | `TemplateJND : ITemplate<JNDParam>, IITemplateLoad` |
| 参数类 | `JNDParam` |
| 依赖模板 | `TemplatePoi` |
| 执行入口 | `AlgorithmJND`，显示名“JND” |
| UI 面板 | `DisplayJND.xaml(.cs)` |
| MQTT 事件 | `MQTTAlgorithmEventEnum.Event_OLED_JND_CalVas_GetData` |
| 结果处理 | `ViewHandleJND` |
| 主要结果类型 | `Compliance_Math_JND`、`JND_CalVas` |

## 源码入口

| 文件 | 用途 |
| --- | --- |
| `TemplateJND.cs` | 注册 JND 模板，设置 `TemplateDicId = 30` 和 `Code = OLED.JND.CalVas`。 |
| `JNDParam.cs` | 保存 `CutOff` 参数。 |
| `AlgorithmJND.cs` | 同时收集 JND 模板和 POI 模板，并发布算法请求。 |
| `DisplayJND.xaml.cs` | 提供 JND 模板、POI 模板、图像文件和设备来源选择。 |
| `ViewHandleJND.cs` | 加载结果、展示表格、绘制 POI 点、导出 CSV 和截图。 |
| `ViewRsultJND.cs` | 把 POI 结果中的 JSON 值解析为 `POIResultDataJND`。 |
| `MysqlJND.cs` | 恢复 MySQL 字典，默认 `CutOff = 0.3`。 |

## 执行链路

1. `TemplateJND` 被模板系统扫描后，加载到 `TemplateJND.Params`。
2. `DisplayJND` 同时绑定 `TemplateJND.Params` 和 `TemplatePoi.Params`。
3. 用户选择 JND 模板、POI 模板和输入图像。
4. `AlgorithmJND.SendCommand(...)` 组装参数：
   - `ImgFileName`
   - `FileType`
   - `DeviceCode`
   - `DeviceType`
   - `TemplateParam`，来自 JND 模板
   - `POITemplateParam`，来自 POI 模板
5. 命令发往 `Event_OLED_JND_CalVas_GetData`。
6. 结果处理时，`ViewHandleJND` 从 `PoiPointResultDao` 按主结果 `Pid` 取回点位结果，再由 `ViewRsultJND` 解析 `h_jnd`、`v_jnd`。

## 参数与结果

| 项目 | 说明 |
| --- | --- |
| `CutOff` | 轮廓裁剪系数，默认 `0.3`。变更时要保留对应图像、POI 模板和算法服务版本。 |
| `h_jnd` | 横向 JND 结果，来自算法服务回写的 `POIResultDataJND`。 |
| `v_jnd` | 纵向 JND 结果，来自算法服务回写的 `POIResultDataJND`。 |
| POI 点位 | JND 不是自己定义点位，而是消费 `TemplatePoi`。点位变更会直接影响 JND 输出。 |

## 项目维护边界

`ProjectShiyuan` 当前会使用 JND/POI 结果导出和 JND 验证。维护时不要把“JND CSV 已生成”等同于“产品 PASS”：项目侧还可能继续读取 `Compliance_Math_JND`、检查 `Validate` 字段、复制图像或生成伪彩图。

相关项目页：[ProjectShiyuan](../../projects/project-shiyuan.md)。

## 结果展示与导出

展示列包括：

- `Name`
- `PixelPos`
- `PixelSize`
- `Shapes`
- `JND.h_jnd`
- `JND.v_jnd`

`SideSave(...)` 会写出 CSV，并尝试保存当前图像视图。这里的 `selectedPath` 在实现中既被当作 CSV 路径，又参与拼接 PNG 路径；如果要改导出交互，先确认调用方传入的是文件路径还是目录路径。

## 常见排查

| 现象 | 优先排查 |
| --- | --- |
| 执行时报 POI 问题 | `TemplatePoi.Params` 是否加载，`TemplatePoiSelectedIndex` 是否有效。 |
| JND 结果为空 | 主结果类型是否是 `Compliance_Math_JND` 或 `JND_CalVas`，`PoiPointResultDao` 是否能按 `Pid` 查到点位。 |
| 表格有点但 JND 值为空 | `PoiPointResultModel.Value` 是否能反序列化为 `POIResultDataJND`。 |
| 项目结果 OK/NG 不一致 | 回看项目包对 JND 的二次验证，不要只看算法页结果。 |
| 导出路径异常 | 检查 `SideSave(...)` 的 `selectedPath` 语义。 |

## 检查清单

- 变更 `CutOff` 时，更新 `JNDParam.cs`、`MysqlJND.cs` 和现场推荐值。
- 修改 POI 选择或坐标系时，同步更新 [POI 模板](./poi-template.md) 和项目包文档。
- 修改结果字段时，同步更新 `ViewRsultJND.cs`、导出列、项目页和验收样例。
- 若项目依赖 JND 判定，项目页必须说明最终 OK/NG 的来源。
