# Templates 模块分析

本页只分析当前仓库里的 Templates 系统现状，不再保留“补了多少文档”“规划了多少优化方案”这类已经失效的总结内容。

## 先看这个模块到底是什么

`Engine/ColorVision.Engine/Templates/` 不是单一算法目录，而是一组同时承担这几类职责的代码：

- 模板抽象和注册
- 模板管理与编辑窗口
- 不同业务域的模板实现
- 流程模板相关能力
- 模板搜索、创建、样例保存等辅助工具

因此它既有纯模型代码，也有明显的 WPF 窗口和编辑器代码，阅读时不要把它误判成“只有算法参数定义”。

## 当前最值得先认识的文件

| 类别 | 文件 |
| --- | --- |
| 核心入口 | `ITemplate.cs`、`ModelBase.cs`、`ParamModBase.cs`、`TemplateContorl.cs` |
| 管理与编辑 UI | `TemplateManagerWindow.xaml(.cs)`、`TemplateEditorWindow.xaml(.cs)`、`TemplateCreate.xaml(.cs)`、`TemplateSettingEdit.xaml(.cs)` |
| 搜索与样例 | `TemplateSearchProvider.cs`、`TemplateSampleLibrary.cs`、`TemplateSampleSaveWindow.xaml(.cs)` |

如果只是想弄清“模板怎么被发现、怎么被打开、怎么被编辑”，先看这些文件通常比直接扎进某个算法子目录更有效。

## 当前目录怎么分

从仓库现状看，这个目录至少可以分成四类：

| 类别 | 代表目录/文件 | 注意点 |
| --- | --- | --- |
| 核心框架层 | `ITemplate.cs`、`ModelBase.cs`、`ParamModBase.cs`、`TemplateContorl.cs`、`TemplatesExtension.cs` | 负责抽象、注册、基础模型和公共 UI |
| 流程模板层 | `Flow/` | 承接流程模板和流程编辑运行能力 |
| 业务模板族 | `ARVR/`、`POI/`、`Compliance/`、`JND/`、`Matching/`、`FindLightArea/`、`FocusPoints/`、`ImageCropping/`、`LedCheck/`、`LEDStripDetection/`、`Validate/`、`DataLoad/` | 有的按算法域分组，有的按处理环节分组 |
| JSON 模板族 | `Jsons/` | 以 JSON 配置为核心，与传统目录式模板并存 |

如果看到名称相近但目录不同的模板实现，不要先假定是重复代码，更可能是历史版本、配置方式或业务接入方式不同。

## 当前运行时是怎么把模板接进系统的

模板系统当前最重要的运行时链路很直接：

1. 主程序和插件先把程序集装载进来。
2. `TemplateContorl` 在数据库连接可用后扫描当前已加载程序集。
3. 它查找实现了 `IITemplateLoad` 的非抽象类型。
4. 这些类型通过 `Load()` 把模板注册进 `ITemplateNames`。
5. 模板管理窗口、编辑窗口、流程窗口和业务功能再去消费这些已注册模板。

这条链路说明两个实际约束：

- 模板不是在一个静态总表里手写声明完的。
- 模板能不能出现，受程序集装载和数据库可用状态共同影响。

## 常见误区

| 误区 | 正确判断 |
| --- | --- |
| 把它当成纯算法层 | 这里同时包含窗口、编辑器、搜索、样例保存、模板创建和流程相关 UI |
| 以为所有目录同一时期按同一规则设计 | 当前目录有明显演进痕迹，不要强套统一分层模型 |
| 模板缺失时先查编辑器 | 更早可能是程序集没装载、数据库没连上、`IITemplateLoad` 没跑到或模板名重复 |

## 阅读顺序

先看 `TemplateContorl.cs` 理解发现和注册，再看 `ITemplate.cs`、`ModelBase.cs`、`ParamModBase.cs`，然后看 `TemplateManagerWindow` 和 `TemplateEditorWindow`，最后进入 `ARVR/`、`POI/` 或 `Flow/` 等具体业务目录。

## 这页不再做什么

本页不再继续维护这些内容：

- 文档补全成果统计
- 过于具体但未落地的重构计划
- 与当前仓库状态脱节的统一分层模型

如果后续要做真正的重构建议，应在独立设计页里单独论证，而不是混在“现状分析”里。
