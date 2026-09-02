# ColorVision 外部插件 SDK

本文档给第三方插件作者使用。目标是让插件项目独立维护，只通过 NuGet 引用平台 SDK，最终交付 `.cvxp` 插件包。

本说明保留可随 SDK 独立交付的项目与发布示例。仓库内当前实现契约见[PluginKit SDK 打包器](../../../docs/02-developer-guide/plugin-development/sdk-packaging.md)；它与仓库根 `Scripts/package_cvxp.py` 不是同一套打包器，尤其不能互换包身份、构建/上传开关与清理规则。

## 1. 插件工程要求

- Windows WPF 插件目标框架使用 `net10.0-windows`。
- 默认平台目标使用 `x64`。
- 插件项目引用 ColorVision SDK 包，例如 `ColorVision.UI`。
- 插件输出目录必须包含 `manifest.json` 和插件主 DLL。
- 需要独立运行时保留 `App.xaml` / `App.xaml.cs` 和窗口入口；宿主加载 DLL 不会代为运行插件的 `App` 启动流程。下面模板使用 `WinExe`，需要可执行入口；只作为库时可改为 `Library`，不要求独立 App。

推荐 `.csproj` 基础模板：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <UseWPF>true</UseWPF>
    <ImplicitUsings>enable</ImplicitUsings>
    <Platforms>x64</Platforms>
    <PlatformTarget>x64</PlatformTarget>
    <GenerateDependencyFile>true</GenerateDependencyFile>
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
    <VersionPrefix>0.1.0.0</VersionPrefix>
  </PropertyGroup>

  <ItemGroup>
    <!-- 替换为与宿主其他 ColorVision 包一致的已发布版本 -->
    <PackageReference Include="ColorVision.UI" Version="PUBLISHED_VERSION" />
  </ItemGroup>

  <ItemGroup>
    <None Update="manifest.json" CopyToOutputDirectory="PreserveNewest" />
    <None Update="README.md" CopyToOutputDirectory="PreserveNewest" />
    <None Update="CHANGELOG.md" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
</Project>
```

## 2. manifest.json

插件根目录必须包含 `manifest.json`。`id` 要稳定，后续更新、安装、卸载都以它为唯一标识；示例中的 `requires` 必须替换为插件真实支持的最低宿主版本。

本 SDK 打包器不按 manifest `id` 独立确定包身份，也不自动同步 manifest `version`。下面示例刻意让项目名、DLL 名与 `id` 同为 `DemoPlugin`；维护者须核对 manifest 与实际编译产物一致，不能把打包成功当作 manifest 或兼容性已校验。

```json
{
  "manifest_version": 1,
  "id": "DemoPlugin",
  "name": "演示插件",
  "version": "0.1.0.0",
  "description": "一个独立维护的 ColorVision 插件。",
  "dllpath": "DemoPlugin.dll",
  "requires": "1.0.0.0",
  "author": "Your Name",
  "entry_point": "DemoPlugin.DemoMenuProvider",
  "icon": "PackageIcon.png"
}
```

## 3. 菜单入口

菜单由 `IMenuItemProvider` 提供。插件 DLL 通过宿主装载和依赖检查后，菜单系统发现公开无参构造、非抽象且非开放泛型的 provider，并读取有 Header 和 Command 的菜单项。manifest 的 `entry_point` 只是字段，不负责调用入口；`requires` 也不是运行装载器的版本门禁。示例需提供自己的 `MainWindow`；构造 provider 成功仍需目标窗口与菜单过滤条件允许显示。

```csharp
using ColorVision.Common.MVVM;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System.Collections.Generic;
using System.Windows;

namespace DemoPlugin;

