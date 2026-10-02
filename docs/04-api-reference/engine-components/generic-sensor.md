---
knowledge_id: "engine.sensor-device"
knowledge_type: "topic"
status: "current"
summary: "本地 TCP/串口通用指令设备、回包分帧、模板持久化和旧传感器结点转发；明确打开、模板命令超时、关闭取消和服务占用边界。"
aliases: ["本地通用传感器", "通用传感器模板", "PG串口指令", "控制器TCP指令", "LocalSensorNode"]
code_paths: ["Engine/ColorVision.Engine/Services/Devices/Sensor", "Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalSensorNode.cs"]
test_paths: ["Test/ColorVision.UI.Tests/LocalSensorSessionTests.cs", "Test/ColorVision.UI.Tests/LocalSensorFlowTests.cs"]
related: ["engine.devices", "engine.mqtt", "flow.runtime", "ui.database", "ui.property-grid"]
---

# 本地通用传感器与模板

通用传感器与本地相机共用“显示设置”入口；“使用本地通用传感器”、连接超时及串口参数统一放在 `DisplaySensorConfig`，通过 `DisplayConfigManager` 保存到本机配置文件，不写入设备数据库。本地资源默认启用，服务器资源默认沿用服务通信。`DeviceSensor.Local.cs` 负责入口和状态，`LocalSensorSession` 持有单一连接并串行执行整组模板。切换开关只影响下一次打开，当前连接要先关闭。打开和重新打开是明确的操作；执行模板、关闭、通信中断后的下一条指令都不会尝试自动打开。连接超时取本机显示设置的 `ConnectTimeout`（默认 3000 ms），属于建立连接阶段；发送及回包共用模板 `Timeout`，结点不再另设本地操作超时。模板 `Delay` 是每条指令成功后的等待，排队与延时仍可被关闭或流程停止取消。地址、端口或波特率仍取原设备配置，本地打开时将它们与本机的数据位、校验位、停止位、DTR/RTS 设置合成连接快照。

`LocalSensorNode` 在本地结点中提供执行模板、打开、关闭、重新打开四种操作。旧 `CommonSensorNode`、`TempCommonSensorNode`、`RealCommonSensorNode` 和选择 `GeneralSensor` 的 `PhyDeviceControlNode` 在设备启用本地模式时转到同一会话；模板结点沿用模板，旧指令结点沿用其指令参数。旧结点转发时跳过原结点的等待超时，远程模式保留原 MQTT 路径。`FlowLocalExecution` 按后端注册，传感器与光谱仪可以同时使用；流程结束时共享的 `FlowRuntimeResources.StopToken` 取消正在进行的传感器操作。

本地回包按实际收到的字节累积，按原模板预期回包的字节长度收齐后完全匹配，避免将第一段数据当成完整回包。未配置预期回包时接收到模板超时结束，有数据才算成功；超时小于等于零的指令作为仅发送使用。文本支持 ASCII、UTF8、GBK 和旧 UTF7，Hex 使用十六进制字节；单次回包最多 1 MiB。超时、最终回包不符或断线会关闭连接，防止残留回包进入下一条指令；错误保留指令名和已接收数据。完整回包不匹配时按原模板 `RetryCount` 最大尝试次数重发，小于 1 时执行一次；所有尝试共用一次模板超时。

本地执行直接读取现有 MySQL 传感器模板；数据库结构、导入导出及 `ValueA` / `ValueB` 写入沿用原实现，不迁移模板，也不建立另一套本地模板库。编辑器使用单指令表单，打开模板默认显示第一条；多条指令全部保留，通过下拉框切换，新增后定位到新指令。括号格式转换工具默认折叠，原字段含义不变。`SensorTemplateRepository` 按设备类别和模板 ID 或名称读取原指令，`LocalSensorCommand` 解析既有的发送、返回、编码、超时/延时、重试次数字段；旧的一至四字段格式使用原服务默认值。执行模板需要连接 MySQL，旧指令结点的直接指令参数不依赖模板库。会话阻止重复本地地址占用，设备入口也检查已知服务占用；服务离线不代表服务已释放连接，已知占用须先由服务关闭。此约束基于本进程会话及服务上报，不能检测其他程序占用的 TCP 连接。

`LocalSensorSessionTests` 和 `LocalSensorFlowTests` 覆盖分包、字节编码、关闭取消、超时隔离、旧模板解析、本机配置存储与旧结点转发边界；TCP 回环验证不代表真实设备和串口驱动验收。
