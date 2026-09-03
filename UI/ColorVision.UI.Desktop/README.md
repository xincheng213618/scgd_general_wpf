# ColorVision.UI.Desktop

这是 ColorVision 的桌面辅助壳层，提供设置、向导、菜单管理、插件市场、下载、第三方工具与诊断窗口；不是产品主程序。产品启动与主窗口位于仓库的 `ColorVision/`。

## 包与运行前提

- 目标框架、直接项目引用与包依赖以 `ColorVision.UI.Desktop.csproj` 为准；当前为 Windows/WPF 项目。
- 项目虽声明 `WinExe`，但本地 `App.xaml.cs` 没有产品启动逻辑，`MainWindow.xaml` 只有空布局。不能用运行这个项目代替主程序验收。
- 市场 Markdown 呈现使用 WebView2；CSS 是样式资源，缺失时仍渲染，但不加载该 CSS。使用 aria2 下载时须能从输出位置或 PATH 找到 `aria2c`。构建成功不证明联网下载、安装替换或插件装载成功。
- 系统工具、安装更新、注册表写入、网络配置和反馈上传可能改变本机或外部状态，须按具体任务授权操作。

## 源码知识入口

[桌面辅助壳层主题](../../docs/04-api-reference/ui-components/ColorVision.UI.Desktop.md)维护各功能责任、源码与测试定位，并分流到设置、向导、菜单等独立契约。

本 README 作为 NuGet 包说明打包到包根目录。上面的相对链接只在源码仓库中有效；包使用者应在与包版本匹配的源码中读取对应主题，不能将当前网站或另一分支当成该包的行为保证。
