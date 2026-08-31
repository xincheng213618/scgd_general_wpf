# ColorVision.Common

ColorVision 的 Windows 共享基础库，包含 MVVM、扩展接口与服务访问入口、模块登记、粗粒度权限，以及第三方工具和 Win32 辅助。目标框架、WPF/WinForms 与 NuGet 包设置以 [ColorVision.Common.csproj](./ColorVision.Common.csproj) 为准。

NuGet 包外的完整源码与知识库位于[项目仓库](https://github.com/xincheng213618/scgd_general_wpf)；以下相对链接用于仓库内阅读。

当前行为与限制统一维护在[共享接口、属性通知与粗粒度权限](../../docs/04-api-reference/ui-components/ColorVision.Common.md)。尤其注意：属性通知和命令不会自动切换线程，`Execute` 不替调用方检查 `CanExecute`；全局权限模式不等于 RBAC 登录或权限码检查。此处不再维护第二份 API 能力清单，也不承诺完整 SDK/二进制兼容。

## 本地构建

在 Windows 仓库根目录执行，需要匹配的 .NET SDK 与可还原依赖；还原可能联网，构建会产生输出，项目启用 `GeneratePackageOnBuild`。这是本地构建入口，不是启动程序、运行外部工具或上传发布。

```powershell
dotnet build .\UI\ColorVision.Common\ColorVision.Common.csproj -p:Platform=x64
```
