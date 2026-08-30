# YoloWpfDemo 命令示例

本页只保留外部示例项目的路径与命令组合；SDK 的配置模式、包身份、上传和清理契约见[PluginKit SDK 打包器](../../../docs/02-developer-guide/plugin-development/sdk-packaging.md)。源码仓库外分发时，还需读取匹配版本的 `../docs/ColorVision.Plugin.SDK.md`；这些命令不是只读文档验证步骤。

在同一个 PowerShell 会话中，先输入插件工程和 ColorVision 源码的实际目录：

```powershell
$pluginRoot = (Resolve-Path -LiteralPath (Read-Host '请输入 YoloWpfDemo 工程目录') -ErrorAction Stop).Path
$colorVisionRoot = (Resolve-Path -LiteralPath (Read-Host '请输入 ColorVision 源码目录') -ErrorAction Stop).Path
$pluginProject = Join-Path -Path $pluginRoot -ChildPath 'YoloWpfDemo.csproj'
$packageScript = Join-Path -Path $colorVisionRoot -ChildPath 'SDK\ColorVision.PluginKit\scripts\package_cvxp.py'
$pluginConfig = Join-Path -Path $pluginRoot -ChildPath 'pluginkit.config.json'

if (-not (Test-Path -LiteralPath $pluginProject -PathType Leaf)) {
    throw "未找到插件项目: $pluginProject"
}
if (-not (Test-Path -LiteralPath $packageScript -PathType Leaf)) {
    throw "未找到 PluginKit 打包脚本: $packageScript"
}
```

后文命令均使用这两个根目录变量：

- 插件工程目录：`$pluginRoot`
- ColorVision 源码目录：`$colorVisionRoot`

## 1. 独立运行 Demo

以下会构建并启动外部插件工程，执行其代码；需要相应 .NET 工具链及运行授权，不能由本页推断第三方项目没有网络、文件或设备副作用。

```powershell
dotnet run --project $pluginProject -c Debug -p:Platform=x64
```

## 2. 构建插件

写本地产物并执行该项目的构建目标；不调用本页的打包器，不代表已安装到宿主。

```powershell
dotnet build $pluginProject -c Debug -p:Platform=x64
```

## 3. 安装到本地 ColorVision 调试目录

取得目标安装目录的写入/覆盖授权后执行。此例直接覆盖现有文件，不备份、不删除旧版本多余文件，也不等待运行中的宿主释放 DLL；复制成功不是插件已加载或完整目录替换成功。

```powershell
$pluginId = "YoloWpfDemo"
$source = Join-Path -Path $pluginRoot -ChildPath 'bin\x64\Debug\net10.0-windows'
$target = Join-Path -Path $colorVisionRoot -ChildPath "ColorVision\bin\x64\Debug\net10.0-windows\Plugins\$pluginId"
New-Item -ItemType Directory -Force -Path $target | Out-Null
Copy-Item -Path (Join-Path -Path $source -ChildPath '*') -Destination $target -Recurse -Force
```

## 4. 初始化 PluginKit config

无参数命令只有在当前目录没有配置时才进入初始化；已有配置时会执行构建/打包/上传。这里先拒绝已有配置，避免将发布误当成初始化。预先将已构建的 `cvplugin.exe` 放到插件目录；本段会写配置。

```powershell
Set-Location -LiteralPath $pluginRoot
if (Test-Path -LiteralPath $pluginConfig) {
    throw '配置已存在；无参数执行将使用该配置，不能作为初始化检查。'
}
& (Join-Path -Path $pluginRoot -ChildPath 'cvplugin.exe')
```

确认写入后本次退出，不继续发布；双击是否进入向导同样取决于进程工作目录有无 `pluginkit.config.json`，不是无条件进入。

如果向导里选择上传，还会继续提示是否在上传成功后保留本地 `.cvxp`。

如果选择“不保留本地 `.cvxp`”，并且输出目录原本不存在或原本为空，那么上传成功后这个空目录也会被自动删除。

## 5. 按配置构建、打包并可能上传

仅在已核对可信配置并取得相应构建/发布授权后执行。已有配置的无参数模式按 `buildEnabled` / `uploadEnabled` 工作；上传默认开启，成功后还可能删除本地包和空输出目录，不是只生成 `.cvxp` 的命令。

```powershell
Set-Location -LiteralPath $pluginRoot
& (Join-Path -Path $pluginRoot -ChildPath 'cvplugin.exe')
```

## 6. 直接调用脚本

显式 `--config` 不启用无参数的 `auto_mode`；下面明确构建、打包并上传，即使配置写了 `uploadEnabled=false`。需要发布授权；仅构建应改用 `--build-only`，它仍会执行构建或配置中的自定义命令，不是只读检查。

```powershell
python $packageScript --config $pluginConfig --build
```

无参数方式与 exe 使用相同的当前目录配置发现规则，仍须先核对是否已有配置及其副作用：

```powershell
Set-Location -LiteralPath $pluginRoot
python $packageScript
```

没有配置时生成 `pluginkit.config.json` 后退出；已有配置时自动读取并执行，不按“第一次/第二次调用”计数。

仓库内重新构建 `cvplugin.exe`：需要 Python、PyInstaller 和待嵌入依赖，会写入/重建本地产物，不发布插件。

```powershell
Set-Location -LiteralPath (Join-Path -Path $colorVisionRoot -ChildPath 'SDK\ColorVision.PluginKit')
python -m PyInstaller --noconfirm --clean .\cvplugin.spec
```

## 7. 发布插件市场

需要发布授权。下列环境变量仅在配置缺少对应 `username` / `password` 或值为空时作为回退，不能覆盖非空配置凭据；先核对目标服务器及配置，不要把真实凭据提交到仓库。无参数模式若配置关闭上传则不发布，不能仅凭进程成功退出报告市场已更新。

```powershell
$env:COLORVISION_UPLOAD_USERNAME = "your-user"
$env:COLORVISION_UPLOAD_PASSWORD = "your-password"

Set-Location -LiteralPath $pluginRoot
& (Join-Path -Path $pluginRoot -ChildPath 'cvplugin.exe')
```
