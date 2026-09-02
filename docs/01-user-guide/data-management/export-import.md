---
knowledge_id: "operations.exports"
knowledge_type: "guide"
status: "current"
summary: "按设置、流程、图像和项目结果定位导入导出实现，说明配置覆盖、文件验收与迁移边界。"
aliases: ["导入导出","CSV","Excel","cvsettings","导出图片","ConfigTransferSettingsProvider","SaveSnapshotExportsAsync"]
code_paths: ["UI/ColorVision.UI.Desktop/Settings/ExportAndImport/ConfigTransferSettingsProvider.cs","UI/ColorVision.UI.Desktop/Settings/ExportAndImport/ConfigTransferSettingsControl.xaml.cs","UI/ColorVision.UI/ConfigHandler.cs","Engine/ColorVision.Engine/Templates/Flow/TemplateFlow.cs","UI/ColorVision.ImageEditor/ImageView.Snapshot.cs"]
test_paths: ["Test/ColorVision.UI.Tests/FlowPackageCompatibilityTests.cs","Test/ColorVision.UI.Tests/ConfigHandlerPersistenceTests.cs","Test/ColorVision.UI.Tests/ImageViewSnapshotSaveTests.cs"]
related: ["operations.data","ui.configuration","engine.results","flow.templates","ui.image-editor"]
---

# 设置、流程与结果的导入导出边界

仓库没有统一的全数据导入导出中心。设置迁移由 `ConfigHandler` 负责，流程包由 `TemplateFlow` 负责，图像由 ImageEditor 负责，客户报表与协议输出由所属项目负责；导出其中一种对象不代表其它数据也已备份。

导出可能包含客户和设备数据，共享前须脱敏。导入可能覆盖配置、创建模板或改数据库，执行前确认精确目标及可用备份；仅查询方法不授权实际导入、运行流程或发送 Socket/MES 消息。

## 按对象定位入口

| 对象 | 当前入口与实现 | 能力边界 |
| --- | --- | --- |
| 软件设置 `.cvsettings` | 设置页中的“导入导出设置”；`ConfigTransferSettingsProvider` 注册 `ConfigTransferSettingsControl` TabItem | 序列化已实例化的配置节并合并目标文件，不是完整复制当前配置，更不包含全部数据库与结果图 |
| 单流程及关联模板 `.cvflow` | `TemplateFlow` 调用 `FlowPackageHelper` | 带关联模板及引用处理；包兼容与导入规则见[模板与 Flow 链路](../../04-api-reference/engine-components/template-flow-chain.md) |
| 多选流程 | `TemplateFlow` 多选导出 | 当前是 zip 内多个 `.stn`，不能等同于多个完整 `.cvflow` 包 |
| 数据库记录 | 所属业务结果页或实体通用查询 | 用于确认源记录和范围，不能据此推断存在通用数据库迁移向导 |
| CSV、Excel、报告 | 对应业务窗口或项目 exporter | 字段、单位、判定与格式版本由具体业务实现决定 |
| 原图与带 overlay 的结果图 | ImageEditor / 结果窗口；`ImageView.Snapshot.cs` | 区分原始像素导出和渲染快照，核对是否包含叠加层、尺寸与格式 |
| Socket/MES 响应 | 项目 handler 和 `ColorVision.SocketProtocol` | 属于协议输出，不是文件导出；项目结果与关联字段见[结果链路](../../04-api-reference/engine-components/result-handoff-chain.md) |

统一 Excel/JSON/XML/PDF 导出中心、通用列映射导入、通用批量文件夹导入均不能从这些入口推定存在；只有目标模块确实实现时才能承诺。

## 设置导出、备份和导入的不同结果

`ConfigTransferSettingsControl.Export_Click` 选择目标文件后调用 `SaveConfigs(fileName)`，不是复制 `ConfigFilePath`。核心保存只序列化当前 `Configs` 中已经实例化的对象，再合并到所选目标的既有 JSON；新目标中可能没有从未实例化的配置节，旧目标中未覆盖的其它节则可能留下。导出按钮返回不证明拿到了所有模块配置的完整副本。保存、加密和目标文件合并规则见[配置持久化与重载](../../04-api-reference/ui-components/configuration.md)。

`Import_Click` 选择文件后依次尝试 `BackupConfigs()`、`File.Copy(..., overwrite: true)` 覆盖主配置文件、`LoadConfigs()` 并使设置缓存失效。对话框也允许所有文件；扩展名过滤不是内容验证，复制前没有 JSON 或配置节校验。备份同样走序列化保存，不是原文件字节副本；其内部捕获并记录异常，备份失败也不会自动阻止后续覆盖。备份调用还会按文件名保留最多 10 个匹配备份并清理更早文件，不是只追加一个永久恢复点。实际导入前必须独立确认可用备份及其覆盖范围。

这个按钮流程没有包住“备份、复制、加载和刷新”的事务或补偿回退，也没有使用核心保存函数的临时文件替换过程来完成那次 `File.Copy`。普通 `LoadConfigs` 对无效输入可能尝试备份恢复或默认配置，重载通知异常还可能阻断后续设置缓存失效。正常 JSON 中的具体配置节仍按需反序列化，加载完成也不证明所有配置类型已验证。因此没有异常提示、窗口还开着或设置看起来正常，都不能代替核对最终文件、加载的节和相关模块是否重新绑定；具体回退与通知边界见配置主题。

## 核对一次导出

1. 固定导出对象、来源窗口、格式版本和明确的 SN/批次/时间范围，优先使用已有获授权样例；另跑流程需要运行授权。
2. 在来源窗口或数据库确认同一轮源数据。数据库证据不足时不要把空文件当作成功交付。
3. 导出到明确目录，确认文件可打开；核对字段顺序、单位、判定、时间和 SN。图像另核对原图/渲染图、尺寸与叠加层，协议另核对项目完成响应。
4. 保存脱敏后的样例文件、截图及必要的外部响应，记录已知限制和核对人/日期。

交付记录至少包含：导出对象、来源窗口、源 SN/批次/时间、数据库证据、文件路径、格式版本、必需字段、截图、外部响应样例（如适用）、已知限制和责任人/日期。它描述可复核证据，不代替实际导出或验收。

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
