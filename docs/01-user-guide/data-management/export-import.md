---
knowledge_id: "operations.exports"
knowledge_type: "guide"
status: "current"
summary: "按设置、流程、图像和项目结果定位导入导出实现，说明配置覆盖、文件验收与迁移边界。"
aliases: ["导入导出","CSV","Excel","cvsettings","导出图片","ConfigTransferSettingsProvider","SaveSnapshotExportsAsync","导入和导出设置","导出设置","导入设置","打开配置文件夹"]
code_paths: ["UI/ColorVision.UI.Desktop/Settings/ExportAndImport/ConfigTransferSettingsProvider.cs","UI/ColorVision.UI.Desktop/Settings/ExportAndImport/ConfigTransferSettingsControl.xaml.cs","UI/ColorVision.UI.Desktop/Settings/ExportAndImport/ConfigTransferSettingsControl.xaml","UI/ColorVision.UI/ConfigHandler.cs","Engine/ColorVision.Engine/Templates/Flow/TemplateFlow.cs","UI/ColorVision.ImageEditor/ImageView.Snapshot.cs"]
test_paths: ["Test/ColorVision.UI.Tests/FlowPackageCompatibilityTests.cs","Test/ColorVision.UI.Tests/ConfigHandlerPersistenceTests.cs","Test/ColorVision.UI.Tests/ImageViewSnapshotSaveTests.cs"]
related: ["operations.data","ui.configuration","engine.results","flow.templates","ui.image-editor","engine.cv-image-export","delivery.file-transfer"]
---

# 设置、流程与结果的导入导出边界

按要保存、迁移或交付的对象选择入口：软件设置、流程包、图像、客户报表和协议输出分别由所属模块处理。本页提供入口与核对方法，并说明设置文件的导出和导入步骤；单项导出只覆盖该对象的内容。

导出可能包含客户和设备数据，共享前须脱敏。导入可能覆盖配置、创建模板或改数据库，执行前确认精确目标及可用备份；仅查询方法不授权实际导入、运行流程或发送 Socket/MES 消息。

## 按对象定位入口

| 对象 | 当前入口与实现 | 能力边界 |
| --- | --- | --- |
| 软件设置 `.cvsettings` | 设置页中的“导入和导出设置”；`ConfigTransferSettingsProvider` 注册 `ConfigTransferSettingsControl` TabItem | 序列化已实例化的配置节并合并目标文件，不是完整复制当前配置，更不包含全部数据库与结果图 |
| 单流程及关联模板 `.cvflow` | `TemplateFlow` 调用 `FlowPackageHelper` | 带关联模板及引用处理；包兼容与导入规则见[模板与 Flow 链路](../../04-api-reference/engine-components/template-flow-chain.md) |
| 多选流程 | `TemplateFlow` 多选导出 | 当前是 zip 内多个 `.stn`，不能等同于多个完整 `.cvflow` 包 |
| 数据库记录 | 所属业务结果页或实体通用查询 | 用于确认源记录和范围，不能据此推断存在通用数据库迁移向导 |
| CSV、Excel、报告 | 对应业务窗口或项目 exporter | 字段、单位、判定与格式版本由具体业务实现决定 |
| 原图与带 overlay 的结果图 | ImageEditor / 结果窗口；`ImageView.Snapshot.cs` | 区分原始像素导出和渲染快照，核对是否包含叠加层、尺寸与格式；见[ImageEditor](../../04-api-reference/ui-components/ColorVision.ImageEditor.md) |
| `.cvraw` / `.cvcie` 原生图像 | 原生导出窗口或命令行 `-e` / `-o` | 通道、位深、命名和覆盖规则见[CVRAW / CVCIE 图像导出](../../04-api-reference/engine-components/cv-image-export.md) |
| 已有文件的网页传送与分享 | Web“文件中转”（`/transfer`） | 传送已有文件，不生成业务导出或导入内容；上传、续传与分享保留期见[文件中转](../../02-developer-guide/backend/file-transfer.md) |
| Socket/MES 响应 | 项目 handler 和 `ColorVision.SocketProtocol` | 属于协议输出，不是文件导出；项目结果与关联字段见[结果链路](../../04-api-reference/engine-components/result-handoff-chain.md) |

## 导出或导入软件设置

在设置中打开“导入和导出设置”。该页有“导出设置”“导入设置”“打开配置文件夹”三个按钮；“打开配置文件夹”在资源管理器中定位当前 `ConfigHandler.ConfigFilePath`，可先用它确认目标配置位置。

### 导出设置

1. 点击“导出设置”，选择保存位置。建议文件名为 `Exported-yyyy-MM-dd.cvsettings`，对话框也允许选择其它扩展名。
2. 保存后核对所需模块的配置节。按钮调用 `SaveConfigs(fileName)`，序列化当前已实例化的配置对象；新目标文件可能缺少从未实例化的配置节，不能据此作为全部设置的完整副本。
3. 若选择已有 JSON 文件，保存会合并它原有的配置节；未覆盖的其它节可能留下。为避免带入旧目标内容，可选择新文件，再核对需要迁移的节。保存校验、加密和写入规则见[配置持久化与重载](../../04-api-reference/ui-components/configuration.md)。

