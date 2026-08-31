---
knowledge_id: "operations.smu"
knowledge_type: "topic"
status: "current"
summary: "SMU手动与Flow参数、A/B通道、扫描结果及关闭输出边界；成功回包、空读数或超时都不能单独证明输出安全关闭。"
aliases: ["SMU","源表","点测","扫描电压","通道串了","关闭输出","MQTTSMU","SMUParam","SMUSweepModelNode"]
code_paths: ["Engine/ColorVision.Engine/Services/Devices/SMU/DeviceSMU.cs","Engine/ColorVision.Engine/Services/Devices/SMU/Configs/ConfigSMU.cs","Engine/ColorVision.Engine/Services/Devices/SMU/MQTTSMU.cs","Engine/ColorVision.Engine/Services/Devices/SMU/DisplaySMU.xaml.cs","Engine/ColorVision.Engine/Services/Devices/SMU/SMUParam.cs","Engine/ColorVision.Engine/Services/Devices/SMU/Dao/SMUResultModel.cs","Engine/ColorVision.Engine/Services/Devices/SMU/Dao/SmuScanModel.cs","Engine/FlowEngineLib/SMUNode.cs","Engine/FlowEngineLib/SMUBaseNode.cs","Engine/FlowEngineLib/Node/SMU/SMUSweepModelNode.cs"]
test_paths: ["Test/ColorVision.UI.Tests/NodeConfiguratorBindingTests.cs","Test/ColorVision.UI.Tests/InitTableEntityMappingTests.cs"]
related: ["engine.devices","operations.device-configuration","flow.session"]
---

# SMU 参数、结果与输出关闭

`DeviceSMU` 的手动点测、手动扫描和 Flow 节点有不同的参数来源与完成处理。查询“通道串了”“扫描不使用当前值”“失败后是否关闭输出”时，必须先区分这三条调用路径。

点测和扫描会向被测件施加电压/电流。执行前由现场操作者确认接线、极性、限压/限流、额定范围与授权。客户端限制开关或成功回包不能替代仪器保护；只读问答、文档或代码修改不授权通电、扫描或试验保护动作。

## 哪一层拥有参数

- `ConfigSMU` 拥有设备类型、设备名和连接等设备配置，设置提交走公共[设备配置保存](./configuration.md)；保存不等于远端已经应用。
- `DisplaySMUConfig` 由 `DisplayConfigManager` 按设备 `Config.Code` 管理。A/B 通道分别持有电压源、电流源的测量值/限值以及显示读数；`CurrentSourceConfig` 由当前 `Channel` 和 `IsSourceV` 选出。切换通道或源类型是在切换参数组，不是发送测量命令。
- `TemplateSMUParam` / `SMUParam` 是数据库模板参数，模板字典 ID 为 `13`。手动扫描读取模板的源类型、起点、终点、点数与限值，但通道显式取自当前 `DisplayConfig.Channel`，不是 `SMUParam.Channel`；该手动载荷也没有序列化模板的 `IsAutoRng` / `SrcRng` / `LmtRng`。
- Flow 节点使用节点/服务模板自己的参数，不继承手动控件选择；量程、通道与关闭输出设置须按具体节点核对。

显示配置中的值可能来自之前输入或之前读回，不能作为当前仪器输出状态的证据。

## 手动 MQTT 命令与客户端限制

| 命令 | 实际参数来源和载荷 |
| --- | --- |
| `Open` | `DevName`、`IsNet`，加当前显示 `Channel` |
| `GetData` | 当前源类型与当前参数组，发送 `IsSourceV`、`MeasureValue`、`LimitValue`、`Channel` |
| `Scan` | `Params.DeviceParam` 内的 `IsSourceV`、`BeginValue`、`EndValue`、`LimitValue`、`Points`、调用方指定的 `Channel` |
| `CloseOutput` | 调用这一刻的 `DisplayConfig.Channel`，不是自动绑定到上次点测/扫描通道 |
| `Close` | 关闭设备服务请求；与单通道 `CloseOutput` 不是同一个命令 |

`IsUseLimitSigned` 默认启用。`GetData` / `Scan` 校验失败会返回 `null`，不会发出请求；`IsLimit` 内把电流输入除以 1000 后比较，即这一路电流参数按 mA 换算。现有显式限制仅覆盖 `Keithley_2400`、`Keithley_2600`、`Precise_S100` 分支，其它设备类型直接放行；扫描检查只使用终点和限值，不等于校验了整个扫描区间。不要以关闭此校验作为排障手段，也不要把这些客户端分支当作完整硬件安全规范。

