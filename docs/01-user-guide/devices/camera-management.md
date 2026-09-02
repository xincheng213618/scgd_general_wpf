---
knowledge_id: "operations.physical-camera"
knowledge_type: "topic"
status: "current"
summary: "物理相机的扫描、创建、许可证、校正资源和还原点入口；区分扫描结果与缓存列表，创建/导入在唯一物理相机时可批量绑定服务。"
aliases: ["物理相机","相机管理","相机许可证","导入lic","唯一相机自动绑定","校准文件上传","恢复点","PhyCameraManager","PhyCamera","SearchCameraIds","SetLicense","CreateRestore","LoadResotre","UploadDataAsync","扫描在线相机","添加未创建的相机","上传校正文件","创建还原点","加载还原点","CameraSearchResultViewModel","PhysicalCamera_Load"]
code_paths: ["Engine/ColorVision.Engine/Services/PhyCameras/PhyCameraManager.cs","Engine/ColorVision.Engine/Services/PhyCameras/PhyCamera.cs","Engine/ColorVision.Engine/Services/PhyCameras/PhyCameraRestoreArchive.cs","Engine/ColorVision.Engine/Services/PhyCameras/CalibrationUploadRunner.cs","Engine/ColorVision.Engine/Services/PhyCameras/CalibrationUploadWorkspace.cs","Engine/ColorVision.Engine/Services/PhyCameras/PhyCameraManagerWindow.xaml","Engine/ColorVision.Engine/Services/PhyCameras/PhyCameraManagerWindow.xaml.cs","Engine/ColorVision.Engine/Services/PhyCameras/CameraSearchTypeWindow.xaml.cs","Engine/ColorVision.Engine/Services/PhyCameras/CameraSearchResultWindow.xaml.cs","Engine/ColorVision.Engine/Services/PhyCameras/CreateWindow.xaml.cs","Engine/ColorVision.Engine/Services/PhyCameras/InfoPhyCamera.xaml","Engine/ColorVision.Engine/Services/RC/RCFileUpload.cs","Engine/cvColorVision/Camera/cvCameraCSLib.Discovery.cs"]
test_paths: ["Test/ColorVision.UI.Tests/PhyCameraRestoreArchiveTests.cs","Test/ColorVision.UI.Tests/CalibrationUploadRunnerTests.cs","Test/ColorVision.UI.Tests/CalibrationUploadWorkspaceTests.cs"]
related: ["operations.camera","operations.camera-configuration","operations.calibration","engine.devices"]
---

# 物理相机发现、许可证与资源管理

从“工具 > 物理相机管理”打开窗口，可扫描相机、创建物理资源，并维护许可证、校正文件和还原点。管理窗口读取业务数据库，扫描会调用相机 SDK；创建、导入和恢复还可能写配置、文件及关联服务，应在已明确目标和操作范围的环境执行。采集步骤与完成判据见[相机服务](./camera.md)，参数编辑和同步覆盖见[相机配置](./camera-configuration.md)。

## 扫描相机并创建资源

1. 点击“扫描在线相机”，选择要搜索的型号。默认勾选 `QHY_USB`、`HK_USB`、`HK_CARD`、`HK_FG_CARD`，也可用“全选”“清除”调整。未选择型号不能开始，取消型号窗口不会扫描。
2. 点击“搜索”，在结果窗口先查看各型号的数量、耗时和状态。SDK 返回成功但数量为 0，与型号搜索失败是两种结果；某个型号失败不会阻止其它已选型号继续搜索。搜索进行中再次发起会被拒绝。
3. 在下方相机列表核对 `CameraID`、MD5 和识别型号。托管发现按“型号 + CameraID”去重，结果窗口又将相同 MD5 的条目合并，所以各型号数量之和可能大于窗口的相机总数。
4. 对未创建的条目点击“创建”，在创建窗口核对代码、ID、型号与物理参数，再确认。该入口预填扫描结果，不要求数据库中事先已有空配置候选。确认会插入或更新物理资源，并进入下文的目录请求和关联流程。

工具栏的“添加未创建的相机”走另一入口：先查 `Type == 101` 且 `Value` 为空的数据库候选，有候选才打开创建窗口；没有则提示并转入扫描。它不等同于扫描结果行内的创建。`CreateWindow` 提交后没有按数据库返回的影响行数阻止后续流程；窗口关闭或行显示已创建，不能代替对资源和绑定结果的核对。

## 管理列表与扫描结果的含义

`PhyCameraManager.LoadPhyCamera` 查询 MySQL 中的 `ServiceTypes.PhyCamera` 资源，有非空配置的记录才新建物理对象。`PhyCamera` 持有配置、许可证、校准资源和逻辑设备关联。

已有 ID 会复用原对象，只更新名称、资源模型以及物理配置中的 `CameraID`（取数据库资源 `Name`）；不会重新反序列化整份 `Value` 或重载全部子资源，也不会在此循环中移除本次查询未出现的旧对象。因此重新打开管理窗口不是强制重建物理配置和资源的操作。

`SearchCameraIds` 在后台依次调用所选型号的 SDK 枚举，再加载管理对象。`MarkDiscoveredCamerasOnline` 以发现结果的 `MD5Id` 匹配物理 `Code` 并标为 Online；这个托管方法不把未命中的对象统一改为 Offline。管理列表的在线数和排序来自资源 `Remark`，关注数还检查许可证提示及子资源是否为空；它们与本次扫描结果、实际打开相机、采集成功分别核对。

## 找到所需操作

先在左侧选择目标物理相机，再使用对应位置的操作：

