# YoloWpfDemo 命令示例

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

```powershell
dotnet run --project $pluginProject -c Debug -p:Platform=x64
```

## 2. 构建插件

```powershell
dotnet build $pluginProject -c Debug -p:Platform=x64
```

## 3. 安装到本地 ColorVision 调试目录

```powershell
$pluginId = "YoloWpfDemo"
$source = Join-Path -Path $pluginRoot -ChildPath 'bin\x64\Debug\net10.0-windows'
$target = Join-Path -Path $colorVisionRoot -ChildPath "ColorVision\bin\x64\Debug\net10.0-windows\Plugins\$pluginId"
New-Item -ItemType Directory -Force -Path $target | Out-Null
Copy-Item -Path (Join-Path -Path $source -ChildPath '*') -Destination $target -Recurse -Force
```

## 4. 初始化 PluginKit config

```powershell
Set-Location -LiteralPath $pluginRoot
& (Join-Path -Path $pluginRoot -ChildPath 'cvplugin.exe')
```

如果你未来把 `package_cvxp.py` 打成 `cvplugin.exe`，把它放到 `$pluginRoot` 后，直接双击一次也会进入同样的配置向导，并在当前目录生成 `pluginkit.config.json`。

如果向导里选择上传，还会继续提示是否在上传成功后保留本地 `.cvxp`。

如果选择“不保留本地 `.cvxp`”，并且输出目录原本不存在或原本为空，那么上传成功后这个空目录也会被自动删除。

## 5. 打 `.cvxp` 包

```powershell
& (Join-Path -Path $pluginRoot -ChildPath 'cvplugin.exe')
```

## 6. 直接调用脚本

```powershell
python $packageScript --config $pluginConfig --build
```

无参数方式也已经可用：

```powershell
Set-Location -LiteralPath $pluginRoot
python $packageScript
```

第一次运行会生成 `pluginkit.config.json`，后续无参数执行会自动读取这个 config 并运行。

仓库内重新构建 `cvplugin.exe`：

```powershell
Set-Location -LiteralPath (Join-Path -Path $colorVisionRoot -ChildPath 'SDK\ColorVision.PluginKit')
python -m PyInstaller --noconfirm --clean .\cvplugin.spec
```

## 7. 发布插件市场

```powershell
$env:COLORVISION_UPLOAD_USERNAME = "your-user"
$env:COLORVISION_UPLOAD_PASSWORD = "your-password"

& (Join-Path -Path $pluginRoot -ChildPath 'cvplugin.exe')
```