`SetParam()` 虽然返回 `true`，当前只表示已调用发布方法；它没有等待服务成功回包。其它方法返回的 `MsgRecord` 同样只是消息追踪对象。

## 成功回包如何变成结果

`MQTTSMU` 的结果处理筛选订阅主题、设备 Code 和 `Code=0`。公共消息层把匹配 `MsgID` 的成功回包标为 `Success`，不等待下面的数据库读取或视图添加，所以请求成功不等于结果已可展示。

| 回包路径 | 结果关联与更新 |
| --- | --- |
| `GetData` | 要求 `Data.MasterId > 0`，按 ID 从 MySQL 读取 `SMUResultModel`；有记录才新增结果并更新记录所属 A/B 通道读数 |
| 当前通道的 `GetData` | 仅当 `model.ChannelType == DisplayConfig.Channel`，再更新当前投影读数，并将 V/I 同步到现有 `DeviceSpectrum` 的显示配置 |
| `Scan`，`FlowEngineManager.ServiceVersion >= 4.0.2.115` | 要求正数 `MasterId`，从 MySQL 读取 `SmuScanModel` 后新增扫描结果 |
| 更旧版本的 `Scan` | 用回包 `VList` / `IList` 和该实例最近的 `_lastScanParam` 生成临时结果，`Id=-1`；不是已持久化历史记录 |

扫描结果处理不执行点测的通道读数/Spectrum 同步逻辑。旧版本的 `_lastScanParam` 只有一个实例字段，不是按 `MsgID` 保存的扫描参数表；不能据此宣称并发扫描结果已可靠隔离。没有 MasterId、数据库记录缺失或处理异常，都可能造成“回包成功但无结果项”。

## 输出关闭的实际边界

手动点测完成后没有自动 `CloseOutput`。手动“关闭输出”按钮发送请求后立刻清空显示 V/I，没有等待关闭成功；空白读数不证明输出已关闭。

手动扫描的 `DisplaySMU.VIScan_Click` 在成功终态后等待约 1 秒，发 `CloseOutput` 并清空显示，再等待约 1 秒。这个关闭请求使用延迟结束时的当前显示通道；期间换通道可能改变关闭目标，并未捕获扫描开始通道。

失败或超时走非 `Success` 分支，没有自动关闭输出。代码中另有“`Success` 且 `Code!=0` 就关闭”的分支，但公共 `MQTTServiceBase` 已把非零 Code 归为 `Fail`，正常消息入口不能把它当作失败兜底。无响应、报错、取消等待或按钮恢复可用都不是安全断输出的证明；需要按现场规程确认目标通道的实际输出，不能由 AI 根据界面状态推定。

## Flow 的独立载荷与完成判据

`SMUNode` 自己持有通道、源/限值和量程。`SMUBaseNode.IsCloseOutput` 默认为 `false`，即未启用自动关闭输出；启用时其 `Reset` 路径延迟发送关闭请求，使用节点自身通道，不走手动 `CloseOutput()` 读取显示通道的逻辑。发送了关闭请求仍不等于收到了关闭成功确认。

`SMUSweepModelNode` 的 `Scan` 使用 `SMUSweepParam(模板名, IsCloseOutput)`，不同于手动扫描内联的 `DeviceParam`。不要把手动界面的限值检查、延迟关闭或最近模板字段套到该服务模板节点；流程整体终态见[Flow 执行会话](../workflow/execution.md)。

## 证据与验证缺口

`NodeConfiguratorBindingTests.PropertyEditors_BindAndFilterAdvancedProperties` 包含 SMU 量程编辑器的元数据、选项和属性绑定断言；`InitTableEntityMappingTests` 校验 `SmuScanModel.channel` 的枚举/数据库列映射。这些测试不覆盖实际点测、扫描、关闭输出、超时保护或数据库结果往返，本次也未运行产品测试。

当前没有在本主题声明 SMU 协议完成性或真机保护测试。后续验证须分别检查精确载荷、通道关联、失败/超时后的输出状态和结果持久化，且硬件及数据库操作需要单独授权。
