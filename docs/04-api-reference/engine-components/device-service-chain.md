# Engine 设备服务链路

这页说明设备服务从数据库资源到运行时 `DeviceService` 的链路。设备服务不是靠窗口手动 new 出来的，而是由数据库资源、`ServiceTypes`、`DeviceServiceFactoryRegistry` 和 `ServiceManager` 共同生成。

## 关键源码

| 源码 | 作用 |
| --- | --- |
| `Services/ServiceManager.cs` | 设备服务集合中心，加载终端、设备、组和标定资源 |
| `Services/DeviceService.cs` | 设备服务基类 |
| `Services/Devices/DeviceServiceFactory.cs` | 设备服务工厂注册表 |
| `Services/Devices/DeviceServiceConfig.cs` | 设备配置基类 |
| `Services/Type/TypeService.cs` | `ServiceTypes` 枚举和类型节点 |
| `Services/Terminal/`、`Services/Devices/<Device>/` | 终端节点和各类具体设备实现 |

## 资源层级

`ServiceManager.LoadServices()` 从数据库读取 `SysDictionaryModel` 和 `SysResourceModel` 后构建运行时树：

```text
SysDictionaryModel
  TypeService
    TerminalService              # pid = null 的终端资源
      DeviceService              # pid = terminal id 的启用设备资源
        GroupResource            # type = Group
        CalibrationResource      # type 30-50
        ServiceFileBase          # 其他设备下资源
```

`ServiceManager.GetInstance()` 是单例入口。初始化时如果 MySQL 已连接，会在 UI Dispatcher 上执行 `LoadServices()`；之后 `MySqlControl.GetInstance().MySqlConnectChanged` 触发时重新加载。

## 生成流程

| 步骤 | 行为 |
| --- | --- |
| 1 | 读取字典生成 `TypeService` |
| 2 | 读取 `pid = null` 的资源生成 `TerminalService` |
| 3 | 读取终端下启用设备资源 |
| 4 | 通过 `DeviceServiceFactoryRegistry.CreateService()` 生成 `DeviceService` |
| 5 | 加载 Group、Calibration 和 ServiceFileBase 子资源 |
| 6 | 由 `GenDeviceDisplayControl()` 写入 `DisPlayManager` |

设备缺失不一定是代码问题，也可能是 MySQL 未连接、资源被标记删除、资源未启用或资源层级不符合预期。

## 默认设备类型

| ServiceTypes | 设备类 | 配置类 |
| --- | --- | --- |
| `Camera` | `DeviceCamera` | `ConfigCamera` |
| `PG` | `DevicePG` | `ConfigPG` |
| `Spectrum` | `DeviceSpectrum` | `ConfigSpectrum` |
| `SMU` | `DeviceSMU` | `ConfigSMU` |
| `Sensor` | `DeviceSensor` | `ConfigSensor` |
| `FileServer` | `DeviceFileServer` | `ConfigFileServer` |
| `Algorithm` | `DeviceAlgorithm` | `ConfigAlgorithm` |
| `FilterWheel` | `DeviceCfwPort` | `ConfigCfwPort` |
| `Calibration` | `DeviceCalibration` | `ConfigCalibration` |
| `Motor` | `DeviceMotor` | `ConfigMotor` |
| `ThirdPartyAlgorithms` | `DeviceThirdPartyAlgorithms` | `ConfigThirdPartyAlgorithms` |
| `Flow` | `DeviceFlowDevice` | `ConfigFlowDevice` |

如果 `SysResourceModel.Type` 对应的 `ServiceTypes` 没有注册工厂，`CreateService()` 会返回 `null`，设备不会进入 `DeviceServices`。

## 显示和过滤

`LoadServices()` 读取字典后会跳过 `6, 11, 12, 13, 14, 15, 16, 17`，对应 FileServer、FocusRing、Flow、Archived、ThirdPartyAlgorithms、ThirdPartyAlgorithms32、PowerControl、LightingControl 等类型。枚举存在不代表左侧类型树一定显示。

显示区由 `GenDeviceDisplayControl()` 从 `TypeServices` 遍历设备，或由 `GenControl(ObservableCollection<DeviceService>)` 用指定集合生成。两者都会先放入 `FlowEngineManager.GetInstance().DisplayFlow`，再追加各设备的 `GetDisplayControl()`。设备树有设备但主区域没有页时，先查 `GetDisplayControl()`、`IDisPlayControl`、`GenDeviceDisplayControl()` 和 `RestoreControl()`。

## 新增设备

| 步骤 | 必做内容 |
| --- | --- |
| 类型 | 在 `ServiceTypes` 增加枚举值 |
| 配置 | 新增 `ConfigXxx : DeviceServiceConfig` |
| 服务 | 新增 `DeviceXxx : DeviceService<ConfigXxx>` |
| 工厂 | 注册到 `DeviceServiceFactoryRegistry` |
| UI | 需要显示页时实现 `GetDisplayControl()`，需要终端图标时设置 `terminalIconResourceKey` |
| Flow | 需要进流程节点时补 `Templates/Flow/NodeConfigurator/` |
| 文档 | 更新本页和用户设备文档 |

## 排查和禁区

| 现象 | 优先检查 |
| --- | --- |
| 类型树没有设备分类 | `SysDictionaryModel`、过滤类型、MySQL 连接 |
| 有终端但没有设备 | `SysResourceModel.Pid`、`IsEnable`、`IsDelete`、`TenantId` |
| 设备资源存在但不生成 | `ServiceTypes` 值和 `DeviceServiceFactoryRegistry` |
| 设备生成但没有显示页 | `GetDisplayControl()`、`IDisPlayControl`、`DisPlayManager` |
| 标定/组资源不显示 | 子资源 type 是否为 `Group` 或 30-50 |

不要在窗口代码里绕过 `ServiceManager` 手动维护全局设备集合；不要只新增菜单或窗口却不注册工厂；不要让底层设备类直接依赖客户项目包。
