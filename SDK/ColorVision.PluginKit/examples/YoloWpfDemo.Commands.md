# YoloWpfDemo 命令示例

这个示例对应当前外部插件项目：

- 插件工程目录：`C:\Users\17917\Desktop\yolo-wpf-demo`
- ColorVision 源码目录：`C:\Users\17917\Desktop\scgd_general_wpf`

## 1. 独立运行 Demo

```powershell
dotnet run --project C:\Users\17917\Desktop\yolo-wpf-demo\YoloWpfDemo.csproj -c Debug -p:Platform=x64
```

## 2. 构建插件

```powershell
dotnet build C:\Users\17917\Desktop\yolo-wpf-demo\YoloWpfDemo.csproj -c Debug -p:Platform=x64
```

## 3. 安装到本地 ColorVision 调试目录

```powershell
$pluginId = "YoloWpfDemo"
$source = "C:\Users\17917\Desktop\yolo-wpf-demo\bin\x64\Debug\net10.0-windows"
$target = "C:\Users\17917\Desktop\scgd_general_wpf\ColorVision\bin\x64\Debug\net10.0-windows\Plugins\$pluginId"
New-Item -ItemType Directory -Force $target | Out-Null
Copy-Item "$source\*" $target -Recurse -Force
```

## 4. 初始化 PluginKit config

```powershell
cd C:\Users\17917\Desktop\yolo-wpf-demo
C:\Users\17917\Desktop\yolo-wpf-demo\cvplugin.exe
```

如果你未来把 `package_cvxp.py` 打成 `cvplugin.exe`，把它放到 `C:\Users\17917\Desktop\yolo-wpf-demo` 后，直接双击一次也会进入同样的配置向导，并在当前目录生成 `pluginkit.config.json`。

如果向导里选择上传，还会继续提示是否在上传成功后保留本地 `.cvxp`。

如果选择“不保留本地 `.cvxp`”，并且输出目录原本不存在或原本为空，那么上传成功后这个空目录也会被自动删除。

## 5. 打 `.cvxp` 包

```powershell
C:\Users\17917\Desktop\yolo-wpf-demo\cvplugin.exe
```

## 6. 直接调用脚本

```powershell
python C:\Users\17917\Desktop\scgd_general_wpf\SDK\ColorVision.PluginKit\scripts\package_cvxp.py --config C:\Users\17917\Desktop\scgd_general_wpf\SDK\ColorVision.PluginKit\pluginkit.config.json --build
```

无参数方式也已经可用：

```powershell
cd C:\Users\17917\Desktop\yolo-wpf-demo
python C:\Users\17917\Desktop\scgd_general_wpf\SDK\ColorVision.PluginKit\scripts\package_cvxp.py
```

第一次运行会生成 `pluginkit.config.json`，后续无参数执行会自动读取这个 config 并运行。

仓库内重新构建 `cvplugin.exe`：

```powershell
cd C:\Users\17917\Desktop\scgd_general_wpf\SDK\ColorVision.PluginKit
python -m PyInstaller --noconfirm --clean cvplugin.spec
```

## 7. 发布插件市场

```powershell
$env:COLORVISION_UPLOAD_USERNAME = "your-user"
$env:COLORVISION_UPLOAD_PASSWORD = "your-password"

C:\Users\17917\Desktop\yolo-wpf-demo\cvplugin.exe
```