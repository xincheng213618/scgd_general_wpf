# ColorVision 外部插件 SDK

外部插件接入、构建、`.cvxp` 打包和上传说明统一维护在 [ColorVision Plugin Kit](./ColorVision.PluginKit/README.md) 及其 [SDK 文档](./ColorVision.PluginKit/docs/ColorVision.Plugin.SDK.md)。

仓库外插件使用 Plugin Kit 的 `cvplugin.exe` 或 `SDK/ColorVision.PluginKit/scripts/package_cvxp.py`；仓库内官方插件使用根目录 `Scripts/package_plugin.bat`，Spectrum 使用专用的 `Scripts/Spectrum.bat`。配置、参数和错误排查见[PluginKit SDK 打包器](../docs/02-developer-guide/plugin-development/sdk-packaging.md)。

ColorVision 包版本、插件版本和最低宿主版本分别以实际 NuGet 源、插件项目文件及 `manifest.json` 为准，不在本页复制固定值。
