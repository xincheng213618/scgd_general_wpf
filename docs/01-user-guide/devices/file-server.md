---
knowledge_id: "operations.file-server"
knowledge_type: "topic"
status: "current"
summary: "旧 FileServer 资源的类型值、装配过滤和 RC 协议兼容，以及相机、算法等设备当前文件保存配置的职责。"
aliases: ["文件服务器", "FileServer", "文件服务为什么不显示", "远程文件", "FileServerCfg", "FileSeviceConfig"]
code_paths: ["Engine/ColorVision.Engine/Services/Devices/DeviceServiceFactory.cs", "Engine/ColorVision.Engine/Services/ServiceManager.cs", "Engine/ColorVision.Engine/Services/Type/TypeService.cs", "Engine/ColorVision.Engine/Services/RC/MQTTRCService.cs", "Engine/ColorVision.Engine/Services/Cache/FileServerCfg.cs", "Engine/ColorVision.Engine/Services/PhyCameras/Configs/ConfigPhyCamera.cs", "Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalCameraCaptureService.cs", "Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalFrameFileService.cs", "Engine/ColorVision.Engine/Services/Devices/Algorithm/LocalAlgorithmResultDirectory.cs"]
test_paths: ["Test/ColorVision.UI.Tests/FileServerCompatibilityTests.cs", "Test/ColorVision.UI.Tests/LocalAlgorithmResultDirectoryTests.cs"]
related: ["engine.devices", "operations.device-configuration", "engine.mqtt", "operations.data", "delivery.file-transfer"]
---

# 旧 FileServer 资源与文件保存配置

历史资源可能仍使用 FileServer 类型，当前相机、算法、光谱和校准设备也使用文件保存配置。本页用于区分旧资源的装配与协议兼容、本地文件输出和远端文件服务状态。

## 遗留资源与协议兼容

`ServiceTypes.FileServer = 6` 和 RC 的 `CVServiceType.FileServer` 协议值继续保留。不要删除或重新编号这些枚举值，否则历史资源类型和服务协议可能不再对应。

`ServiceManager.LoadServices` 过滤 FileServer 类型分支。即使遗留 Type 为 6 的资源挂在其它可见类型的终端下，默认 `DeviceServiceFactoryRegistry.CreateService` 也因没有对应内置工厂而返回 null，跳过实例创建，不修改资源记录。通用装配规则见[设备服务链](../../04-api-reference/engine-components/device-service-chain.md)。RC 的服务查询与客户端设备实例装配分别处理，设备列表未显示资源不能证明远端服务已停止。

## 文件保存配置与输出

| 入口 | 当前职责 |
| --- | --- |
| `Services/Cache/FileServerCfg.cs` 的 `IFileServerCfg / FileServerCfg` | 相机、算法、光谱和校准配置中的数据保存设置，含 `DataBasePath`、`Endpoint`、`PortRange`、`SaveDays` |
| 物理相机的 `FileSeviceConfig` | 相机校正资源根目录及旧服务传输配置，含 `FileBasePath`、`Endpoint`、`PortRange` |
| `LocalFrameFileService.SaveCapture` | 使用调用方传入的数据基础路径，将本地采集输出保存到 `<根目录>/<设备 Code>/Data/yyyy-MM-dd`；相机采集调用方传入 `FileServerCfg.DataBasePath` |
| `LocalAlgorithmResultDirectory` | 未显式指定结果目录时，读取算法服务 `FileServerCfg.DataBasePath` 和 Code，确定当天结果目录 |
| Web“文件中转”（`/transfer`） | 独立的 HTTP 上传、断点续传和公开分享，见[文件中转](../../02-developer-guide/backend/file-transfer.md) |

数据保存、保留和清理职责见[数据管理](../data-management/README.md)；本地 CVRAW/CVCIE 格式读写见[CV 文件读写](../../04-api-reference/engine-components/ColorVision.FileIO.md)。

`FileServerCfg.Endpoint` 与 `FileServerCfg.PortRange` 是兼容已部署旧文件服务的传输配置。当前部署按本机文件路径运行，属性编辑器不显示这两个字段，但它们仍保留默认值并参与 JSON 序列化；不要用 `JsonIgnore` 或删除字段替代界面隐藏，否则旧服务收到缺失的 `Endpoint` 后可能无法完成设备初始化。物理相机的 `FileSeviceConfig` 同样保留 `FileBasePath`、`Endpoint = 127.0.0.1` 和 `PortRange` 的序列化；界面只显示文件路径。

`FileServerCfg.SaveDays` 保留为可读写的兼容字段，界面隐藏，读取和下发始终为 `0`，表示禁用设备服务按保存天数删除数据。旧配置中的正值（例如 3 或 365）加载后同样按 `0` 处理；保存和下发 JSON 时显式包含 `"SaveDays": 0`，即使序列化器忽略默认值也不省略。已核对的旧服务只在 `SaveDays > 0` 时按天清理，且会将大于 30 的值压回 30，因此不能用很大的天数替代禁用值。更新客户端后需重新下发设备配置，已运行服务的旧配置才会切换。流程执行时的容量清理继续由流程预处理负责；独立归档任务的清理策略不由此字段控制。

旧服务的正值按自然日目录计算：从 `<数据基础路径>/<设备 Code>/Data` 枚举子目录，将目录名解析为日期，与今天零点相减；日期差大于等于 `SaveDays` 时递归删除整个目录，不按文件写入时间等待完整的 24 小时倍数。设备初始化和运行期间跨天刷新目录时触发；例如周五下午的文件，周一零点起其日期目录已满足 3 天条件。

## 验证范围

`FileServerCompatibilityTests` 验证默认工厂跳过遗留 Type 为 6 的资源、保留原配置，并保持设备和 RC 的类型值为 6；同时覆盖旧保存天数加载后归零、显式序列化禁用值及属性编辑器隐藏保存天数。`LocalAlgorithmResultDirectoryTests` 覆盖算法结果目录继续读取数据基础路径；测试引用不表示已经执行或通过。

远端服务是否有文件传输调用、配置是否已生效以及现场文件保留行为，需要按实际部署另行核对。
