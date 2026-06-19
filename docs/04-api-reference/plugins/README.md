# 现有插件能力说明

本章说明当前仓库 `Plugins/` 目录里真实存在的通用插件能做什么、从哪里进入、依赖什么、如何构建和维护。插件开发方法放在 [插件开发手册](../../02-developer-guide/plugin-development/README.md)，客户项目包放在 [项目说明](../../00-projects/README.md)。

如果你要确认“当前每个插件是否都有对应文档”，先看 [当前插件文档覆盖清单](./current-plugin-coverage.md)。如果你要横向比较“哪个插件有 Socket、哪个需要管理员、哪个依赖 native DLL、哪个会写数据库或注册表”，看 [插件能力与交接矩阵](./plugin-capability-matrix.md)。如果你接到的是“插件不显示”“现场缺 DLL”“打包 `.cvxp`”“Socket 指令不通”这类具体任务，按 [插件运行与交接场景手册](./plugin-handoff-playbook.md) 处理。现场替换或发版验收时，用 [现有插件现场验收与交接清单](./plugin-field-acceptance.md) 逐项记录；要留下 manifest、DLL FileVersion、`.cvxp` 和回退证据时，用 [插件发布证据与版本核查表](./plugin-release-evidence.md)。本页保留章节入口和装载模型。

## 当前插件能力总览

| 插件 | 源码目录 | manifest Id | 入口/能力 | 文档 |
| --- | --- | --- | --- | --- |
| Conoscope | `Plugins/Conoscope/` | `Conoscope` | VAM/锥镜图像观察、关注点、色域和对比度分析 | [Conoscope](./standard-plugins/conoscope.md) |
| Spectrum | `Plugins/Spectrum/` | `Spectrum` | 光谱仪连接、标定、测量、EQE、SQLite 结果 | [Spectrum](./standard-plugins/spectrum.md) |
| SystemMonitor | `Plugins/SystemMonitor/` | `SystemMonitor` | 性能监控、状态栏、磁盘/网络/进程信息 | [SystemMonitor](./standard-plugins/system-monitor.md) |
| EventVWR | `Plugins/EventVWR/` | `EventVWR` | Windows 事件错误查看、Dump 配置 | [EventVWR](./standard-plugins/eventvwr.md) |
| WindowsServicePlugin | `Plugins/WindowsServicePlugin/` | `WindowsServicePlugin` | CVWindowsService 安装、注册、MySQL/MQTT 配置 | [WindowsServicePlugin](./standard-plugins/windows-service.md) |

## 先读哪一页

| 你要做什么 | 先看 |
| --- | --- |
| 确认每个当前插件都有文档 | [当前插件文档覆盖清单](./current-plugin-coverage.md) |
| 接手所有现有插件，判断复杂度和风险 | [插件能力与交接矩阵](./plugin-capability-matrix.md) |
| 排查插件不加载、缺 DLL、权限或 Socket 问题 | [插件运行与交接场景手册](./plugin-handoff-playbook.md) |
| 发版、现场替换或交接现有插件 | [现有插件现场验收与交接清单](./plugin-field-acceptance.md) |
| 记录 manifest、DLL 版本、`.cvxp`、native 文件和回退包 | [插件发布证据与版本核查表](./plugin-release-evidence.md) |
| 新增一个通用插件 | [插件开发手册](../../02-developer-guide/plugin-development/README.md) |
| 排查插件没有加载 | [插件运行与交接场景手册](./plugin-handoff-playbook.md)、本页的装载和交付模型、[插件生命周期](../../02-developer-guide/plugin-development/lifecycle.md) |
| 打包 `.cvxp` | [插件运行与交接场景手册](./plugin-handoff-playbook.md)、本页的构建和打包、[构建与发布脚本](../../02-developer-guide/scripts/README.md) |
| 确认某个插件的业务逻辑 | 进入对应插件页 |

## 装载和交付模型

插件由 `UI/ColorVision.UI/Plugins/PluginLoader.cs` 装载：

1. 扫描主程序输出目录下的 `Plugins/` 一级子目录。
2. 优先读取每个目录的 `manifest.json`。
3. 用 `manifest.json` 的 `Id` 更新插件配置缓存。
4. 根据 `dllpath` 找到插件 DLL。
5. 如果目录内存在唯一 `.deps.json`，检查 `ColorVision.*` 依赖版本。
6. 通过 `Assembly.LoadFrom(...)` 加载程序集。
7. 如果没有 `manifest.json`，才尝试用“目录名同名 DLL”兼容加载。

推荐交付形态始终是：

```text
ColorVision/bin/x64/<Config>/net10.0-windows/Plugins/<PluginName>/
  <PluginName>.dll
  manifest.json
  README.md
  CHANGELOG.md
  PackageIcon.png        # 可选
```

## manifest 字段

| 字段 | 说明 |
| --- | --- |
| `manifest_version` | 清单版本，目前为 `1` |
| `id` / `Id` | 插件唯一标识，运行时缓存使用它做 key |
| `name` | 插件显示名 |
| `version` | 插件清单版本 |
| `requires` | 最低 ColorVision 版本 |
| `description` | 插件描述 |
| `dllpath` | 主程序集文件名 |
| `author`、`url`、`entry_point`、`icon` | 可选元数据 |

当前 manifest 文件有大小写混用现象；运行时通过 Newtonsoft.Json 反序列化到 `PluginManifest`，维护时应优先保持现有字段兼容。

## 构建和打包

单独构建插件：

```powershell
dotnet build Plugins/Spectrum/Spectrum.csproj -c Release -p:Platform=x64
```

生成 `.cvxp` 插件包：

```powershell
Scripts\package_plugin.bat Spectrum --no-upload
```

`package_plugin.bat` 会调用 `Scripts/package_cvxp.py`，并执行以下动作：

- 可选执行 `dotnet build`。
- 从插件输出目录收集 DLL 和依赖。
- 按 `Scripts/shared_files.json` 剔除宿主已共享的文件。
- 复制插件根目录的 `README.md`、`CHANGELOG.md`、`manifest.json`、`PackageIcon.png`。
- 生成 `<PluginName>-<FileVersion>.cvxp`。

## 不在当前插件清单里的历史名称

以下名称当前没有对应 `Plugins/<Name>/` 源码目录、`.csproj` 和 `manifest.json`，不再作为“现有插件能力说明”的入口：

- Pattern
- ImageProjector
- ScreenRecorder

如果这些插件重新回到 `Plugins/` 目录，应先恢复源码、manifest、构建脚本、README、CHANGELOG 和打包验证，再把它们重新加入 [当前插件文档覆盖清单](./current-plugin-coverage.md) 与当前插件总览。

## 维护要求

- 新增插件时必须补 `Plugins/<Name>/README.md` 和本章对应页面。
- 新增、删除或恢复插件时必须更新 [当前插件文档覆盖清单](./current-plugin-coverage.md)。
- 修改 manifest 或 PostBuild 复制规则时，同步更新本页“装载模型”和“构建打包”，以及 [插件发布证据与版本核查表](./plugin-release-evidence.md)。
- 修改插件入口、Socket 指令、管理员权限、native 依赖、数据库或注册表行为时，同步更新 [插件运行与交接场景手册](./plugin-handoff-playbook.md)、[插件能力与交接矩阵](./plugin-capability-matrix.md)、[现有插件现场验收与交接清单](./plugin-field-acceptance.md) 和 [插件发布证据与版本核查表](./plugin-release-evidence.md)。
- 插件帮助窗口会读取插件目录 README/CHANGELOG 的，应同时更新运行时 README 和 docs 站点页。
