# 插件与现状页

本章只保留两类内容：

- 当前工作区里仍能直接对上源码的插件专题页
- 对应源码已缺失或不再完整维护、因此改写成“历史状态说明”的旧插件页

它不再承担“完整插件目录”这个角色，也不默认这里列到的每一页都代表当前源码树中可直接开发的插件项目。

## 先理解这章的边界

- 当前插件装载模型应以 `manifest.json` 和 `UI/ColorVision.UI/Plugins/PluginLoader.cs` 的实际实现为准。
- 插件 API 参考页只覆盖当前文档里已经收束过的少数专题，不等于 `Plugins/` 目录的完整镜像。
- 如果文档描述和当前源码目录不一致，应优先以源码目录和运行时装载行为为准。

## 当前包含哪些页面

### 当前仍能和源码直接对上的专题

- [Spectrum 插件](./standard-plugins/spectrum.md)
- [SystemMonitor 插件](./standard-plugins/system-monitor.md)
- [EventVWR 插件](./standard-plugins/eventvwr.md)
- [Windows 服务插件](./standard-plugins/windows-service.md)

### 历史状态说明页

- [Pattern / 图卡生成功能](./standard-plugins/pattern.md)
- [ImageProjector（历史状态）](./standard-plugins/image-projector.md)
- [ScreenRecorder（历史状态）](./standard-plugins/screen-recorder.md)

这些页面保留的目的，是解释“当前仓库里还能不能对上源码、应该去哪里找现状”，而不是继续扮演功能承诺页。

## 怎么读这一章更有效

1. 先看 [插件开发概览](../../02-developer-guide/plugin-development/overview.md)，理解插件入口、产物形态和运行时边界。
2. 再确认目标插件当前是否真在 `Plugins/` 目录中存在对应源码。
3. 如果页面明确写成“历史状态”，应把它当作现状说明，而不是当作当前开发手册。
4. 如果要追运行时装载链，应回到 `PluginLoader` 和各插件目录下的 `manifest.json` 对照阅读。

## 当前已知空白

- 当前 API 参考并没有覆盖 `Plugins/` 目录里的全部真实项目。
- 像 Conoscope 这类当前仍存在源码的插件，目前还没有单独的 API 参考页。
- 因此这章更适合作为“已整理专题入口”，不适合作为“插件全量索引”。

## 继续阅读

- [API 参考总览](../README.md)
- [插件开发概览](../../02-developer-guide/plugin-development/overview.md)
- [FlowEngineLib 节点扩展](../extensions/flow-node.md)