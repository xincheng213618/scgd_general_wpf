---
knowledge_id: "operations.exports"
knowledge_type: "guide"
status: "current"
summary: "按配置备份、流程、图像和项目结果定位入口，说明文件验收与迁移边界。"
aliases: ["导入导出","CSV","Excel","cvsettings","导出图片","SaveSnapshotExportsAsync","配置备份","打开配置文件夹"]
code_paths: ["UI/ColorVision.UI/ConfigHandler.cs","Engine/ColorVision.Engine/Templates/Flow/TemplateFlow.cs","UI/ColorVision.ImageEditor/ImageView.Snapshot.cs"]
test_paths: ["Test/ColorVision.UI.Tests/FlowPackageCompatibilityTests.cs","Test/ColorVision.UI.Tests/ConfigHandlerPersistenceTests.cs","Test/ColorVision.UI.Tests/ImageViewSnapshotSaveTests.cs"]
related: ["operations.data","ui.configuration","ui.storage-maintenance","engine.results","flow.templates","ui.image-editor","engine.cv-image-export","delivery.file-transfer"]
---

# 设置、流程与结果的导入导出边界

按要保存、迁移或交付的对象选择入口：配置备份、流程包、图像、客户报表和协议输出分别由所属模块处理。本页提供入口与核对方法；单项导出只覆盖该对象的内容。

导出可能包含客户和设备数据，共享前须脱敏。导入可能覆盖配置、创建模板或改数据库，执行前确认精确目标及可用备份；仅查询方法不授权实际导入、运行流程或发送 Socket/MES 消息。

## 按对象定位入口

| 对象 | 当前入口与实现 | 能力边界 |
| --- | --- | --- |
| 软件配置与备份 | “存储与维护”的配置/备份目录入口；配置服务提供保存、备份与重载 | 设置窗口不提供 `.cvsettings` 导入/导出；配置备份不包含全部数据库与结果图 |
| 单流程及关联模板 `.cvflow` | `TemplateFlow` 调用 `FlowPackageHelper` | 带关联模板及引用处理；包兼容与导入规则见[模板与 Flow 链路](../../04-api-reference/engine-components/template-flow-chain.md) |
| 多选流程 | `TemplateFlow` 多选导出 | 当前是 zip 内多个 `.stn`，不能等同于多个完整 `.cvflow` 包 |
| 数据库记录 | 所属业务结果页或实体通用查询 | 用于确认源记录和范围，不能据此推断存在通用数据库迁移向导 |
| CSV、Excel、报告 | 对应业务窗口或项目 exporter | 字段、单位、判定与格式版本由具体业务实现决定 |
| 原图与带 overlay 的结果图 | ImageEditor / 结果窗口；`ImageView.Snapshot.cs` | 区分原始像素导出和渲染快照，核对是否包含叠加层、尺寸与格式；见[ImageEditor](../../04-api-reference/ui-components/ColorVision.ImageEditor.md) |
| `.cvraw` / `.cvcie` 原生图像 | 原生导出窗口或命令行 `-e` / `-o` | 通道、位深、命名和覆盖规则见[CVRAW / CVCIE 图像导出](../../04-api-reference/engine-components/cv-image-export.md) |
| 已有文件的网页传送与分享 | Web“文件中转”（`/transfer`） | 传送已有文件，不生成业务导出或导入内容；上传、续传与分享保留期见[文件中转](../../02-developer-guide/backend/file-transfer.md) |
| Socket/MES 响应 | 项目 handler 和 `ColorVision.SocketProtocol` | 属于协议输出，不是文件导出；项目结果与关联字段见[结果链路](../../04-api-reference/engine-components/result-handoff-chain.md) |

## 配置文件与备份

在[存储与维护](../../04-api-reference/ui-components/storage-maintenance.md)中定位配置和备份目录。配置服务管理主文件的保存、备份与重载；已有 `.cvsettings` 文件不能通过设置窗口直接导入。

`ConfigHandler.SaveConfigs(fileName)` 仍是底层保存接口：序列化当前已实例化的配置对象，并合并目标文件已有节。新目标可能缺少从未实例化的节，已有目标也可能保留其它旧节；该接口不构成全项目备份。保存校验、备份回退、重载通知与旧对象引用的边界统一见[配置持久化与重载](../../04-api-reference/ui-components/configuration.md)。

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

配置保存、备份和加载在 `UI/ColorVision.UI/ConfigHandler.cs`；流程入口在 `Engine/ColorVision.Engine/Templates/Flow/TemplateFlow.cs`；图像快照与原图保存入口在 `UI/ColorVision.ImageEditor/ImageView.Snapshot.cs`。

`ConfigHandlerPersistenceTests.cs` 覆盖配置重载和持久化的局部契约，不等于所有模块迁移已验证；`FlowPackageCompatibilityTests.cs` 覆盖流程包兼容、完整性与模板引用；`ImageViewSnapshotSaveTests.cs` 覆盖快照/原图保存与格式限制。项目报表字段和 Socket/MES 交付仍需对应样例与项目测试。
