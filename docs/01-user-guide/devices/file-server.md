---
knowledge_id: "operations.file-server"
knowledge_type: "topic"
status: "current"
summary: "客户端 FileServer 设备包装与工厂已移除；类型编号、相机和算法的文件保存配置及旧服务传输字段保持兼容。"
aliases: ["文件服务器", "FileServer", "DeviceFileServer", "ConfigFileServer", "文件服务为什么不显示", "远程文件", "FileServerCfg", "FileSeviceConfig"]
code_paths: ["Engine/ColorVision.Engine/Services/Devices/DeviceServiceFactory.cs", "Engine/ColorVision.Engine/Services/ServiceManager.cs", "Engine/ColorVision.Engine/Services/Type/TypeService.cs", "Engine/ColorVision.Engine/Services/DeviceService.cs", "Engine/ColorVision.Engine/Services/RC/MQTTRCService.cs", "Engine/ColorVision.Engine/Services/Cache/FileServerCfg.cs", "Engine/ColorVision.Engine/Services/PhyCameras/Configs/ConfigPhyCamera.cs", "Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalFrameFileService.cs", "Engine/ColorVision.Engine/Services/Devices/Algorithm/LocalAlgorithmResultDirectory.cs"]
test_paths: ["Test/ColorVision.UI.Tests/FileServerCompatibilityTests.cs", "Test/ColorVision.UI.Tests/LocalAlgorithmResultDirectoryTests.cs"]
related: ["engine.devices", "operations.device-configuration", "engine.mqtt", "operations.data", "delivery.file-transfer"]
---

# FileServer 移除与文件保存配置边界

客户端不再提供 `DeviceFileServer`、`ConfigFileServer` 或对应内置工厂。原包装只有通用 MQTT 服务、配置编辑和未接入显示区的 `ImageView`，没有实现独立文件浏览、上传或下载界面；移除该包装不等于停用服务端文件服务，也不删除数据库资源、配置或磁盘文件。

## 遗留资源与协议兼容

`ServiceTypes.FileServer = 6` 和 RC 的 `CVServiceType.FileServer` 协议值继续保留。不要删除或重新编号这些枚举值，否则历史资源类型和服务协议可能不再对应。

`ServiceManager.LoadServices` 仍过滤 FileServer 类型分支。即使遗留 Type 为 6 的资源挂在其它可见类型的终端下，默认 `DeviceServiceFactoryRegistry.CreateService` 也返回 null，跳过实例创建而不修改资源记录。通用装配规则见[设备服务链](../../04-api-reference/engine-components/device-service-chain.md)。旧二进制扩展若直接引用已删除的两个公开类型，需要更新扩展；保留类型编号不意味着继续保留这些 CLR 类型。

## 仍在使用的文件配置

| 入口 | 当前职责 |
| --- | --- |
| `Services/Cache/FileServerCfg.cs` 的 `IFileServerCfg / FileServerCfg` | 相机、算法、光谱和校准配置中的数据保存设置，含 `DataBasePath`、`Endpoint`、`PortRange`、`SaveDays` |
| 物理相机的 `FileSeviceConfig` | 相机校正资源根目录及旧服务传输配置，含 `FileBasePath`、`Endpoint`、`PortRange` |
| `LocalFrameFileService.SaveCapture` | 读取设备数据基础路径，将本地采集输出保存到 `<根目录>/<设备 Code>/Data/yyyy-MM-dd` |
| `LocalAlgorithmResultDirectory` | 未显式指定结果目录时，读取算法服务 `FileServerCfg.DataBasePath` 和 Code，确定当天结果目录 |
| Web“文件中转”（`/transfer`） | 独立的 HTTP 上传、断点续传和公开分享，见[文件中转](../../02-developer-guide/backend/file-transfer.md) |

这些入口不依赖被移除的 `DeviceFileServer`，不能按名称把文件保存配置、本地文件读写或文件类型枚举一并删除。实际数据保存、保留和清理职责见[数据管理](../data-management/README.md)；本地 CVRAW/CVCIE 格式读写见[CV 文件读写](../../04-api-reference/engine-components/ColorVision.FileIO.md)。

`FileServerCfg.Endpoint` 与 `FileServerCfg.PortRange` 是兼容已部署旧文件服务的传输配置。当前部署按本机文件路径运行，属性编辑器不显示这两个字段，但它们仍保留默认值并参与 JSON 序列化；不要用 `JsonIgnore` 或删除字段替代界面隐藏，否则旧服务收到缺失的 `Endpoint` 后可能无法完成设备初始化。物理相机的 `FileSeviceConfig` 同样保留 `FileBasePath`、`Endpoint = 127.0.0.1` 和 `PortRange` 的序列化；界面只显示文件路径。

设备基类的“文件保存路径”编辑仍按 `IFileServerCfg` 判断是否可用；发生变化后调用 `Save()`，可能请求 RC 重启对应设备服务。移除 FileServer 包装不会改变相机或算法配置的保存副作用，具体契约见[设备配置](./configuration.md)。

## 验证范围

`FileServerCompatibilityTests` 验证默认工厂跳过遗留 Type 为 6 的资源、保留原配置，并保持设备和 RC 的类型值为 6。`LocalAlgorithmResultDirectoryTests` 覆盖算法结果目录继续读取数据基础路径；测试引用不表示已经执行或通过。

运行服务是否仍有文件传输调用、外部插件是否引用旧类型以及现场文件保留行为，需要按实际部署另行核对。客户端包装移除或默认设备树中没有 FileServer，都不能证明服务端文件功能已退役。
