# ColorVision Plugin Kit

这个目录用于对外分发给第三方插件作者。

目标是让插件作者最后只拿到一个 `cvplugin.exe`。第一次双击时如果当前目录还没有 `pluginkit.config.json`，它会在 cmd 里引导用户完成配置；后续再双击就会自动按 config 执行构建、打包和上传。

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
  - 与平台共享 DLL/资源清单，用于瘦身 `.cvxp` 包。

## 推荐使用流程

1. 把未来打包出来的 `cvplugin.exe` 放到插件项目目录。
2. 第一次双击时，如果当前目录没有 `pluginkit.config.json`，它会提示：
   - 是否配置构建步骤。
   - 默认使用当前目录下的单个 `.csproj`，也可以改成别的 `.csproj`、别的项目目录，或输入 `cmd:<命令>` 作为自定义构建命令。
   - 打包源目录，默认是 `bin\x64\Release\net10.0-windows`。
   - 是否在打包完成后上传，默认上传。
3. 确认后会在当前目录写入 `pluginkit.config.json`。
4. 后续再双击 `cvplugin.exe`，会自动读取当前目录的 `pluginkit.config.json`，并按配置执行构建、打包和上传。

仓库内调试这个流程时，也可以直接运行 `scripts/package_cvxp.py`；无参数行为和未来的 exe 是一致的。

仓库内如果不想先打 exe，也可以直接运行：

```powershell
python .\scripts\package_cvxp.py
```

它的无参数行为和 `cvplugin.exe` 一致。

## 运行前提

- Windows
- .NET SDK
- Python 3.10+
- 至少安装 `pefile`
- 如果要直接上传，再安装 `requests`

安装最小依赖：

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
- 如果未来把 `package_cvxp.py` 打成单个 `cvplugin.exe`，需要把 `shared_files.json` 一起打进 exe 资源里；脚本已经兼容 PyInstaller 这类运行时解包目录。
- 如果只是想重新生成 `cvplugin.exe`，直接双击 `build.bat` 即可。
- 如果 `keepPackageAfterUpload = false`，上传成功后不仅会删除本地 `.cvxp`，还会在输出目录原本不存在或原本为空时一并删掉空的输出目录。
- 仓库内重新构建 exe 时，可以运行：`python -m PyInstaller --noconfirm --clean cvplugin.spec`。
- 如果你更新了 ColorVision 的插件协议、打包格式或上传接口，优先同步更新这个目录。