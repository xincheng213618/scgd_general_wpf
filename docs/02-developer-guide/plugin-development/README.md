# 插件开发手册

本章节只讲“怎么开发、装载、调试、打包一个通用插件”。现有插件能做什么，放在 [现有插件能力说明](../../04-api-reference/plugins/README.md)；客户项目包的业务流程，放在 [项目说明](../../00-projects/README.md)。

如果你正在选择参考哪个现有插件，先看 [现有插件能力说明](../../04-api-reference/plugins/README.md)。需要横向排查菜单、状态栏、设置页、Socket、数据库、注册表、管理员权限或 native 依赖时，再进入 [插件横向速查](../../04-api-reference/plugins/plugin-capability-matrix.md)。

## 插件在仓库中的位置

- 运行时插件源码位于 `Plugins/`
- 插件被主程序在运行时发现并加载
- 如果插件带 UI，通常需要启用 WPF 并遵循主应用的界面约定

## 开发一个插件的最短路径

1. 先看 [扩展性概览](../core-concepts/extensibility.md)
2. 再看 [插件开发概览](./overview.md)
3. 进入 [插件开发入门](./getting-started.md)
4. 需要理解运行阶段时，再看 [插件生命周期](./lifecycle.md)

## 当前推荐约定

- 目标框架保持与主仓库一致的 Windows 桌面方向
- 需要界面时启用 WPF
- 构建后将产物复制到主程序输出目录下的 `Plugins/<Name>/`
- 优先参考现有标准插件的组织方式，而不是另起一套约定

## 建议参考的现有插件

- [现有插件能力说明](../../04-api-reference/plugins/README.md)：按插件进入当前能力、manifest 和单插件页。
- [插件横向速查](../../04-api-reference/plugins/plugin-capability-matrix.md)：排查现有插件扩展点、依赖和发布风险。
- [Conoscope 插件](../../04-api-reference/plugins/standard-plugins/conoscope.md)：图像观察、关注点、色域和对比度分析。
- [Spectrum 插件](../../04-api-reference/plugins/standard-plugins/spectrum.md)：光谱仪连接、标定、测量和结果记录。
- [SystemMonitor 插件](../../04-api-reference/plugins/standard-plugins/system-monitor.md)：系统性能和状态监控。
- [WindowsServicePlugin 插件](../../04-api-reference/plugins/standard-plugins/windows-service.md)：Windows 服务安装和运行配置。

## 说明

- 本页只提供插件开发入口，不展开每个现有插件的业务能力。
- 新增、删除或恢复插件时，同步更新 [现有插件能力说明](../../04-api-reference/plugins/README.md)、插件横向速查和导航。
- 如果某个插件依赖项目级定制逻辑，应同时查看 `Projects/` 下对应实现。
