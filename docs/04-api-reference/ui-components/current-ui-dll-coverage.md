# 当前 UI DLL 文档覆盖清单

本页是 `UI/` 目录的模块级台账。接手人员先用它确认“当前真实存在多少个 UI 项目、每个项目对应哪一页文档、发布 DLL 时要看哪些证据”，再进入单个组件页或发布手册。

更新时间：2026-06-10。

## 当前结论

- 当前 `UI/` 下有 10 个真实项目目录，每个目录都有 `.csproj` 和 `README.md`。
- 10 个项目都已经有对应的文档页，文档页放在 `docs/04-api-reference/ui-components/`。
- 10 个项目都启用了 `GeneratePackageOnBuild`，发布时不能只检查主程序输出目录，还要检查 NuGet 包内容。
- `ColorVision.Common`、`ColorVision.Themes`、`ColorVision.UI`、`ColorVision.Core`、`ColorVision.Database`、`ColorVision.SocketProtocol`、`ColorVision.Scheduler` 支持 `net8.0-windows7.0` 与 `net10.0-windows7.0`。
- `ColorVision.ImageEditor`、`ColorVision.UI.Desktop`、`ColorVision.Solution` 当前只面向 `net10.0-windows7.0`。
- `ColorVision.Core` 是 native/runtime 风险最高的 UI 包；`ColorVision.ImageEditor` 是图像交互和结果 overlay 风险最高的 UI 包；`ColorVision.UI.Desktop` 是桌面工具和现场运维入口风险最高的 UI 包。

## 模块覆盖表

| UI 项目 | 项目文件 | 源码 README | 文档页 | 发布形态 | 交接重点 |
| --- | --- | --- | --- | --- | --- |
| `UI/ColorVision.Common/` | `ColorVision.Common.csproj` | 有 | [ColorVision.Common](./ColorVision.Common.md) | DLL + NuGet | MVVM 基础、插件接口、共享契约、状态栏元数据 |
| `UI/ColorVision.Themes/` | `ColorVision.Themes.csproj` | 有 | [ColorVision.Themes](./ColorVision.Themes.md) | DLL + NuGet | 主题资源字典、窗口样式、明暗主题切换 |
| `UI/ColorVision.UI/` | `ColorVision.UI.csproj` | 有 | [ColorVision.UI](./ColorVision.UI.md) | DLL + NuGet | 配置、菜单、插件加载、PropertyGrid、快捷键、多语言 |
| `UI/ColorVision.Core/` | `ColorVision.Core.csproj` | 有 | [ColorVision.Core](./ColorVision.Core.md) | DLL + NuGet + native runtime | `HImage`、OpenCV helper、视频/图像互操作、`runtimes/win-x64/native` |
| `UI/ColorVision.Database/` | `ColorVision.Database.csproj` | 有 | [ColorVision.Database](./ColorVision.Database.md) | DLL + NuGet | SqlSugar DAO、数据库浏览器、MySQL/SQLite 接入 |
| `UI/ColorVision.SocketProtocol/` | `ColorVision.SocketProtocol.csproj` | 有 | [ColorVision.SocketProtocol](./ColorVision.SocketProtocol.md) | DLL + NuGet | 本地 TCP 服务、JSON/Text 消息分发、消息历史 |
| `UI/ColorVision.Scheduler/` | `ColorVision.Scheduler.csproj` | 有 | [ColorVision.Scheduler](./ColorVision.Scheduler.md) | DLL + NuGet | Quartz 调度、任务恢复、任务历史、管理窗口 |
| `UI/ColorVision.ImageEditor/` | `ColorVision.ImageEditor.csproj` | 有 | [ColorVision.ImageEditor](./ColorVision.ImageEditor.md) | DLL + NuGet + 图像资源 | `ImageView`、`DrawCanvas`、工具栏、结果 overlay、3D/CIE 视图 |
| `UI/ColorVision.UI.Desktop/` | `ColorVision.UI.Desktop.csproj` | 有 | [ColorVision.UI.Desktop](./ColorVision.UI.Desktop.md) | WinExe + NuGet | 设置窗口、向导、插件市场、下载工具、DLL 版本查看 |
| `UI/ColorVision.Solution/` | `ColorVision.Solution.csproj` | 有 | [ColorVision.Solution](./ColorVision.Solution.md) | DLL + NuGet | 工作区、编辑器、终端、多图查看、本地 RBAC、项目管理 |

## 发布边界

