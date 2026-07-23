# ColorVision.Solution

> 版本: 1.5.6.1 | 目标框架: .NET 10.0 Windows

ColorVision 的单工作区资源管理、项目模型和文档打开模块。界面参考 Visual Studio 的 Solution Explorer，但只实现 ColorVision 当前业务需要的文件夹、`.cvsln`、`.cvproj` 与 .NET MSBuild 项目能力。

## 核心边界

- `SolutionManager` 只维护一个活动工作区。文件夹、解决方案和项目统一通过可取消的异步打开流程切换。
- `ResourceOpenService` 是应用级资源路由：工作区激活、普通文件打开、指定编辑器打开和批量文件打开从同一入口分流。
- `SolutionExplorer` 管理当前工作区的树、项目引用、虚拟解决方案文件夹、配置映射、文件监控和撤销/重做。
- `ProjectProviderRegistry` 通过 Provider 扩展项目格式。内置 `.cvproj`，并只读接入 `.csproj`、`.fsproj` 和 `.vbproj`；不支持 `.sln` 导入和 VC++ 工程。
- 生成、运行和调试能力仍保留在项目 Provider 中，当前由 `SolutionFeatureVisibility.ShowBuildAndDebugUI` 控制是否显示界面入口。

## 目录职责

- `Editor/`：编辑器注册、默认打开方式、Open With 与文档打开路由。
- `Explorer/`：节点模型、项目 Provider、菜单贡献、搜索、工作区配置与文件操作。
- `Mru/`：最近工作区列表、固定项和原子 JSON 持久化。
- `Terminal/`：Windows ConPTY 会话、终端缓冲、输入历史和键盘/IME 适配。
- `Workspace/`：AvalonDock 文档生命周期、布局和工作区私有状态。

## 内置编辑器

| 编辑器 | 用途 |
| --- | --- |
| TextEditor | 文本、源码、日志和项目文件 |
| ImageEditor | 常见图像查看与编辑 |
| HexEditor | 二进制和未知格式检查 |
| WebView2Editor | URL、本地 HTML 和文件夹 Web 视图 |
| Model3DEditor | OBJ、STL 等三维模型 |
| ProjectEditor | 文件夹列表与 `.cvsln` 配置 |

Markdown 预览位于 `ColorVision.UI.Desktop`；多图查看与融合工具位于 `ColorVision.ImageTools`；用户、角色和权限管理位于 `ColorVision.Rbac`。它们不是 Solution 核心职责。

## 打开规则

- 多选打开只接受普通文件；文件夹、项目和解决方案必须单独激活。
- 文件夹工作区和直接打开项目所需的私有 `.cvsln` 写入用户配置目录，不污染源目录。
- 显式“打开方式”只选择编辑器，不触发工作区切换。
- 切换或关闭工作区前统一检查关联文档；用户取消时保留原工作区。

## 依赖

- 项目：`ColorVision.UI`、`ColorVision.UI.Desktop`、`ColorVision.ImageEditor`
- 包：AvalonEdit、AvalonDock VS2013 Theme、WebView2、WPFHexaEditor

## 构建

```powershell
dotnet build UI/ColorVision.Solution/ColorVision.Solution.csproj -c Debug -p:Platform=x64
```
