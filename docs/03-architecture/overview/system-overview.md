---
knowledge_id: "platform.system"
knowledge_type: "topic"
status: "current"
summary: "宿主、UI、Engine、插件与项目的职责及调用边界：UI操作不必经过Engine，程序集依赖不是统一执行顺序，构建产物不等于交付制品。"
aliases: ["系统架构", "分层", "代码结构", "项目结构", "仓库地图", "哪里改", "模块对应", "源码参考", "API参考", "模块入口", "调用链", "组件交互", "跨模块", "UI调用边界"]
code_paths: ["ColorVision/ColorVision.csproj", "ColorVision/App.xaml.cs", "Engine/ColorVision.Engine/ColorVision.Engine.csproj", "UI/ColorVision.ImageEditor/ColorVision.ImageEditor.csproj", "UI/ColorVision.ImageEditor/Algorithms/ImageAlgorithmPreviewSession.cs", "UI/ColorVision.Algorithms/ColorVision.Algorithms.csproj", "UI/ColorVision.UI/Plugins/PluginLoader.cs"]
test_paths: []
related: ["platform.architecture", "platform.runtime", "algorithms.platform", "engine.index", "flow.architecture", "plugins.model", "projects.index", "delivery.deployment"]
---

# 系统职责与跨模块边界

ColorVision 是由桌面宿主、共享类库、Engine、插件和客户项目组合的系统，**不存在所有操作都遵循的“主窗口 → UI → Engine → 插件”调用顺序**。本页解释容易误判的职责边界；按目录查全部主题请直接用[生成的源码地图](../../knowledge/index.md)，不在正文复制另一份文件树。

## 目录、引用与调用不是同一张图

- 目录说明源码归属，不表示严格的上下层关系。`UI/ColorVision.Algorithms/` 就包含中立算法契约，不是一个设备操作窗口。
- `.csproj` 的项目/包引用说明编译和交付依赖，不证明某次运行经过了所有被引用模块。条件引用和 native 输入还要核对项目属性。
- 运行链从实际入口、调用者和消费方确认。菜单可能直接打开插件或客户项目窗口，局部图像操作可能直接调用本地算法；不能先画出固定分层，再让源码迁就图。

## 按责任确认边界

| 要判断的边界 | 当前事实与不能推出的结论 | 权威主题 |
| --- | --- | --- |
| 宿主启动与业务功能 | `ColorVision/App.xaml.cs` 处理启动分支、恢复选择和模块装载；是否装载插件取决于当前启动路径，不是每次业务操作后的最后一步 | [启动运行时](./runtime.md)、[插件装载](../../02-developer-guide/plugin-development/overview.md) |
| 共享 UI 与本地图像算法 | ImageEditor 可以直接调用自己的算法 runtime；UI 目录下的行为并不全部下发 Engine，算法 provider 仍有发布和依赖门禁 | [统一算法平台](../../02-developer-guide/core-concepts/image-algorithm-platform-v1.md)、[ImageEditor](../../04-api-reference/ui-components/ColorVision.ImageEditor.md) |
| Engine 业务宿主与算法实现 | Engine 负责设备、模板、消息及相应业务适配，但不是所有本地算法的内核；历史结果 handler 与中立算法结果不能混成一套注册机制 | [Engine 入口](../../04-api-reference/engine-components/README.md)、[结果交接](../../04-api-reference/engine-components/result-handoff-chain.md) |
| Flow 执行与业务最终化 | 画布、模板持久化、节点图内核、共享业务会话与隔离执行各有 owner；不能把共享会话的前后处理和 RC 前提套给所有 Flow 路径 | [Flow 架构](../components/engine/flow-engine.md) |
| 通用扩展与客户规则 | 可装载插件与客户项目包通过宿主已有扩展入口接入；独立对接示例按自身入口运行。客户判定、Recipe/Fix、协议字段和导出映射属于具体项目，不应下沉为通用 UI 或 Engine 规则 | [扩展职责](../../02-developer-guide/core-concepts/extensibility.md)、[客户项目](../../04-api-reference/projects/README.md) |
| 源码输出与正式交付 | 项目构建、当前客户端更新、外部安装工程和包发布是不同链；历史 `src/ColorVisionSetup/` 仍存在不表示它参与当前发布 | [平台与制品](../../02-developer-guide/README.md)、[交付责任](../../02-developer-guide/deployment/overview.md) |

## 两个可核对的反例

### 本地图像操作不必经过 Engine

`UI/ColorVision.ImageEditor/Algorithms/ImageAlgorithmPreviewSession.cs` 中的预览路径直接调用 `_image.AlgorithmRuntime.Runner.RunAsync`。ImageEditor 工程引用 Algorithms、Common、Core、Themes、UI，没有直接的 Engine 项目引用；中立的 `ColorVision.Algorithms.csproj` 也没有 Engine 项目引用。

这些证据说明该路径不要求先经过 Engine 设备/模板适配器；它们不证明每一种 provider 都无 native 依赖、默认可执行或跨平台可用。能力门禁、输入所有权和结果生命周期仍以统一算法主题为准。

### 插件装载不是业务链尾端

在 `App.xaml.cs` 的普通启动路径中，允许装载时先调用 `PluginLoader.LoadPlugins`，之后才创建启动向导或 `StartWindow`。恢复选择可以跳过全部或指定插件；不能因为插件目录存在或主程序已经进入窗口就推断所有扩展均成功装载。

程序集进入发现集合、各扩展注册器何时扫描、菜单是否可见以及具体动作能否执行仍是不同阶段。检查对应装载器与功能注册器，不用“插件已加载”代替全部能力可用。

## 跟踪一项改动

1. 确定实际入口：菜单、窗口、库 API、Flow 请求或协议消费方；不要默认入口都在主窗口。
2. 顺着真实调用、回调及数据消费定位 owner；跨过模块边界时阅读相应主题，不把概要当成完整调用图。
3. 核对该路径的前提、输入输出和完成判据；请求已发、图已结束、业务已保存和交付已完成不能互相代替。
4. 用本地 `knowledge.mjs impact <源码路径>` 查需复核的主题，再补查实际调用者与测试。映射只报告已登记关系，未命中不等于没有文档影响。

## 验证边界

本页由工程引用、实际入口和关联主题支撑，不声明全仓静态调用图或端到端测试覆盖。纯知识维护不需要启动应用、连接数据库/设备或运行发布脚本；实现改动的验证范围见[测试与验证](../../02-developer-guide/testing.md)，具体证据随各能力主题维护。
