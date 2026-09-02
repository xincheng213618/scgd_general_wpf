---
knowledge_id: "operations.first-run"
knowledge_type: "guide"
status: "current"
summary: "主程序启动的配置、实例和服务副作用，以及隔离测试环境中的最小本地图像验证。"
aliases: ["首次启动","快速上手","试用","最小闭环","无硬件验证","打不开程序","插件未加载","普通图片启动","PNG","JPEG","TIFF","StartupFileOpenPolicy","WizardCompletionKey"]
code_paths: ["ColorVision/App.xaml.cs","ColorVision/StartWindow.xaml.cs","ColorVision/StartupFileOpenPolicy.cs","ColorVision/SingleInstanceStartupPolicy.cs","UI/ColorVision.UI/Update/ApplicationUpdateProcessCoordinator.cs","UI/ColorVision.UI/ConfigHandler.cs","UI/ColorVision.UI/Plugins/PluginLoader.cs","UI/ColorVision.UI/FileProcessorFactory.cs","UI/ColorVision.Solution/Editor/ImageEditor.cs","Engine/ColorVision.Engine/MySqlInitializer.cs","Engine/ColorVision.Engine/MQTT/MqttInitializer.cs"]
test_paths: ["Test/ColorVision.UI.Tests/StartupRecoveryPluginScannerTests.cs","Test/ColorVision.UI.Tests/SingleInstanceStartupTests.cs","Test/ColorVision.UI.Tests/StartupFileOpenPolicyTests.cs","Test/ColorVision.UI.Tests/CommonImageOpenDecodeTests.cs"]
related: ["delivery.prerequisites","platform.runtime","ui.configuration","ui.wizards","ui.image-editor"]
---

# 主程序启动与最小图像验证

本页覆盖 `App` 启动到本地图片显示的验证边界。只读源码问答不需要启动应用；构建步骤集中在[环境与构建前提](./prerequisites.md)。这里的“无硬件验证”是受限的人工检查，不是产品提供的离线沙箱模式。

## 启动前先确认副作用

- 使用已获授权的隔离测试机器或环境，核对配置和网络边界，不载入现场设备配置、不连接真实设备、不执行流程。单独复制可执行文件并不自动隔离配置或本机服务。
- 确认目标安装目录没有正在工作的实例。未附加调试器、未允许多实例时，`SingleInstanceStartupPolicy` 会进入旧实例替换分支；`App.xaml.cs` 会尝试关闭同安装路径的旧进程。命令行 `debug` 标志不会代替这里的 `Debugger.IsAttached` 判断，不能把再次启动当作无影响检查。
- 启动会初始化并可能写入配置、日志和插件目录。`App` 先将工作目录设为可执行文件目录；`ConfigHandler.InitializePaths` 优先使用已经存在的相邻 `Config/`，否则使用当前用户 ApplicationData 下按程序集公司名划分的 `Config/`。以实际 `ConfigFilePath` 为准，完整路径与恢复规则见[配置持久化与重载](../04-api-reference/ui-components/configuration.md)；不通过删除现有配置来“重置试用”。
- 启动还可能连接已配置的服务并产生系统级副作用：`MySqlInitializer` 会连接数据库，连接失败且主机为本地时可通过 `ColorVisionServiceHost` 尝试启动或修复 MySQL 服务；`MqttInitializer` 可连接 MQTT 并尝试启动本地 `mosquitto`。没有设备不代表没有这些副作用。

若当前任务只允许阅读或本地构建，不执行后续启动步骤；文档示例本身不构成运行授权。

## 进入主窗口

满足上述运行前提后，启动已安装版本的快捷方式，或启动目标安装/构建输出目录中的 `ColorVision.exe`。源码输出必须与本次构建的配置、x64 平台及运行时 DLL 匹配；不要用另一目录中已有的程序代替验收本次产物。

正常桌面启动有条件分支，不能要求每次直接出现主窗口：

1. 上次启动未被标记为健康时，可能先显示启动恢复窗口。按错误与本次授权选择退出或恢复，不默认禁用全部插件或回退版本。
2. `WizardCompletionKey` 尚未完成或恢复操作要求重新配置时，显示[设置向导](../04-api-reference/ui-components/wizards.md)。按其步骤应用、关闭和完成契约核对测试环境；下一步可能产生安装等副作用，向导不是连接现场设备的授权。
3. 进入 `StartWindow` 后执行已发现的 `IInitializer`；初始化完成再打开主窗口，异常会显示启动错误。完整启动分支及插件恢复契约见[运行时链路](../03-architecture/overview/runtime.md)。

带文件参数也不等于绕过正常启动：`StartupFileOpenPolicy` 仅让 `.cvraw` / `.cvcie` 进入主窗口前的独立打开分支，普通 PNG、JPEG、TIFF 不属于该分支；独立打开也不应被描述为无副作用沙箱。

## 最小本地图像检查

在已满足运行前提、已进入主窗口的测试环境中：

1. 准备一张已知可读、非敏感的小型 PNG 或 JPEG 文件。
2. 使用文件菜单的打开入口（`MenuFileOpen`）选择文件；菜单文字受语言资源和插件影响，以当前界面为准。
3. 确认文件进入图像编辑器，画面与样本相符，没有文件打开错误。默认编辑器由 `UI/ColorVision.Solution/Editor/ImageEditor.cs` 的 `EditorForExtension` 注册，并调用 `ImageView.OpenImage`。
4. 不执行采集、流程、算法、保存或上传操作；记录程序版本、样本格式和实际显示结果。进一步的图像行为转到[ImageEditor 图像与绘图契约](../04-api-reference/ui-components/ColorVision.ImageEditor.md)。

通过这一检查，只能说明当前环境中主窗口与该样本的文件打开/显示链可用；不能据此宣布插件全部健康、数据库已就绪、所有格式可用或真实检测流程通过。

## 失败时沿启动边界定位

| 现象 | 优先核对 |
| --- | --- |
| 仍在向导或恢复窗口 | `WizardCompletionKey`、上次启动阶段及恢复选择；这不等于主窗口已经验证通过 |
| 启动慢或启动错误 | `StartWindow` 显示的阶段、本次日志、对应 `IInitializer` 的服务连接和超时；不直接终止进程或重启服务 |
| 启动退出或配置似乎来自旧版本 | 实际可执行文件目录、`ConfigFilePath`、日志中的配置/依赖错误，以及是否触发旧实例替换；保留现有配置再定位 |
| 插件未加载 | 正常扫描默认位于可执行文件目录下的 `Plugins/`；核对恢复时是否跳过、插件启用状态、`manifest.json` 的 `id` / `dllpath`、DLL 与依赖，入口为 `PluginLoader` |
| 数据库或 MQTT 连接失败 | 实际配置的主机、端口、凭据权限及服务日志；修复、注册或启动服务需要相应授权，不把它们当成只读诊断 |
| 图片无法显示 | 实际文件格式、文件访问错误、编辑器路由和图像解码日志；只有错误指向 native 依赖时才沿输出 DLL / OpenCV runtime 排查 |

反馈问题时提供版本、复现步骤、启动阶段和脱敏错误；不要上传配置中的凭据或未经授权的客户图片。

## 验证覆盖与缺口

元数据中的测试分别覆盖恢复插件扫描、单实例策略、独立文件打开分支和 JPEG 解码等局部契约，不是完整安装或首次启动验收。是否运行过这些测试应由具体任务报告；向导、配置组合、第三方插件、现场服务和最终图像显示仍需在获授权的目标环境中验证。
