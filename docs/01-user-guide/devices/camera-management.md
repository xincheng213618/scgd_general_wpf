---
knowledge_id: "operations.physical-camera"
knowledge_type: "topic"
status: "current"
summary: "PhyCameraManager发现、许可导入、校准资源与恢复点契约；许可证导入可重置配置，并在唯一物理相机时批量绑定设备。"
aliases: ["物理相机","相机管理","相机许可证","导入lic","唯一相机自动绑定","校准文件上传","恢复点","PhyCameraManager","PhyCamera","SearchCameraIds","SetLicense","CreateRestore","LoadResotre","UploadDataAsync"]
code_paths: ["Engine/ColorVision.Engine/Services/PhyCameras/PhyCameraManager.cs","Engine/ColorVision.Engine/Services/PhyCameras/PhyCamera.cs","Engine/ColorVision.Engine/Services/PhyCameras/PhyCameraRestoreArchive.cs","Engine/ColorVision.Engine/Services/PhyCameras/CalibrationUploadRunner.cs","Engine/ColorVision.Engine/Services/PhyCameras/CalibrationUploadWorkspace.cs"]
test_paths: ["Test/ColorVision.UI.Tests/PhyCameraRestoreArchiveTests.cs","Test/ColorVision.UI.Tests/CalibrationUploadRunnerTests.cs","Test/ColorVision.UI.Tests/CalibrationUploadWorkspaceTests.cs"]
related: ["operations.camera","operations.camera-configuration","operations.calibration","engine.devices"]
---

# 物理相机发现、许可证与资源管理

`PhyCameraManager` 管理数据库中的物理相机对象，`PhyCamera` 持有物理配置、许可证、校准资源和逻辑设备关联。对象存在不等于硬件在线，更不等于远程采集已经成功。采集链见[相机服务](./camera.md)；参数编辑和同步覆盖见[相机配置](./camera-configuration.md)。

## 发现结果和已有对象不是同一层

`PhyCameraManager.LoadPhyCamera` 从 MySQL 查询 `ServiceTypes.PhyCamera` 资源；有非空配置的记录才进入物理相机集合。已有 ID 优先复用对象；新对象才装载其资源。它不是硬件枚举，也不能当成强制重建所有已有配置/资源的操作。

`SearchCameraIds()` 先选择相机型号，再由 `Task.Run` 调用原生 `cvCameraCSLib.SearchCameraIds`；正在搜索时拒绝重入。完成后重新加载对象，以发现结果的 `MD5Id` 匹配物理相机 `Code`、更新在线标记并展示搜索结果。取消型号选择不会搜索；异常会提示并释放搜索中标志。列表中的在线/关注统计是管理状态，不能替代本次设备打开和采集返回。

`Create()` 先查是否存在 `Type == 101` 且 `Value` 为空的候选资源；没有时转入搜索，有时才打开创建窗口。因此空列表要分别查发现结果、数据库候选记录和已创建对象，不能直接推断驱动坏了。

## 许可证导入的两个入口

| 入口 | 匹配、持久化与副作用 |
| --- | --- |
| `PhyCameraManager.Import` | 支持 `.lic` / `.zip`；ZIP 中只处理 `.lic` 项，以文件名（不含扩展名）作为 `MacAddress`，解析许可证并保存许可证/物理资源 |
| `PhyCamera.SetLicense` | 更新当前相机，文件名必须匹配该物理资源 `Code`；保存返回 `1` 时刷新许可证，并请求关联的校准服务和相机服务重启 |

管理器的批量导入**不是只替换许可证**：`UpdateSysResource` 对新对象和已有对象都会写入默认 `new ConfigPhyCamera()`；已有物理配置可能被覆盖。之后 `CreatePhysicalCameraFloder` 调用资源目录创建入口；若加载后仅有一台物理相机，会设置该许可证，并遍历设备服务更新 `DeviceServiceConfig.SN`、相机/校准服务的 `CameraCode` 后逐个 `Save()`。导入可能影响多项绑定和服务，不是只读修复手段，也没有整批事务成功的保证。

许可证解析/数据库保存与硬件运行授权是不同判据；不要因“导入成功”就宣称采集可用。执行导入、更新、创建或恢复前，确认目标相机代码、可覆盖配置/许可证的范围、关联服务和写入授权；不要把重导许可证作为默认排障步骤。

## 校准资源上传

`PhyCamera.UploadCalibration` 的目标是 `Config.FileServerCfg.FileBasePath / Code / cfg`。`UploadDataAsync` 使用每台相机独立的 `CalibrationUploadRunner`：同一相机拒绝并发，任务结束或异常后释放门禁，不会全局串行化其它相机。

上传会创建目标目录，在独立临时工作区解包、读取 `Calibration.cfg`，覆盖目标同名文件并写入/更新数据库资源、分组。文件复制与数据库更新分步完成，失败不保证全部回滚；名称虽为“上传”，此入口的资源文件写入是本地文件系统操作。模板如何消费这些资源见[校准服务](./calibration.md)。

不要把 `UploadData()` 返回、`UploadDataAsync` 结束或 `UploadClosed` 事件等同于全部成功：旧 `UploadData` 是 fire-and-forget，异步实现内部会捕获错误，失败路径也会发关闭事件。应检查 `UploadList` 的逐项状态、`Msg`、错误日志与目标资源；分组处理也可能单独报错。源码中的“上传完成”提示不是完整事务验收。

## 恢复点的创建与载入并不对称

`CreateRestore()` 收集相机配置、许可证和校准资源，在临时目录组包，最终写入桌面的 `Restore/{Code}.cvcal`。`PhyCameraRestoreArchive.CreateOrReplace` 先生成同目录临时压缩文件，再替换目标；压缩失败时保留已有恢复点。创建恢复点本身会写文件，不证明恢复过程已验证。

当前 `LoadResotre()`（源码保留此拼写）直接读取桌面 `Restore/{Code}/CameraConfig.cfg` 和可选 `{Code}.lic`，然后保存配置/许可证；它没有直接选择或解压 `CreateRestore()` 生成的 `.cvcal`，也没有在此方法中恢复整套校准资源。不能写成“一键完整还原”，更不能未获授权自动解包、覆盖或调用它。保存物理配置仍会触发绑定服务的同步副作用。

## 验证范围

- `PhyCameraRestoreArchiveTests` 覆盖压缩失败保留旧文件、成功替换；不覆盖 `LoadResotre` 或真机恢复。
- `CalibrationUploadRunnerTests` 覆盖同相机并发拒绝、失败释放门禁、不同相机不互锁及 UI 通知。
- `CalibrationUploadWorkspaceTests` 约束临时工作区隔离/清理；不证明资源上传的文件/数据库一致性。
- 发现、许可证导入、唯一相机自动关联、跨服务重启和完整校准恢复仍需授权环境验收。只读源码核对不能代替 SDK、数据库和实际设备验证。
