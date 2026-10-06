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

## 验证范围

`FileServerCompatibilityTests` 验证默认工厂跳过遗留 Type 为 6 的资源、保留原配置，并保持设备和 RC 的类型值为 6。`LocalAlgorithmResultDirectoryTests` 覆盖算法结果目录继续读取数据基础路径；测试引用不表示已经执行或通过。

远端服务是否有文件传输调用、配置是否已生效以及现场文件保留行为，需要按实际部署另行核对。
