# Engine 模板系统开发交接手册

本页说明 `Engine/ColorVision.Engine/Templates/` 的真实模板开发模型。模板负责参数、编辑、保存、导入导出和算法命令参数，不负责客户最终判定和报表格式。

先读 [Engine 模板与 Flow 链路](../../04-api-reference/engine-components/template-flow-chain.md)，再按本页改代码。

## 模板运行链路

| 阶段 | 关键对象 | 说明 |
| --- | --- | --- |
| 模板实例注册 | `ITemplate` 构造函数、`TemplateControl.AddITemplateInstance` | 模板创建后按 `Name` / `Code` 进入全局模板表 |
| 模板发现 | `IITemplateLoad`、`TemplateControl` | 启动时扫描可加载模板并实例化 |
| 参数集合 | `TemplateModel<T>`、`TemplateParams` | UI 下拉框和编辑窗口绑定的真实集合 |
| MySQL 模板 | `ITemplate<T>`、`ParamModBase`、`ModMasterModel`、`ModDetailModel` | 通过 `TemplateDicId` 读取系统字典和模板明细 |
| JSON 模板 | `ITemplateJson<T>`、`TemplateJsonParam` | 以 JSON 字段承载复杂算法参数 |
| 编辑入口 | `TemplateEditorWindow`、`EditTemplateJson`、具体编辑控件 | 新建、复制、编辑、导入、导出 |
| Flow 绑定 | `Templates/Flow/`、`NodeConfigurator` | 节点配置面板读取模板并写入节点参数 |
| 结果展示 | `ViewHandle*`、`IResultHandleBase` | 算法结果解析和 overlay，不属于模板保存本身 |

## 选择哪种模板

| 场景 | 推荐模型 | 示例 |
| --- | --- | --- |
| 参数来自系统字典，字段结构稳定 | `ITemplate<T>` + `ParamModBase` | `TemplatePoi`、`TemplateSFR`、`TemplateImageCropping` |
| 算法参数层级复杂，字段经常随算法版本变动 | `ITemplateJson<T>` + `TemplateJsonParam` | `TemplateSFR2`、`TemplateOLEDAOI`、`TemplateKB` |
| 设备自己的运行参数 | 设备目录下的 `Templates/` | Camera、PG、Sensor、SMU 目录下的模板 |
| Flow 流程模板 | `Templates/Flow/TemplateFlow` | 流程组和节点配置 |
| 只是客户最终输出格式 | 项目包 `Process` / exporter | 不要放进通用模板层 |

## 新增模板步骤

1. 确认参数属于通用算法、设备运行参数、Flow 节点参数还是项目客户规则。
2. 创建参数类。MySQL 模板继承 `ParamModBase`，JSON 模板继承 `TemplateJsonParam`。
3. 创建 `Template*` 类，继承 `ITemplate<T>` 或 `ITemplateJson<T>`，并在需要自动加载时实现 `IITemplateLoad`。
4. 准备静态 `Params` 集合，并在构造函数里赋给 `TemplateParams`。
5. 如需从数据库恢复，实现 `GetMysqlCommand()` 并确认 `TemplateDicId` / 字典项正确。
6. 准备编辑入口：普通参数用 PropertyGrid/编辑控件，JSON 参数用 `EditTemplateJson` 或专用编辑页。
7. 如果算法执行要引用模板，在 `Algorithm*` 中把模板 ID、名称和相关 POI 模板写入 `CVTemplateParam`。
8. 如果 Flow 节点要选择模板，在 `NodeConfigurator` 或节点属性面板里加下拉和编辑按钮。
9. 如果有新结果类型，再补 `ViewHandle*` 和结果展示文档。

## 保存和兼容要点

- `ITemplate<T>.Load()` 会按 `TemplateDicId` 查询 `ModMasterModel` 和 `ModDetailModel`。
- `SaveIndex` 只保存被标记的模板；编辑器里改字段后要确认调用了保存路径。
- 导入模板时要避免重名，`NewCreateFileName()` 和 `TemplateControl.ExitsTemplateName()` 是当前重名检查入口。
- JSON 模板字段改名会影响旧模板反序列化，新增字段要有默认值。
- 模板名、模板 ID、POI 模板名经常会被项目包导出或结果展示引用，不能只在 UI 里验证。

## 常见错误

| 现象 | 优先排查 |
| --- | --- |
| 下拉框没有新模板 | `IITemplateLoad` 是否实现、`TemplateParams` 是否赋值、`TemplateControl` 是否扫到类型 |
| 保存后重开丢失 | `GetMysqlCommand()`、`TemplateDicId`、`SaveIndex`、MySQL 连接 |
| Flow 节点显示了旧值 | 节点配置存储字段、模板 ID 和模板名是否同步 |
| 算法执行拿不到参数 | `Algorithm*` 是否把 `TemplateParam` / `POITemplateParam` 写入请求 |
| 结果页打不开 | 先看 `ViewHandle*` 和 DAO，不要只改模板 |

## 验收清单

| 项目 | 验收方式 |
| --- | --- |
| 模板管理 | 新建、复制、重命名、导入、导出、删除 |
| 保存恢复 | 保存后重启主程序，模板仍在且字段一致 |
| Flow | 用新模板跑最小流程，确认节点参数没有回退 |
| 算法请求 | 日志或消息记录里能看到正确模板 ID/名称 |
| 结果展示 | 历史结果、overlay、表格和项目导出都能读到新结果 |
| 旧模板 | 用旧模板执行一次，确认新增字段默认值不破坏旧数据 |

## 相关文档

- [Engine 模板与 Flow 链路](../../04-api-reference/engine-components/template-flow-chain.md)
- [Engine 结果展示与项目交接链路](../../04-api-reference/engine-components/result-handoff-chain.md)
- [FlowEngineLib](../../04-api-reference/engine-components/FlowEngineLib.md)
- [测试与验证交接手册](../testing.md)
