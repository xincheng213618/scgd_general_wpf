# JSON 模板

本页只描述当前仓库里真实可用的 JSON 模板宿主链，不再继续维护“通用算法 DSL 平台 + 跨项目配置框架”式旧稿。

## 先看这个模块现在是什么

按当前源码状态，JSON 模板系统不是一套脱离数据库独立存在的配置平台，而是 `ColorVision.Engine` 模板体系中的一个具体分支。它当前的核心目标是：

- 把 `ModMasterModel.JsonVal` 里的 JSON 内容托管成模板项。
- 通过通用编辑器 `EditTemplateJson` 提供文本编辑和属性编辑两种模式。
- 让具体模板类型以 `ITemplateJson<T>` 的形式复用同一套加载、保存、导入导出逻辑。
- 为像 `PoiAnalysis`、`SFRFindROI` 这类 JSON 驱动模板提供统一宿主。

因此它更像“数据库中的 JSON 模板分支”，而不是一个完全独立的配置子系统。

## 当前最关键的文件

- `Engine/ColorVision.Engine/Templates/Jsons/ITemplateJson.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/TemplateJsonParam.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/EditTemplateJson.xaml`
- `Engine/ColorVision.Engine/Templates/Jsons/EditTemplateJson.xaml.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/PoiAnalysis/TemplatePoiAnalysis.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/SFRFindROI/TemplateSFRFindROI.cs`

如果只是想看“JSON 模板现在怎么存、怎么编、怎么挂进模板窗口”，这些文件已经覆盖主干。

## 当前主链怎么跑

### 宿主基类

`ITemplateJson<T>` 是 JSON 模板分支的通用宿主。它当前负责：

- 用 `TemplateDicId` 从 MySQL 读取 `ModMasterModel`
- 把每条记录包装成 `TemplateModel<T>`
- 提供保存、删除、复制、导入、导出
- 在创建新模板时，从字典模板默认 JSON 生成初始内容

这意味着 JSON 模板虽然长得像纯文本编辑，但当前仍然深度依赖模板字典和数据库记录。

### 参数对象

`TemplateJsonParam` 当前是最基础的 JSON 模板参数对象。它持有：

- `TemplateJsonModel`
- `ResetCommand`
- `CheckCommand`
- `JsonValueChanged` 事件

其中 `JsonValue` 的真实语义是：

- 读取时用 `JsonHelper.BeautifyJson(...)` 格式化
- 写入时只有在 JSON 合法时才回写 `TemplateJsonModel.JsonVal`

`ResetValue()` 则会回到字典模板记录的默认 JSON，而不是简单清空本地文本。

### 编辑器控件

`EditTemplateJson` 是当前真正的编辑入口。它现在同时支持：

- AvalonEdit 文本模式
- `JsonPropertyEditorControl` 属性模式
- 描述注释视图切换
- 校验按钮
- 外部 JSON 网站辅助入口

其中右下角的 `json` 按钮当前实际行为很明确：

- 打开 `https://www.json.cn/`
- 把当前 JSON 复制到剪贴板

这就是当前活动文件里 `Button_Click_1` 的真实作用，不是其它隐藏命令。

### 模式切换与同步

`EditTemplateJson` 当前不是简单文本框包装。它会：

- 用防抖定时器同步文本改动
- 通过 `IEditTemplateJson.JsonValueChanged` 反向刷新界面
- 在文本模式与属性模式之间切换时同步 JSON 内容
- 用 `EditTemplateJsonConfig` 记住宽度和默认编辑模式

因此这里的复杂度主要在“两个编辑面保持同一份 JSON 一致”，而不是算法本身。

## 当前几个最容易写错的点

### 它不是通用文件模板平台

当前 JSON 模板的主存储是 MySQL 的 `ModMasterModel.JsonVal`，不是仓库里一组任意 JSON 文件。继续把它写成“读取磁盘配置目录”会偏离真实实现。

### 不是所有 JSON 模板共享同一个业务 schema

`ITemplateJson<T>` 只提供宿主链；每个具体模板实际需要什么字段，仍由各自的 JSON 约定决定。文档不能再把某一类 JSON 结构写成全系统统一规范。

### 编辑器已经不只是文本编辑器

当前 `EditTemplateJson` 已经支持属性模式和描述模式切换。只描述 AvalonEdit 文本框，会漏掉用户实际看到的一半功能。

### “校验”当前主要是事件触发，不是完整编译器

`CheckCommand` 触发的是 `JsonValueChanged` 事件链，具体怎么响应取决于调用方。不要把它写成独立的 JSON 规则引擎。

## 推荐阅读顺序

1. `Engine/ColorVision.Engine/Templates/Jsons/ITemplateJson.cs`
2. `Engine/ColorVision.Engine/Templates/Jsons/TemplateJsonParam.cs`
3. `Engine/ColorVision.Engine/Templates/Jsons/EditTemplateJson.xaml.cs`
4. `Engine/ColorVision.Engine/Templates/Jsons/PoiAnalysis/TemplatePoiAnalysis.cs`
5. `Engine/ColorVision.Engine/Templates/Jsons/SFRFindROI/TemplateSFRFindROI.cs`

## 继续阅读

- [Templates API 参考](./api-reference.md)
- [模板管理](./template-management.md)
- [ColorVision.Engine](../../engine-components/ColorVision.Engine.md)