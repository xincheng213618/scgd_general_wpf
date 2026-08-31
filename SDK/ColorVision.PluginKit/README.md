# ColorVision Plugin Kit

这个目录用于对外分发给第三方插件作者。

仓库内的当前契约见[PluginKit SDK 打包器](../../docs/02-developer-guide/plugin-development/sdk-packaging.md)；单独分发时保留本页必要前提，并提供匹配版本的 `docs/ColorVision.Plugin.SDK.md` 使用说明。SDK 脚本与仓库根目录的 `Scripts/package_cvxp.py` 是不同实现，不能混用参数或包身份规则。

对外分发时，插件作者只需要 `cvplugin.exe`。第一次双击时如果当前目录还没有 `pluginkit.config.json`，它会在 cmd 里引导用户完成配置；后续再双击就会自动按 config 执行构建、打包和上传。

## 目录说明

- `docs/ColorVision.Plugin.SDK.md`
  - 外部插件接入说明。
- `cvplugin.spec`
  - 仓库内用于构建单文件 `cvplugin.exe` 的 PyInstaller spec。
- `build.bat`
  - 仓库内一键重建 `cvplugin.exe` 的脚本，优先使用仓库 `.venv`。
- `examples/YoloWpfDemo.Commands.md`
  - 以 `YoloWpfDemo` 为例的常用命令。
- `scripts/package_cvxp.py`
  - 核心脚本。现在支持无参数交互式生成 config、自动读取当前目录 config 执行、`--config`、`--init-config` 和 `--build-only`。
- `scripts/shared_files.json`
  - 与平台共享 DLL/资源清单，用于瘦身 `.cvxp` 包；它由仓库根目录的 `Scripts/generate_shared_files.py` 与仓库镜像一次扫描同步生成，不要手工编辑。

## 推荐使用流程

1. 把构建或分发得到的 `cvplugin.exe` 放到插件项目目录。
2. 第一次双击时，如果当前目录没有 `pluginkit.config.json`，它会提示：
   - 是否配置构建步骤。
   - 默认使用当前目录下的单个 `.csproj`，也可以改成别的 `.csproj`、别的项目目录，或输入 `cmd:<命令>` 作为自定义构建命令。
   - 打包源目录，默认是 `bin\x64\Release\net10.0-windows`。
   - 是否在打包完成后上传，默认上传。
3. 确认后会在当前目录写入 `pluginkit.config.json`。
4. 后续再双击 `cvplugin.exe`，会自动读取当前目录的 `pluginkit.config.json`，并按配置执行构建、打包和上传。

首次无配置仅写配置后退出；后续无参数运行可能构建、覆盖本地包并上传。配置可以包含 `buildCommand` 和上传凭据，只运行自己信任的配置，不把配置文件提交到公开仓库。

仓库内从根目录复现该流程时，先进入本 SDK 目录（Windows 下根 `Scripts/` 不是这里的 `scripts/`）。以下不是只读验证：已有配置时会执行配置中的构建/发布动作。

```powershell
Push-Location .\SDK\ColorVision.PluginKit
python .\scripts\package_cvxp.py
Pop-Location
```

无参数行为和 `cvplugin.exe` 一致。显式 `--config` 则不是相同模式：不会仅因 `buildEnabled=true` 就构建，且即使 `uploadEnabled=false` 仍会上传；构建须显式加 `--build`，`--build-only` 执行构建后退出、不打包上传。SDK 没有 `--validate-only` 或 `--no-upload` 参数。

SDK 以推导的 `project_name` 定位输出根目录下的同名 DLL，并决定包名、包内根目录和市场路径，不以 manifest `id` 作为独立打包身份。示例项目应保持项目名、主 DLL 名与 manifest `id` 一致；不同名称的场景先核对当前主题中的推导顺序，不能直接套用根打包器的规则。

## 运行前提

- 使用已构建的单文件 `cvplugin.exe`：Windows；启用默认 .NET 构建步骤时还需要对应 .NET SDK，自定义构建命令需要它自己的工具链。不要求使用者另装 Python。
- 直接运行 Python 源码：Python 3.10+ 与 `pefile`；上传还需要 `requests`。构建步骤的工具链前提与 exe 相同。
- 重建 exe：构建环境需要 Python、PyInstaller 和打入包内的依赖；提供上传功能的 exe 应在有 `requests` 及其依赖的环境构建。`build.bat` 缺少 PyInstaller 时会联网安装，并写本地 `build/`、`dist/`，不是仅做检查。

仅在源码运行/构建环境安装最小依赖；下面的 pip 命令会联网并修改所选 Python 环境：

```powershell
python -m pip install pefile
```

需要上传时再安装：

```powershell
python -m pip install requests
```

## 备注

- 这个目录可以单独拷贝出去使用。
- `pluginkit.config.json` 现在会额外记录 `buildEnabled`、`uploadEnabled`、`keepPackageAfterUpload`，并且支持 `buildCommand` 这种自定义构建命令。
- `cvplugin.spec` 已把 `shared_files.json` 打进单文件 exe 资源，不需要用户再放一个外部副本；实际分发产物仍需单独验收。
- 如果只是想重新生成 `cvplugin.exe`，直接双击 `build.bat` 即可。
- 如果 `keepPackageAfterUpload = false`，上传成功后不仅会删除本地 `.cvxp`，还会在输出目录原本不存在或原本为空时一并删掉空的输出目录。
- 在本 SDK 目录重新构建 exe 时，可以运行：`python -m PyInstaller --noconfirm --clean cvplugin.spec`；会重建本地产物，不发布插件。
- 如果你更新了 ColorVision 的插件协议、打包格式或上传接口，优先同步更新这个目录。
