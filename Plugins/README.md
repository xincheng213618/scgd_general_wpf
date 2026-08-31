# ColorVision Plugins

`Plugins/` 保存当前仓库内的通用插件源码；它不是运行时已安装插件的清单，也不涵盖独立仓库中的插件或 `Projects/` 客户包。

## 知识入口

- [插件装配与模块知识](../docs/04-api-reference/plugins/README.md)：按问题定位当前模块与具体主题。
- [插件装载与扩展发现](../docs/02-developer-guide/plugin-development/overview.md)：manifest、依赖预检、程序集加载与扩展发现的契约。
- [构建与发布脚本](../docs/02-developer-guide/scripts/README.md)：普通插件、Spectrum 和客户包的交付入口与副作用。

实际项目身份、依赖与输出以本目录下各项目的 `.csproj`、`manifest.json`、`Directory.Build.props` 及根 `PluginProject.HostCopy.targets` 为准。进入具体模块后继续读取其 README；原有模块/包说明可以保持原语言。本页不再复制加载步骤或目录快照。

本目录的 `*.bat` 含发布入口，不是普通验证脚本：其中 wrapper 会上传，`Spectrum.bat` 直接调用带 `--upload` 的构建脚本。仅阅读、修改文档或诊断插件不授权执行它们。

全仓定位见[源码知识地图](../docs/knowledge/index.md)，许可见仓库根 [LICENSE.md](../LICENSE.md)。
