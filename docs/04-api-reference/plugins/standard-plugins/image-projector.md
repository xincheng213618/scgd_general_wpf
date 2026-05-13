# ImageProjector 状态说明

本页不再把 ImageProjector 写成当前仓库里的标准插件实现，因为在当前 `scgd_general_wpf` 工作区里，已经找不到与之对应的源码工程。

## 当前工作区里的实际状态

按当前仓库结构核对：

- `Plugins/` 目录下没有 `ImageProjector/` 源码目录。
- 工作区里没有对应的插件工程文件。
- 当前插件索引页 [Plugins/README.md](../../../../Plugins/README.md) 也没有把它列为现存插件目录。
- 当前文档侧边栏中保留它，只是为了让历史状态说明页仍然可达，而不是表示当前仓库里存在对应插件实现。

因此，这一页不能继续保留旧版那种“多显示器投影工具完整手册”的写法，否则会把历史功能写成当前源码事实。

## 这页现在保留什么信息

当前只保留一个结论：

ImageProjector 相关文档在这个工作区里属于历史残留页，而不是基于当前源码可核对的 API 参考页。

如果后续重新引入这个插件，新的文档应至少基于这些真实锚点重写：

- 插件目录与工程文件
- `manifest.json`
- 菜单或 provider 接入点
- 主窗口或投影窗口实现
- 配置落点

在这些代码重新出现之前，不应继续补充功能介绍、配置表或 API 清单。

## 为什么不继续维护旧稿

旧版页面把 ImageProjector 描述成当前真实存在的插件，并给出了完整功能列表、显示模式说明和组件结构。但在当前源码树里，这些描述已经没有可核对的实现承载。

文档如果继续这么写，会把“过去可能存在过的功能”伪装成“当前仓库事实”。这正是本轮清理要避免的事情。

## 继续阅读

- [Plugins/README.md](../../../../Plugins/README.md)
- [docs/02-developer-guide/plugin-development/overview.md](../../../02-developer-guide/plugin-development/overview.md)
- [docs/04-api-reference/plugins/standard-plugins/pattern.md](./pattern.md)