---
knowledge_id: "operations.smu"
knowledge_type: "topic"
status: "current"
summary: "SMU本地与服务模式的参数、A/B通道、结果单位、Flow及输出释放边界；SDK成功或空读数不能单独证明实机输出关闭。"
aliases: ["SMU","源表","本地源表","UseLocalSmu","点测","扫描电压","通道串了","关闭输出","MQTTSMU","SMUParam","SMUSweepModelNode"]
code_paths: ["Engine/ColorVision.Engine/Services/Devices/SMU","Engine/cvColorVision/Devices/PassSx/PassSx.cs","Engine/ColorVision.Engine/FlowProcessing/Nodes/Compatibility/SMU","Engine/FlowEngineLib/SMUBaseNode.cs","Engine/FlowEngineLib/Node/SMU"]
test_paths: ["Test/ColorVision.UI.Tests/LocalSmuSessionTests.cs","Test/ColorVision.UI.Tests/LocalSmuFlowTests.cs","Test/ColorVision.UI.Tests/InitTableEntityMappingTests.cs"]
related: ["engine.devices","operations.device-configuration","flow.session"]
---

# SMU 参数、结果与输出关闭

`DeviceSMU` 的手动点测、手动扫描和 Flow 节点有不同的参数来源与完成处理。查询“通道串了”“扫描不使用当前值”“失败后是否关闭输出”时，必须先区分这三条调用路径。

点测和扫描会向被测件施加电压/电流。执行前由现场操作者确认接线、极性、限压/限流、额定范围与授权。客户端限制开关或成功回包不能替代仪器保护；只读问答、文档或代码修改不授权通电、扫描或试验保护动作。

## 哪一层拥有参数

- `ConfigSMU` 拥有设备类型、设备名和连接等设备配置，设置提交走公共[设备配置保存](./configuration.md)；保存不等于远端已经应用。
- `DisplaySMUConfig` 由 `DisplayConfigManager` 按设备 `Config.Code` 管理。A/B 通道分别持有电压源、电流源的测量值/限值以及显示读数；`CurrentSourceConfig` 由当前 `Channel` 和 `IsSourceV` 选出。切换通道或源类型是在切换参数组，不是发送测量命令。
- `TemplateSMUParam` / `SMUParam` 是数据库模板参数，模板字典 ID 为 `13`。手动扫描读取模板的源类型、起点、终点、点数与限值，但通道显式取自提交时的 `DisplayConfig.Channel`，不是 `SMUParam.Channel`。服务模式载荷保持原有字段，本地模式还使用模板的 `IsAutoRng` / `SrcRng` / `LmtRng`。
- Flow 节点使用节点/服务模板自己的参数，不继承手动控件选择；量程、通道与关闭输出设置须按具体节点核对。

显示配置中的值可能来自之前输入或之前读回，不能作为当前仪器输出状态的证据。

## 本地源表的连接与执行

在源表设备面板勾选“使用本地源表”，设置 `ConfigSMU` 的设备类型、`DevName`、`IsNet`、四线制/前后端和延时，再打开连接。本地资源默认启用此模式；既有服务设备保持原设置。模式选择保存在显示配置中，连接占用期间保持当前后端，关闭后才能切换。编辑连接配置不会修改已打开的 SDK 连接，测量时发现配置变化会要求先关闭再重开。

`LocalSmuNative` 使用随应用交付的 x64 `cvCamera.dll` 源表导出和既有 `PassSx` 绑定。打开后读取仪器标识；“点亮”调用普通测量，“设置”调用步进测量。每次操作先选择提交时的 A/B 通道和源类型，手动扫描在同一次串行操作内关闭该通道输出，成功确认关闭后才完成命令。所有 SDK 调用在后台执行，同一连接按顺序执行；不会用延迟后台任务关闭后续测量的输出。

参数中的电流源值和限流值使用 mA，进入 SDK 时转换为 A；电压使用 V。会话内部点测/读取/扫描电流统一为 mA。为兼容既有结果表、视图和消息，点测结果 `I` / `SMUResultModel.IResult` 保持 mA，扫描 `IList` / `SmuScanModel.IResult` 保持 A，电流源扫描的 `ScanList` 也使用 A。手动量程参数按既有 SDK/服务约定直接传递，不做源值单位转换。旧服务 `GetMeasureResult` 的电流换算与点测不一致，本地读取按 SDK 的 A 转 mA；真机读取单位仍需单独核对。

本地校验拒绝无效通道、非有限源值/限值、零限值、无效手动量程，以及不在 2～100000 范围内的扫描点数。启用 `IsUseLimitSigned` 时使用既有机型限值，并检查扫描两个端点的最大绝对值。失败通过 `MsgRecord` 返回，校验失败不会调用测量 SDK。