| 边界 | 包含模块 | 发布时先看 | 现场风险 |
| --- | --- | --- | --- |
| 基础共享层 | `ColorVision.Common`、`ColorVision.Themes`、`ColorVision.UI` | [UI DLL 组件手册](./component-handbook.md)、[UI DLL 发布矩阵](./release-matrix.md) | 菜单、配置、插件入口、主题资源失效会影响多个上层窗口 |
| 图像与 native 层 | `ColorVision.Core`、`ColorVision.ImageEditor` | [UI DLL 发布证据与现场核查表](./dll-release-evidence.md)、[UI 运行时组件交接手册](./ui-runtime-handoff.md) | 缺 native DLL、图像资源或 overlay 注册失败，会直接影响检测结果查看 |
| 数据与服务窗口层 | `ColorVision.Database`、`ColorVision.SocketProtocol`、`ColorVision.Scheduler` | [UI DLL 发布场景手册](./ui-dll-release-playbook.md)、[UI 组件目录](./control-catalog.md) | 数据源、Socket 监听、调度历史和后台任务排障依赖这些窗口 |
| 桌面工具与工作区层 | `ColorVision.UI.Desktop`、`ColorVision.Solution` | [UI 运行时组件交接手册](./ui-runtime-handoff.md)、对应单模块页 | 插件市场、下载工具、Solution 工作区、RBAC 和本地项目管理集中在这里 |

## 证据来源

本轮审计使用下面几类证据确认覆盖状态：

- `Get-ChildItem UI -Directory`：确认当前真实 UI 项目目录。
- 每个 `UI/ColorVision.*/` 目录下的 `.csproj`：确认目标框架、`VersionPrefix`、`GeneratePackageOnBuild`、`PackageReadmeFile`、资源复制规则。
- 每个 `UI/ColorVision.*/README.md`：确认包内 README 来源。
- `docs/04-api-reference/ui-components/*.md`：确认每个 UI 项目都有独立文档页。
- `Directory.Build.props`：确认全局版本元数据、作者、仓库地址和 `ColorVision.snk` 条件强名称签名。

## 重点风险

| 风险点 | 影响模块 | 接手时怎么查 |
| --- | --- | --- |
| native runtime 丢失 | `ColorVision.Core` | 检查 NuGet 包和宿主输出目录是否包含 `runtimes/win-x64/native` 下的 OpenCV/helper DLL |
| 图像 overlay 不显示 | `ColorVision.ImageEditor`、Engine 结果显示链 | 先看 [UI 运行时组件交接手册](./ui-runtime-handoff.md)，再看 Engine 的 [结果交接链路](../engine-components/result-handoff-chain.md) |
| 菜单或插件入口不出现 | `ColorVision.UI`、`ColorVision.Common`、`ColorVision.UI.Desktop` | 检查菜单注册、插件发现、权限和配置项是否被加载 |
| 桌面工具缺文件 | `ColorVision.UI.Desktop` | 检查 `OutputType=WinExe`、WebView2、CSS、`aria2c.exe`、资源文件的复制规则 |
| 工作区功能异常 | `ColorVision.Solution` | 检查编辑器、终端、多图查看、本地 RBAC 与项目目录权限 |
| net8/net10 混用 | 全部 UI DLL | 检查宿主目标框架、插件目标框架、Engine 包引用回退版本是否一致 |

## 维护规则

新增、删除或重命名 UI DLL 时，必须同步更新：

1. 本页的模块覆盖表和发布边界。
2. `docs/04-api-reference/ui-components/README.md` 的 UI 包清单。
3. 对应的单模块文档页，例如 `ColorVision.Xxx.md`。
4. [UI DLL 组件手册](./component-handbook.md)。
5. [UI 组件目录](./control-catalog.md)，如果新增了窗口、控件、Provider 或扩展点。
6. [UI DLL 发布矩阵](./release-matrix.md)。
7. [UI DLL 发布证据与现场核查表](./dll-release-evidence.md)。
8. [UI 运行时组件交接手册](./ui-runtime-handoff.md)，如果模块有运行时发现、菜单、设置或服务窗口。
9. `docs/.vitepress/i18n/navigation-data.json` 中的侧边栏导航。

## 快速复查命令

```powershell
Get-ChildItem UI -Directory | Sort-Object Name | Select-Object -ExpandProperty Name

Get-ChildItem docs/04-api-reference/ui-components -File |
  Sort-Object Name |
  Select-Object -ExpandProperty Name

Get-ChildItem UI -Directory | Sort-Object Name | ForEach-Object {
  $csproj = Get-ChildItem $_.FullName -Filter *.csproj -File | Select-Object -First 1
  $readme = Test-Path (Join-Path $_.FullName 'README.md')
  "$($_.Name): csproj=$($csproj.Name) readme=$readme"
}
```

复查结果如果出现“有源码项目但没有文档页”或“有文档页但源码项目已不存在”，优先修本文档和侧边栏，再处理翻译版本。
