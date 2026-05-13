# ScreenRecorder 状态说明

本页不再把 ScreenRecorder 写成当前仓库里的标准插件实现，因为在当前 `scgd_general_wpf` 工作区里，已经找不到与之对应的源码工程。

## 当前工作区里的实际状态

按当前仓库结构核对：

- `Plugins/` 目录下没有 `ScreenRecorder/` 源码目录。
- 工作区里没有对应的插件工程文件。
- 当前插件索引页 [Plugins/README.md](../../../../Plugins/README.md) 没有把它列为现存插件目录。
- 当前文档侧边栏中保留它，只是为了让历史状态说明页仍然可达，而不是表示当前仓库里存在对应插件实现。

因此，这一页不能继续保留旧版那种“高性能录屏插件 API 手册”的写法，否则会把历史描述误写成当前实现。

## 这页现在保留什么信息

当前只保留一个结论：

ScreenRecorder 相关文档在这个工作区里属于历史残留页，而不是基于当前源码可核对的 API 参考页。

如果后续重新引入这个插件，新的文档应至少基于这些真实锚点重写：

- 插件目录与工程文件
- `manifest.json`
- 菜单或 provider 接入点
- 录制窗口与录制源管理实现
- 配置和输出落点

在这些代码重新出现之前，不应继续补充编码格式、录制源类型或覆盖层 API 之类的描述。

## 为什么不继续维护旧稿

旧版页面把 ScreenRecorder 描述成当前真实存在的录屏插件，并给出了录制源、编码器、覆盖层和高级功能列表。但在当前源码树里，这些内容已经没有可核对的实现。

继续润色那份旧稿，只会让文档看起来更完整，却和当前仓库越来越脱节。

## 继续阅读

- [Plugins/README.md](../../../../Plugins/README.md)
- [docs/02-developer-guide/plugin-development/overview.md](../../../02-developer-guide/plugin-development/overview.md)
- [docs/04-api-reference/plugins/standard-plugins/pattern.md](./pattern.md)