本地结果直接进入现有源表结果视图；已连接 MySQL 时使用现有结果表持久化，否则为内存结果，`MasterId=0`，不作为历史数据库记录。点测只更新结果所属通道，只有它仍为当前显示通道才同步 Spectrum 的 V/I；扫描不执行这一点测同步。数据库保存失败会报告命令失败，已采集的数据仍可供查看。

连接、输出和端点占用由 `LocalSmuSession` 持有。关闭/释放取消未开始的排队操作；已进入原生 SDK 的调用必须等其返回，随后关闭相关输出和连接，不能提前复用句柄或声明取消已完成。释放失败时状态为未知，并保留端点占用，避免另一会话立即重开；诊断和实机输出确认仍由现场操作承担。本应用内相同设备名/连接类型不能同时由多个本地会话占用，已观察到的服务占用也阻止切换；无法由此检测其它程序或另一台机器的连接。

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

手动点测完成后没有自动 `CloseOutput`。服务模式“关闭输出”按钮发送请求后立刻清空显示 V/I，没有等待关闭成功；本地模式在 SDK 确认成功后才清空对应通道读数。空白读数仍不证明实机输出已关闭。

服务模式手动扫描的 `DisplaySMU.VIScan_Click` 在成功终态后等待约 1 秒，向提交扫描时捕获的通道发 `CloseOutput` 并清空显示，再等待约 1 秒。本地模式由会话在扫描操作内关闭输出，不走此延迟请求。

服务模式失败或超时走非 `Success` 分支，没有自动关闭输出。代码中另有“`Success` 且 `Code!=0` 就关闭”的分支，但公共 `MQTTServiceBase` 已把非零 Code 归为 `Fail`，正常消息入口不能把它当作失败兜底。本地采集异常会尝试关闭该操作的输出，主动关闭连接会等待原生调用返回后释放；SDK 释放失败会报告失败。无响应、报错、取消等待或按钮恢复可用都不是安全断输出的证明；需要按现场规程确认目标通道的实际输出。

## Flow 的独立载荷与完成判据

`SMUNode` 自己持有通道、源/限值和量程。`SMUBaseNode.IsCloseOutput` 默认为 `false`，即未启用正常结束自动关闭输出；启用时，服务模式 `Reset` 延迟发送关闭请求，本地模式直接执行串行关闭，两者使用节点通道。发送了关闭请求或 SDK 返回成功仍不等于实机输出确认。

`SMUSweepModelNode` 的 `Scan` 使用 `SMUSweepParam(模板名, IsCloseOutput)`，不同于手动扫描内联的 `DeviceParam`。不要把手动界面的限值检查、延迟关闭或最近模板字段套到该服务模板节点；流程整体终态见[Flow 执行会话](../workflow/execution.md)。

选择本地后端时，现有点测/CSV/模板循环、扫描/模板扫描、结果读取及物理设备打开/关闭/重开节点在进程内执行，不发布 MQTT 请求；可自动打开已配置的本地源表。模板取自已加载的 `TemplateSMUParam.Params`，要求 ID 或名称唯一，参数在提交时快照。内联 `DeviceParam` 优先于模板。`SMU.MeasureResult` 读取最近使用通道及源类型，并按节点等待时间延迟结果转交。流程沿用批次、节点 ZIndex 和 `PersistResults`，保存正数结果 ID 后发布现有结果消息。

流程停止或超时后拒绝迟到结果；清理被丢弃结果时检查该次测量的输出归属，不关闭同通道后来开始的测量。SDK 没有可中断接口，超时后的原生调用仍要等返回再完成清理，不能把流程终态当作硬件释放已完成。

## 证据与验证缺口

`NodeConfiguratorBindingTests.PropertyEditors_BindAndFilterAdvancedProperties` 包含 SMU 量程编辑器的元数据、选项和属性绑定断言；`InitTableEntityMappingTests` 校验 `SmuScanModel.channel` 的枚举/数据库列映射。这些测试不覆盖实际点测、扫描、关闭输出、超时保护或数据库结果往返。

`LocalSmuSessionTests` 使用模拟 SDK 验证单位、扫描结果契约、通道关闭顺序、句柄 0、端点互斥、取消与释放失败；`LocalSmuFlowTests` 验证既有节点本地完成和循环结束清理不发布 MQTT。这些回归保护共享单位和生命周期边界，不替代仪器、供应商 SDK 或 MySQL 往返验收。

当前没有在本主题声明真机点测、扫描、读取单位或输出保护验证。后续验证须分别检查 SDK 返回码、精确载荷、通道关联、失败/超时后的实际输出状态和结果持久化，且硬件及数据库操作需要单独授权。
