# ColorVision.UI

Windows WPF 宿主共用的壳层基础设施，提供配置、程序集发现、菜单、属性编辑、热键、产品搜索、语言和状态栏接入。它不是产品主程序，也不是所有 UI 模块的基础依赖；Common、Themes 和各业务模块有各自职责。目标框架、依赖与包设置以 [ColorVision.UI.csproj](./ColorVision.UI.csproj) 为准。

当前模块责任及各能力的唯一说明见 [壳层责任与知识入口](../../docs/04-api-reference/ui-components/ColorVision.UI.md)（`ui.framework`），不在 README 另维护一份 API 清单。

## 宿主与包接入边界

- 引用包不等于执行宿主启动。插件需由宿主调用 `PluginLoader`，各扩展仍需进入程序集发现、实例化和对应消费者的装配流程；菜单声明也不替业务注册命令处理。
- 插件加载不是只读扫描：可创建目录、保存插件配置并将 DLL 加载进当前进程。依赖预检有条件且不覆盖全部依赖，`manifest.requires` 不是启动装载器的准入门禁；禁用不卸载已加载程序集。完整边界见 [插件装载](../../docs/02-developer-guide/plugin-development/overview.md)。
- 设置编辑、保存、重载，窗口/全局热键注册，以及语言切换重启是不同动作；不要把创建控件或界面可见当作持久化、权限检查或业务初始化成功。先从模块入口选择对应契约。

README 随 NuGet 放在包根，`docs/` 不随此包保证交付。上述相对链接用于源码仓库；独立用包时，在[项目仓库](https://github.com/xincheng213618/scgd_general_wpf)按匹配版本读取主题和测试，不把当前分支当作旧包保证。

## 本地构建

从 Windows 仓库根目录执行，需要对应 SDK/WPF 环境；还原可能联网，构建写入本地产物并按项目设置生成 NuGet/符号包，不启动宿主或上传发布。

```powershell
dotnet build .\UI\ColorVision.UI\ColorVision.UI.csproj -p:Platform=x64
```
