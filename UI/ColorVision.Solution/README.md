# ColorVision.Solution

ColorVision 的单工作区、资源打开、项目树和文档停靠模块；不是 Visual Studio `.sln` 导入器。目标框架、依赖和 NuGet 内容以 `ColorVision.Solution.csproj` 为准。

## 契约入口

- [资源打开与单工作区切换](../../docs/04-api-reference/ui-components/ColorVision.Solution.md)：ResourceOpenService、`.cvsln` / `.cvproj`、项目 Provider、私有工作区、取消与恢复。
- [编辑器与文档生命周期](../../docs/04-api-reference/ui-components/editor-document-lifecycle.md)：编辑器选择、文档复用、保存/关闭、文件变更与停靠布局。
- [终端进程、会话与脚本运行](../../docs/01-user-guide/interface/terminal.md)：ConPTY、Python、命令提交及进程退出。

这些链接用于源码仓库；单独取得 NuGet 包时，请核对对应源码版本。本文件不维护第二套行为手册。打开/恢复工作区可能写私有状态、生成树缓存或修复配置；取消切换不等于撤销全部文件变化。

## 源码职责

- `Editor/`：编辑器发现与打开路由；内置 Text、Image、Hex、Web、Model3D、Project 等编辑器。
- `Explorer/`：树节点、项目 Provider、模板、项目配置与文件操作。
- `Workspace/`：文档生命周期、AvalonDock 布局、私有工作区身份。
- `Mru/`：最近路径与固定项；`Terminal/`：终端会话和控件。

Markdown 预览在 `ColorVision.UI.Desktop`，多图/融合工具在 `ColorVision.ImageTools`，账户权限在 `ColorVision.Rbac`。内置 MSBuild Provider 只读解析部分 .NET 项目信息，不评估完整 MSBuild；生成/运行/调试 UI 当前由关闭的 `ShowBuildAndDebugUI` 隐藏，不代表底层命令不存在。

从仓库根目录在 Windows 上执行以下本地构建；会还原依赖和写入产物，启用了 `GeneratePackageOnBuild`，不发布包或验证工作区运行：

```powershell
dotnet build .\UI\ColorVision.Solution\ColorVision.Solution.csproj -c Debug -p:Platform=x64
```
