# Engine 服务开发交接手册

本页说明 `Engine/ColorVision.Engine/Services/` 里真实存在的设备服务开发模型。这里的“服务”不是通用 DI 服务，而是主程序运行时可见、可配置、可显示、可通过 MQTT 发命令的设备或业务服务。

第一次接手先读 [Engine 设备服务链路](../../04-api-reference/engine-components/device-service-chain.md)，再用本页定位开发落点。

## 当前服务链路

| 阶段 | 关键对象 | 说明 |
| --- | --- | --- |
| 服务类型 | `ServiceTypes` | 定义服务类型编号，当前包含 Camera、PG、Spectrum、SMU、Sensor、FileServer、Algorithm、FilterWheel、Calibration、Motor、Flow、ThirdPartyAlgorithms 等 |
| 配置来源 | `SysResourceModel.Value` | 保存设备配置 JSON，`DeviceService<T>` 构造时反序列化到具体 `Config*` |
| 实例创建 | `DeviceServiceFactoryRegistry` | 按 `SysResourceModel.Type` 找到工厂并创建具体 `Device*` |
| 运行集合 | `ServiceManager.GetInstance().DeviceServices` | 主程序里当前已经加载的设备服务集合 |
| UI 入口 | `GetDeviceInfo()`、`GetDisplayControl()` | 信息页、控制面板、设备树和属性窗口 |
| 命令链路 | `GetMQTTService()`、`MQTTDeviceService<T>` | 设备命令、返回、超时和消息记录 |

典型加载顺序是：

1. 数据库或配置里保存 `SysResourceModel`。
2. `ServiceManager.Load()` 遍历资源。
3. `DeviceServiceFactoryRegistry.CreateService(sysResourceModel)` 创建具体服务。
4. `DeviceService<T>` 读取 `SysResourceModel.Value`，恢复 `Config`、`Code`、`Name`。
5. 具体 `Device*` 创建对应 `MQTT*` 服务，并提供 `Info*` / `Display*` 控件。
6. 设备树、流程节点、项目包或模板窗口从 `ServiceManager.DeviceServices` 里选择服务。

## 当前默认注册项

| 类型 | 目录 | 设备类 | MQTT 类 | 主要职责 |
| --- | --- | --- | --- | --- |
| Camera | `Services/Devices/Camera/` | `DeviceCamera` | `MQTTCamera` | 相机、实时画面、拍图、曝光和校准相关命令 |
| PG | `Services/Devices/PG/` | `DevicePG` | `MQTTPG` | 图案发生器切图、PG 参数和项目切图联动 |
| Spectrum | `Services/Devices/Spectrum/` | `DeviceSpectrum` | `MQTTSpectrum` | 光谱仪连接、暗电流、测量和光谱数据 |
| SMU | `Services/Devices/SMU/` | `DeviceSMU` | `MQTTSMU` | 电源表、扫描、结果读取和部分光谱联动 |
| Sensor | `Services/Devices/Sensor/` | `DeviceSensor` | `MQTTSensor` | 串口/网络传感器命令和模板化指令 |
| FileServer | `Services/Devices/FileServer/` | `DeviceFileServer` | `MQTTFileServer` | 文件路径、下载、缓存和文件服务命令 |
| Algorithm | `Services/Devices/Algorithm/` | `DeviceAlgorithm` | `MQTTAlgorithm` | 算法服务调用、结果查询和算法视图 |
| FilterWheel | `Services/Devices/CfwPort/` | `DeviceCfwPort` | `MQTTCfwPort` | 滤光轮端口和位置控制 |
| Calibration | `Services/Devices/Calibration/` | `DeviceCalibration` | `MQTTCalibration` | 校准命令、文件和校准结果 |
| Motor | `Services/Devices/Motor/` | `DeviceMotor` | `MQTTMotor` | 电机回零、移动、位置读取和光阑控制 |
| Flow | `Services/Devices/FlowDevice/` | `DeviceFlowDevice` | `MQTTFlowDevice` | 流程设备服务 |
| ThirdPartyAlgorithms | `Services/Devices/ThirdPartyAlgorithms/` | `DeviceThirdPartyAlgorithms` | `MQTTThirdPartyAlgorithms` | 第三方算法接入 |

## 新增设备服务步骤

1. 确认是不是已有 `ServiceTypes` 可以复用。只有现场设备类型真的不同，才新增枚举值。
2. 新建 `Config* : DeviceServiceConfig`，字段要能从旧 `SysResourceModel.Value` 反序列化。
3. 新建 `Device* : DeviceService<Config*>`，构造时创建 `DService = new MQTT*(Config)`。
4. 覆盖 `GetDeviceInfo()`，必要时覆盖 `GetDisplayControl()` 和 `GetMQTTService()`。
5. 新建 `MQTT* : MQTTDeviceService<Config*>`，只封装设备命令，不写客户判定。
6. 在 `DeviceServiceFactoryRegistry.RegisterDefaults()` 注册 `DeviceServiceFactory<Config*>`。
7. 如需从终端创建，确认 `DeviceServiceCreateContext` 的 `Code`、`Name`、`SendTopic`、`SubscribeTopic` 会写入配置。
8. 更新使用手册、设备服务链路、模块对照表和测试验收记录。

## 变更边界

| 变更 | 应该改哪里 | 不应该改哪里 |
| --- | --- | --- |
| 新设备类型 | `ServiceTypes`、`DeviceServiceFactoryRegistry`、`Services/Devices/<Name>/` | 项目包里的 `Process` |
| 新设备命令 | 对应 `MQTT*` 方法和消息参数 | 通用 `MQTTControl` |
| 新设备配置字段 | `Config*` 和设备属性页 | `SysResourceModel` 表结构，除非确实需要迁移 |
| 新显示窗口 | `Info*`、`Display*`、View 配置 | 算法结果 handler |
| 项目客户判定 | 项目包 `Process`、Recipe、Fix、导出器 | 设备服务层 |

## 验收清单

| 项目 | 验收方式 |
| --- | --- |
| 注册 | 启动主程序，设备树能看到新服务类型或已有服务仍能恢复 |
| 配置 | 导出、导入、重置、保存重开后配置不丢失 |
| MQTT | `SendTopic` / `SubscribeTopic` 正确，命令返回能匹配 `MsgID` |
| UI | 属性页、信息页、显示控件打开不抛异常 |
| Flow/项目 | 依赖该服务的流程节点或项目包能选中正确设备 |
| 日志 | 超时、失败返回和异常有 `MsgRecord` 或 log4net 记录 |

## 相关文档

- [Engine 设备服务链路](../../04-api-reference/engine-components/device-service-chain.md)
- [MQTT 消息处理](./mqtt.md)
- [Engine 运行时对象目录](../../04-api-reference/engine-components/runtime-object-map.md)
- [测试与验证交接手册](../testing.md)
