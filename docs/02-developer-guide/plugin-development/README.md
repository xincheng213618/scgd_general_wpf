# 插件开发总览

本章节面向需要扩展 ColorVision 功能的开发者，优先给出当前仍然有效的插件开发路径。

## 插件在仓库中的位置

- 运行时插件源码位于 `Plugins/`
- 插件被主程序在运行时发现并加载
- 如果插件带 UI，通常需要启用 WPF 并遵循主应用的界面约定

## 开发一个插件的最短路径

1. 先看 [扩展性概览](../core-concepts/extensibility.md)
2. 再看 [插件开发入门](./getting-started.md)
3. 需要理解运行阶段时，再看 [插件生命周期](./lifecycle.md)

## 当前推荐约定

- 目标框架保持与主仓库一致的 Windows 桌面方向
- 需要界面时启用 WPF
- 构建后将产物复制到主程序输出目录下的 `Plugins/<Name>/`
- 优先参考现有标准插件的组织方式，而不是另起一套约定

## 建议参考的现有插件

- [Pattern 插件](../../04-api-reference/plugins/standard-plugins/pattern.md)
- [Spectrum 插件](../../04-api-reference/plugins/standard-plugins/spectrum.md)
- [SystemMonitor 插件](../../04-api-reference/plugins/standard-plugins/system-monitor.md)

## 说明

- 本页只提供入口，不展开过细的历史设计细节。
- 如果某个插件依赖项目级定制逻辑，应同时查看 `Projects/` 下对应实现。