### 导入设置

1. 确认目标配置位置、要导入的内容和独立可用的原配置备份。对话框允许 `.cvsettings` 和所有文件，扩展名不证明内容有效。
2. 点击“导入设置”并选择文件。确认文件选择后立即进入备份、覆盖和加载流程，没有配置差异预览或第二次导入确认；软件设置导入不同时恢复流程模板、数据库或图片。
3. 导入后核对主文件、所需配置节和相关模块的实际值。重载后的配置按需实例化，已打开控件或持有旧引用的模块仍需核对其刷新结果。

| 阶段 | 执行与失败边界 |
| --- | --- |
| `BackupConfigs()` | 用序列化快照生成备份，不是原文件字节副本；尝试按文件名保留最多 10 个匹配备份并清理更旧文件。备份或清理异常仅记日志，因此备份失败不会自动阻止覆盖 |
| `File.Copy(..., overwrite: true)` | 直接覆盖主配置文件，复制前没有 JSON 或配置节校验；这次复制不使用核心保存函数的临时文件替换机制 |
| `LoadConfigs()` | 加载主文件；无效输入可能触发备份恢复或默认配置，并非总以导入错误结束。正常 JSON 加载也不等于所有配置类型已验证 |
| `InvalidateCache()` | 加载后使设置项缓存失效；前面的重载通知若抛错，这一步可能不执行。清缓存也不会自动重绑所有已打开控件 |

上述按钮流程没有覆盖“备份、复制、加载、刷新”的整体事务或补偿回退。窗口仍可使用、没有异常提示或部分设置看起来正常，都不能代替第 3 步的核对。备份回退、重载通知与旧引用的具体契约由配置主题维护。

## 按对象核对导出结果

先记录对象、来源入口、输出位置和预期格式，再用适用于该对象的检查判断是否完成。使用已有获授权样例即可；另跑流程、写入数据库或向外部系统发送消息有各自的执行前提。

| 对象 | 核对内容 |
| --- | --- |
| 软件设置 | 文件内容可解析，包含所需配置节；需要迁移时另核对目标模块的重载结果 |
| 流程包或多选 zip | 文件可读取，流程名称、条目数和关联模板符合所选范围；导入成功与运行时引用可用分别核对 |
| CSV、Excel、项目报告 | 对应同一 SN、批次或时间范围的源记录；检查格式版本、字段顺序、单位、判定与时间，空文件不能代替源数据证据 |
| 图像 | 原图或渲染图模式、尺寸、位深、所需通道和 overlay；原生导出另按其主题检查命名碰撞和部分通道成功 |
| Socket/MES 输出 | 对应项目的最终结果、响应字段及外部接收结果；本地文件存在不证明协议交付 |
| 网页文件中转 | 各项上传完成状态和实际分享/下载内容；传送完成不验证文件内部业务字段 |

保留足以复核本次交付的脱敏样例与来源信息；数据库记录、截图或外部响应按对应对象的需要收集。

## 故障分流

| 现象 | 第一检查点 |
| --- | --- |
| 找不到导出按钮 | 对象属于设置、流程、图像还是具体业务窗口；不要寻找不存在的总导出菜单 |
| 文件为空或字段不对 | 源数据、选定批次/SN、项目 exporter 与客户格式版本 |
| 图片/overlay 不对齐 | 原图与结果图是否同一轮、坐标空间及导出模式；按[三条结果链](../../04-api-reference/engine-components/result-handoff-chain.md)定位 |
| 外部系统收不到 | 项目是否已生成最终结果、协议与端口、项目 handler 和响应关联字段 |
| 导入后行为变化 | 实际被替换的配置与重载结果；流程模板、数据库及图片是否仍是原环境数据 |

## 源码与验证边界

`ConfigTransferSettingsProvider.cs` / `ConfigTransferSettingsControl.xaml.cs` 位于 `UI/ColorVision.UI.Desktop/Settings/ExportAndImport/`；配置保存、备份和加载在 `UI/ColorVision.UI/ConfigHandler.cs`；流程入口在 `Engine/ColorVision.Engine/Templates/Flow/TemplateFlow.cs`；图像快照与原图保存入口在 `UI/ColorVision.ImageEditor/ImageView.Snapshot.cs`。

`ConfigHandlerPersistenceTests.cs` 覆盖配置重载和持久化的局部契约，不等于设置导入按钮全链或所有模块迁移已验证；`FlowPackageCompatibilityTests.cs` 覆盖流程包兼容、完整性与模板引用；`ImageViewSnapshotSaveTests.cs` 覆盖快照/原图保存与格式限制。项目报表字段和 Socket/MES 交付仍需对应样例与项目测试。
