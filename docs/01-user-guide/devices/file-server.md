---
knowledge_id: "operations.file-server"
knowledge_type: "topic"
status: "current"
summary: "FileServer 工厂存在但默认类型树过滤；当前仅有配置与通用 MQTT 包装，未实现远端文件列表、上传或下载操作。"
aliases: ["文件服务器", "FileServer", "DeviceFileServer", "ConfigFileServer", "文件服务为什么不显示", "远程文件", "FileServerCfg"]
code_paths: ["Engine/ColorVision.Engine/Services/Devices/FileServer", "Engine/ColorVision.Engine/Services/Devices/DeviceServiceFactory.cs", "Engine/ColorVision.Engine/Services/ServiceManager.cs", "Engine/ColorVision.Engine/Services/Devices/MQTTDeviceService.cs", "Engine/ColorVision.Engine/Services/Core/MQTTServiceBase.cs", "Engine/ColorVision.Engine/Services/DeviceService.cs", "Engine/ColorVision.Engine/Services/RC/MQTTRCService.cs", "Engine/ColorVision.Engine/Services/Cache/FileServerCfg.cs"]
test_paths: []
related: ["engine.devices", "operations.device-configuration", "engine.mqtt", "operations.data"]
---

# FileServer 设备配置与实现边界

`DeviceFileServer` 是 `ServiceTypes.FileServer` 的设备包装，不是已完成的远端文件浏览器。当前类只创建通用 `MQTTDeviceService<ConfigFileServer>`、一个 `ImageView` 和配置编辑命令；没有实现远端列表、上传、下载、覆盖或删除动作，也没有专用文件操作回执。不能根据类名或协议库里存在文件类型，就回答“在这个设备页点击上传/下载”。

## 工厂存在不等于默认可见

`DeviceServiceFactoryRegistry.RegisterDefaults` 为 `ServiceTypes.FileServer = 6` 注册了配置与实例工厂；但 `ServiceManager.LoadServices` 构建 `TypeServices` 时明确过滤 FileServer。终端树再从这个过滤后的集合生成，因此正常加载路径不会建立它自己的 FileServer 类型分支。

这不等于删除了工厂，也不能扩大成“任何调用路径都无法构造实例”。若某个扩展主动创建该类，仍需核对扩展如何加入资源树、显示区和生命周期。不要通过写数据库、伪造类型或移除过滤条件来验证文档。通用资源加载条件见[设备服务链](../../04-api-reference/engine-components/device-service-chain.md)。

`GetDeviceInfo()` 返回空 `UserControl`；类中的 `View` 虽是 `ImageView`，没有被本类接成文件浏览/操作面板。创建实例会构造图像编辑器并让 DService 订阅共享 MQTT 接收事件，不是完全无副作用的数据对象。

## 配置的含义与保存副作用

`ConfigFileServer` 在通用设备身份、主题、令牌等字段之外增加 `Endpoint`、`PortRange`、`FileBasePath`。新建配置工厂设置默认 Endpoint 为 `127.0.0.1`、FileBasePath 为 `D:\CVTest`，并以 `Random.Shared.Next(6500, 6599)` 产生起始端口、加 5 得到范围终点。这些是工厂默认值，不证明远端目录存在、端口可用或网络权限已配置；直接构造配置对象、加载已存 JSON 也不能套用工厂新建默认值。

本类没有把上述三个字段交给本地文件传输客户端；它们随设备配置持久化，服务端是否接受和如何解释必须核对实际部署的服务实现。不要把 `FileBasePath` 当成本地探测、创建或清理目录的授权。

配置编辑使用管理员门禁和 `PropertyEditorEditMode.Transactional`；确认后通过 `Submitted` 调用继承的 `Save()`。**该动作会更新数据库资源并请求 RC 重启设备服务，不是纯本地表单保存。** 通用“重启服务”命令也进入 `Save()`。必须先确认目标设备、影响范围和重启授权；精确持久化链见[设备配置契约](./configuration.md)。

保存路径不是数据库与远端重启的整体事务：`RestartServices(nodeType, svrCode, devCode)` 不等待服务完成重启，RC 未连接或无可用 token 时内部可返回 `false`，该 void 包装不把结果交回编辑窗口。因此窗口关闭或数据库更新不能证明远端已应用配置。

## 三种“文件服务”概念不要混用

| 入口 | 当前职责 |
| --- | --- |
| `DeviceFileServer / ConfigFileServer` | 本页的设备类型与配置包装；没有文件传输 UI/API |
| `Services/Cache/FileServerCfg.cs` 的 `IFileServerCfg / FileServerCfg` | 部分相机、算法等配置持有的数据保存设置，含 `DataBasePath`、`Endpoint`、`PortRange`、`SaveDays` |
| 基类 `ExportCommand / ImportCommand` | 本地设备配置 JSON 的导出/导入；不是远端结果文件下载/上传 |

`ConfigFileServer` 本身不实现 `IFileServerCfg`；基类 `UpdateFilecfgCommand` 的可执行条件正是这个接口，不能因设备名含 FileServer 就推断它提供“文件保存路径”编辑入口。实际数据保存、保留或清理行为应沿配置的消费方核对，见[数据管理](../data-management/README.md)。

配置导出会写入所选本地 `.config` 文件；导入读取 JSON 后复制进当前配置并调用 `Save()`，因此还可能请求远端重启。只读诊断不执行导入、导出或真实上传来试探权限。

## 消息、失败与生命周期缺口

DService 使用通用 `MQTTServiceBase`，本类没有安装文件专属 `MsgReturnReceived` 处理器。基类接收先比较订阅主题，再按 `MsgID` 匹配待处理记录，以 `Code == 0` 标记该记录成功；不能把它说成已经校验文件内容、设备代码、事件类型或传输完整性。本页更没有可据此等待的“上传完成”状态机。通用通信边界见[MQTT](../../02-developer-guide/engine-development/mqtt.md)。

这两个层次还要区分：`MQTTServiceBase.Dispose()` 能解绑接收事件并清理请求计时器；但 `DeviceFileServer` 没有重写 `Dispose()`，当前设备基类的实现不会代它释放 DService 或 View。因此若扩展实际实例化本类，需要额外核对所有者的清理路径，不能宣称设备树刷新已保证释放这些资源。

## 验证入口与未覆盖范围

本页 `test_paths` 为空：未声明专用的 FileServer 工厂过滤、配置应用、文件协议或生命周期自动化覆盖。排查首先只读核对：

- 默认类型树过滤、数据库资源的类型/父子关系及真正的实例创建入口。
- 当前 `ConfigFileServer` 与设备资源中的代码、主题、Endpoint 和路径是否对应目标服务。
- 保存是否停在数据库更新、RC 连接/token 检查或实际服务重启阶段；不要用普通消息成功替代业务完成。
- 所求文件操作是否由另一个模块或外部服务实现，先找到实际入口，再确定协议、覆盖范围和验收方式。

远端服务实现、真实网络/目录权限、重启后配置应用与文件操作均未由本页证明。需要功能验证时，先取得相应运行与写入授权，并使用隔离、非敏感数据。