| 位置与名称 | 用途 |
| --- | --- |
| 顶部“操作 > 许可证导入” | 批量读取 `.lic` / `.zip`；可能重置物理配置，见下文 |
| 当前相机的许可证区域 | 使用当前相机的许可证更新入口；按相机代码匹配 |
| 详情区“修改配置” | 编辑选中相机的物理配置；顶部“操作 > 修改配置”编辑的是管理器配置 |
| 详情区“打开配置文件” | 实际打开 `FileBasePath / Code` 文件夹，目录存在时才可用 |
| 详情区“上传校正文件” | 解包校正资源到当前相机目录并更新数据库，见下文 |
| 详情区“创建还原点” / “加载还原点” | 分别生成 `.cvcal` 和读取已展开的目录，两者不是直接对称的归档恢复入口 |
| 顶部“操作 > MVS 日志” | 启动本机 MVS 日志工具；只在约定的 `C:\Program Files (x86)\MVS\Applications\Win64\LogViewer.exe` 存在时可用 |

## 创建资源时的目录请求与自动关联

创建窗口和管理器许可证导入都可能调用 `CreatePhysicalCameraFloder`。它先经 `RCFileUpload` 发送 `PhysicalCamera_Load`，得到请求记录后继续执行，没有等待目录创建的完成回执。

随后加载物理集合；若集合中仅一台相机，就设置该相机许可证，遍历当前全部设备服务：写入通用配置的 `SN`、相机/校准服务的 `CameraCode`，并逐个 `Save()`。这条路径可能影响多项绑定和服务；“唯一”按已加载的物理集合判断，不是按本次扫描到几台相机判断。文件/数据库/关联服务没有整批事务成功保证，需分别确认请求结果、保存内容和预期设备绑定。

## 许可证导入的两个入口

| 入口 | 匹配、持久化与副作用 |
| --- | --- |
| `PhyCameraManager.Import` | 支持 `.lic` / `.zip`；ZIP 中只处理 `.lic` 项，以文件名（不含扩展名）作为 `MacAddress`，解析许可证并保存许可证/物理资源 |
| `PhyCamera.SetLicense` | 更新当前相机，文件名必须匹配该物理资源 `Code`；保存返回 `1` 时刷新许可证，并请求关联的校准服务和相机服务重启 |

管理器批量导入的 `UpdateSysResource` 对新对象和已有对象都会写入默认 `new ConfigPhyCamera()`，已有物理配置可能被覆盖；保存后继续进入上面的目录请求和自动关联流程。应先核对这是要创建/更新物理资源，还是只给当前相机更新许可证，再选择入口。

许可证解析/数据库保存与硬件运行授权是不同判据；不要因“导入成功”就宣称采集可用。执行导入、更新、创建或恢复前，确认目标相机代码、可覆盖配置/许可证的范围、关联服务和写入授权；不要把重导许可证作为默认排障步骤。

## 校准资源上传

在详情区点击“上传校正文件”进入 `PhyCamera.UploadCalibration`，目标是 `Config.FileServerCfg.FileBasePath / Code / cfg`。`UploadDataAsync` 使用每台相机独立的 `CalibrationUploadRunner`：同一相机拒绝并发，任务结束或异常后释放门禁，不会全局串行化其它相机。

上传会创建目标目录，在独立临时工作区解包、读取 `Calibration.cfg`，覆盖目标同名文件并写入/更新数据库资源、分组。文件复制与数据库更新分步完成，失败不保证全部回滚；名称虽为“上传”，此入口的资源文件写入是本地文件系统操作。模板如何消费这些资源见[校准服务](./calibration.md)。

不要把 `UploadData()` 返回、`UploadDataAsync` 结束或 `UploadClosed` 事件等同于全部成功：旧 `UploadData` 是 fire-and-forget，异步实现内部会捕获错误，失败路径也会发关闭事件。应检查 `UploadList` 的逐项状态、`Msg`、错误日志与目标资源；分组处理也可能单独报错。源码中的“上传完成”提示不是完整事务验收。

## 恢复点的创建与载入并不对称

“创建还原点”调用 `CreateRestore()`，收集相机配置、可用许可证和校准资源，在临时目录组包，最终写入桌面的 `Restore/{Code}.cvcal`。`PhyCameraRestoreArchive.CreateOrReplace` 先生成同目录临时压缩文件，再替换目标；压缩失败时保留已有恢复点。创建恢复点本身会写文件，不证明恢复过程已验证。

“加载还原点”调用 `LoadResotre()`（源码保留此拼写），直接读取桌面 `Restore/{Code}/CameraConfig.cfg` 和可选 `{Code}.lic`，然后保存配置/许可证；它没有直接选择或解压 `CreateRestore()` 生成的 `.cvcal`，也没有在此方法中恢复整套校准资源。不能写成“一键完整还原”，更不能未获授权自动解包、覆盖或调用它。保存物理配置仍会触发绑定服务的同步副作用。

## 验证范围

- `PhyCameraRestoreArchiveTests` 覆盖压缩失败保留旧文件、成功替换；不覆盖 `LoadResotre` 或真机恢复。
- `CalibrationUploadRunnerTests` 覆盖同相机并发拒绝、失败释放门禁、不同相机不互锁及 UI 通知。
- `CalibrationUploadWorkspaceTests` 约束临时工作区隔离/清理；不证明资源上传的文件/数据库一致性。
- 发现、许可证导入、唯一相机自动关联、跨服务重启和完整校准恢复仍需授权环境验收。只读源码核对不能代替 SDK、数据库和实际设备验证。