public sealed class DemoMenuProvider : IMenuItemProvider
{
    public IEnumerable<MenuItemMetadata> GetMenuItems()
    {
        return
        [
            new MenuItemMetadata
            {
                OwnerGuid = MenuItemConstants.Tool,
                GuidId = "DemoPlugin",
                Header = "演示插件",
                Order = 600,
                Command = new RelayCommand(_ =>
                {
                    var window = new MainWindow
                    {
                        Owner = Application.Current?.GetActiveWindow(),
                        WindowStartupLocation = WindowStartupLocation.CenterOwner
                    };
                    window.Show();
                })
            }
        ];
    }
}
```

## 4. 构建和本地安装

外部插件可以在自己的仓库里构建。下面命令写本地 `bin/obj`，可能还原依赖，不上传：

```powershell
dotnet build .\DemoPlugin.csproj -c Debug -p:Platform=x64
```

开发调试时，取得覆盖目标安装目录的授权并核对占用后，可将构建输出复制到 ColorVision 运行目录。下面是会覆盖文件的开发示例，不是正式安装事务、回滚或热重载验证：

```powershell
$pluginId = "DemoPlugin"
$source = "C:\Path\To\DemoPlugin\bin\x64\Debug\net10.0-windows"
$target = "C:\Path\To\ColorVision\bin\x64\Debug\net10.0-windows\Plugins\$pluginId"
New-Item -ItemType Directory -Force $target | Out-Null
Copy-Item "$source\*" $target -Recurse -Force
```

安装完成后，插件会位于：

```text
C:\Path\To\ColorVision\bin\x64\Debug\net10.0-windows\Plugins\DemoPlugin
```

重新启动 ColorVision 且插件与菜单 provider 成功加载后，示例菜单应出现在 `工具` 菜单下；文件复制完成本身不证明已经加载。

## 5. 用单个 cvplugin.exe 完成首次配置和后续发布

最终对外建议只发一个 `cvplugin.exe`。

已构建的单文件 exe 不要求使用者安装 Python；启用默认构建步骤需要对应 .NET SDK，自定义命令需要自己的工具链。直接运行源码需 Python 3.10+、`pefile`，上传还需 `requests`。构建、上传和执行配置中的 `buildCommand` 都需要对应授权；不要执行不可信配置或把上传凭据提交到公开仓库。

使用方式：

1. 把 `cvplugin.exe` 放到插件项目根目录。
2. 第一次双击时，如果当前目录没有 `pluginkit.config.json`，它会进入交互式配置：
  - 是否配置构建步骤，默认是。
  - 优先发现当前目录直属的 `.csproj`，直属没有时再查可发现的子目录。唯一候选可直接使用；也可以明确选择 `.csproj`、能发现唯一项目的目录，或输入 `cmd:<命令>` 保存自定义构建命令。
  - 打包源目录，默认是 `bin\x64\Release\net10.0-windows`。
  - 包输出目录，默认是当前目录下的 `packages`。
  - 是否在打包完成后上传，默认上传。
  - 如果选择上传，是否在上传成功后保留本地 `.cvxp`，默认不保留。
  - 如果选择“不保留本地 `.cvxp`”，而输出目录原本不存在或原本为空，上传成功后这个空目录也会被自动删除。
3. 确认后会在当前目录生成 `pluginkit.config.json` 并退出，本次不继续发布。
4. 后续再次双击 `cvplugin.exe`，它会自动读取当前目录的 `pluginkit.config.json`，并按配置执行构建、打包和上传。

如果你在仓库里直接运行 `scripts/package_cvxp.py`，无参数时的行为和 `cvplugin.exe` 一致。

## 6. 仓库内调试入口

下面命令以 `SDK/ColorVision.PluginKit` 为当前目录；在 ColorVision 仓库根目录先执行 `Set-Location .\SDK\ColorVision.PluginKit`，不要误用根 `Scripts/package_cvxp.py`。无参数时没有 config 则先生成配置，有 config 则按配置构建/打包/上传，并非只读调试。

显式 `--config` 不启用无参数的 `auto_mode`：`buildEnabled` 不会自动触发构建，`uploadEnabled=false` 也不会阻止上传。下面命令明确构建、打包并上传包与 `LATEST_RELEASE`，须取得发布授权；只需构建时使用 `--build-only`（仍会写本地产物并可能执行自定义命令）。SDK 不支持根打包器的 `--validate-only`，也没有 `--no-upload` 参数。

```powershell
python .\scripts\package_cvxp.py --config .\pluginkit.config.json --build
```

SDK 实际生成 `{project_name}-{FileVersion}.cvxp`，包内顶层目录和上传路径 `Plugins/{project_name}` 也使用同一名字。推导优先级是显式 `--plugin-name` / 配置 `pluginName`、项目文件名；未提供这些时才尝试输出 manifest 的 `dllpath` 文件名、`.deps.json` 和 DLL。最后始终读取输出根目录 `<project_name>.dll`，不会按 manifest `id` 或嵌套 `dllpath` 独立选择主 DLL。当前 `.deps.json` 回退只去掉 `.json` 后缀，可能错误查找 `DemoPlugin.deps.dll`；明确提供正确的项目文件或 `pluginName`，避免依赖此回退。示例使用 `DemoPlugin` 统一这些名字，得到 `DemoPlugin/manifest.json`、`DemoPlugin/DemoPlugin.dll`；不同命名方案不能从根打包器的能力推断 SDK 已支持。

## 7. 上传插件市场

取得发布授权后，可在本 SDK 目录直接调用脚本。下面会构建、覆盖本地包并向指定市场上传；占位凭据不能写入公开版本库。环境变量仅在配置的 `username` / `password` 缺失或为空时生效，不会覆盖非空配置凭据：

```powershell
$env:COLORVISION_UPLOAD_USERNAME = "your-user"
$env:COLORVISION_UPLOAD_PASSWORD = "your-password"

python .\scripts\package_cvxp.py --config .\pluginkit.config.json --build --upload-url http://your-marketplace-host:9998
```

当前 `cvplugin.spec` 已把 `shared_files.json` 嵌入单文件 exe，无需额外分发该文件。只有包和 `LATEST_RELEASE` 都上传成功，且未要求保留包时，SDK 才删除本地 `.cvxp` 并按条件清理空输出目录；任一步上传失败会报错，不进入这一步成功清理。

在本 SDK 目录、已安装 PyInstaller 和所需 Python 依赖的环境重新构建 `cvplugin.exe` 时，使用下面命令；它写本地构建产物，不发布插件。`build.bat` 还会在缺少 PyInstaller 时尝试联网安装：

```powershell
python -m PyInstaller --noconfirm --clean .\cvplugin.spec
```

## 8. 版本兼容建议

- 插件版本使用四段版本号，例如 `0.1.0.0`。
- `manifest.json` 的 `requires` 写最低支持的 ColorVision 版本。
- 插件更新时保持 `id` 不变。
- 大模型、大样例数据建议放到插件目录下的 `Models/` 或单独下载，不要混入平台主仓。
- 如果插件依赖 ColorVision 已经自带的 DLL，打包脚本会根据 `shared_files.json` 去重，避免 `.cvxp` 过大。

## 9. 最小交付清单

外部插件仓库至少包含：

```text
DemoPlugin.csproj
App.xaml
App.xaml.cs
MainWindow.xaml
MainWindow.xaml.cs
DemoMenuProvider.cs
manifest.json
README.md
CHANGELOG.md
```

使用上述 `WinExe` 模板时需包含 App 文件及有效窗口启动入口；只作库的项目按其实际输出方式维护。发布给用户时交付 `.cvxp` 和必要的独立模型/数据资源，用户不需要插件源码。
