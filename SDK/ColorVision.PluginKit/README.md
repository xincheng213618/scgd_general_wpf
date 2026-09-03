# ColorVision Plugin Kit

Plugin Kit 为独立维护的 ColorVision 插件提供配置、构建、`.cvxp` 打包和市场上传入口。可以分发单文件 `cvplugin.exe`，也可以分发本目录的 Python 源码；插件工程本身由作者维护。

[外部插件 SDK 使用说明](./docs/ColorVision.Plugin.SDK.md)包含项目模板、manifest、菜单示例、本地安装和首次发布步骤，随 Kit 一同提供。仓库中的[PluginKit SDK 打包器参考](../../docs/02-developer-guide/plugin-development/sdk-packaging.md)提供完整配置表、命名规则和错误排查；本 Kit 单独分发时，回到匹配版本的仓库读取该参考。

## 运行前提

| 使用方式 | 所需环境 |
| --- | --- |
| 已构建的 `cvplugin.exe` | Windows；默认构建需要对应 .NET SDK，自定义构建需要其自己的工具链；无需另装 Python |
| Python 源码 | Python 3.10+、`pefile`；上传需要 `requests`，构建工具链同上 |
| 重新生成 exe | Python、PyInstaller 和待嵌入的依赖，包括提供上传能力的 `requests` 及其依赖 |

在所选源码/构建环境安装依赖时，以下命令会联网并修改 Python 环境；exe 使用者无需运行：

```powershell
python -m pip install pefile
python -m pip install requests
```

## 开始使用

1. 把 `cvplugin.exe` 放在插件工程目录，并从该目录运行。默认配置发现以**进程当前工作目录**为准。
2. 没有 `pluginkit.config.json` 时进入向导。选择项目或 `cmd:<命令>` 自定义构建、打包源目录、输出目录及上传设置；构建/上传默认开启，选择上传时默认不保留本地包。
3. 确认配置后写入文件并退出，本次不发布。检查配置中的路径、服务器和账户；不要执行不可信的 `buildCommand` 或把凭据提交到公开仓库。
4. 后续无参数运行读取该配置，按设置构建、打包并可能上传。上传成功且不保留包时，会删除本地 `.cvxp`；输出目录为空且运行前不存在或为空时，也会清理该空目录。

首次配置默认源目录是项目的 `bin/x64/Release/net10.0-windows`，输出目录是配置旁 `packages`。示例项目应保持项目名、主 DLL 名与 manifest `id` 一致；SDK 不以 manifest `id` 独立推导包身份，也不校验或同步 manifest 版本。打包成功后仍须确认宿主安装与加载结果。

SDK 脚本与根目录 `Scripts/package_cvxp.py` 是不同实现。SDK 显式 `--config` 不会仅因 `buildEnabled=true` 就构建，却会在打包后上传，即使配置写了 `uploadEnabled=false`。需要构建后继续发布时加 `--build`；`--build-only` 构建后退出。SDK 没有 `--validate-only` / `--no-upload`。配置模式和完整参数见 SDK 使用说明及仓库参考。

## 共享清单更新

在配置中设置 `targetHostVersion` 为插件实际支持的四段主程序版本，打包器会从上传服务器下载该版本的共享清单，不再依赖 EXE 内嵌快照。主程序正式发布时自动上传清单；尚未发布清单的旧版本不会自动补齐。`sharedFilesUrl` 可指定可信 HTTP(S) 完整地址，推荐 HTTPS。

`--check-shared-files` 仅检查清单，可能下载并写缓存，不构建/打包/上传；加 `--offline` 只读取匹配缓存。网络不可用时仅允许同来源、同版本、同框架和平台的有效缓存，404 或清单内容错误直接失败。显式 `sharedFiles` 本地文件优先，文件缺失不回退。未配置目标版本的旧配置保留内嵌清单行为并警告，不自动猜测最新宿主。

```powershell
.\cvplugin.exe --config .\pluginkit.config.json --target-host-version 1.4.14.1 --check-shared-files
```

示例版本必须换成实际目标；NuGet 包版本与主程序版本不是同一个版本号。验证清单不代表已经验证插件 ABI/运行兼容性。

## 源码入口与 exe 构建

| 文件 | 用途 |
| --- | --- |
| `scripts/package_cvxp.py` | 与 exe 相同的主入口；无参数时发现当前目录配置或进入初始化 |
| `scripts/shared_manifest.py` | 目标宿主版本清单的下载、验证与匹配缓存 |
| `scripts/shared_files.json` | 宿主共享 DLL/资源清单；由根 `Scripts/generate_shared_files.py` 同步生成，不手工维护镜像 |
| `examples/YoloWpfDemo.Commands.md` | 使用明确工程路径的构建、本地安装与发布组合 |
| `cvplugin.spec` | PyInstaller 控制台单文件配置，嵌入共享清单和运行依赖 |
| `build.bat` | 优先使用仓库 `.venv`，缺少 PyInstaller 时联网安装，然后构建 exe |

在仓库根目录通过源码使用向导或配置时：

```powershell
Push-Location .\SDK\ColorVision.PluginKit
python .\scripts\package_cvxp.py
Pop-Location
```

该命令已有配置时可能执行构建和上传，须先确认配置及发布授权。实际插件发布应从其配置所在目录运行，或使用指向该配置的显式命令；不要将 SDK 目录误认为插件工程目录。

重建 exe 时在本 SDK 目录运行 `build.bat`，或在依赖齐全的环境运行以下命令。它重建本地 `build/` 和 `dist/cvplugin.exe`，不发布插件；正常分发无需再附外部 `shared_files.json`，产物依赖是否完整仍需单独验收。

```powershell
python -m PyInstaller --noconfirm --clean .\cvplugin.spec
```
