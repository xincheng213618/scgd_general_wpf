# ColorVision 外部插件 SDK

外部插件接入、构建、`.cvxp` 打包和上传说明统一维护在 [ColorVision Plugin Kit](./ColorVision.PluginKit/README.md) 及其 [SDK 文档](./ColorVision.PluginKit/docs/ColorVision.Plugin.SDK.md)。

不要使用旧的独立发布脚本。仓库外插件应使用 Plugin Kit 的 `cvplugin.exe`，或直接运行 `SDK/ColorVision.PluginKit/scripts/package_cvxp.py`。仓库内官方插件继续使用根目录 `Scripts/package_plugin.bat`，Spectrum 使用专用的 `Scripts/Spectrum.bat`。

ColorVision 包版本、插件版本和最低宿主版本分别以实际 NuGet 源、插件项目文件及 `manifest.json` 为准，不在本页复制固定值。
