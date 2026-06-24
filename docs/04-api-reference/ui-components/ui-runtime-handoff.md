# UI 运行时组件

这页只回答一个问题：主程序启动后，菜单、设置、插件、状态栏、热键、图像工具、Socket、调度和 Solution 编辑器是怎么被发现的。

具体控件和窗口源码看 [UI 组件目录](./control-catalog.md)，DLL 边界看 [UI DLL 速查](./component-handbook.md)，发布检查看 [UI DLL 发布](./publishing.md)。

## 先分流

| 现象 | 第一检查点 | 下一页 |
| --- | --- | --- |
| 插件目录存在但功能没出现 | 插件是否被 `PluginLoader` 加载，程序集是否刷新 | [ColorVision.UI](./ColorVision.UI.md) |
| 菜单没有出现 | `OwnerGuid`、`GuidId`、权限过滤、目标窗口 | [UI 组件目录](./control-catalog.md) |
| 设置项找不到 | `IConfigSettingProvider` 或 `[ConfigSetting]` 是否被扫描 | [ColorVision.UI](./ColorVision.UI.md) |
| PropertyGrid 显示不对 | 属性是否 public get/set，编辑器类型是否可创建 | [ColorVision.UI](./ColorVision.UI.md) |
| 状态栏项缺失 | Provider 是否被发现，是否触发刷新事件 | [UI 组件目录](./control-catalog.md) |
| 图片工具栏少按钮 | ImageEditor 工具是否被工厂发现 | [ColorVision.ImageEditor](./ColorVision.ImageEditor.md) |
| Socket 有连接但业务不跑 | 消息历史、协议模式、`EventName`、handler 程序集 | [ColorVision.SocketProtocol](./ColorVision.SocketProtocol.md) |
| 调度任务不执行 | Quartz 是否启动，任务配置和历史库是否正常 | [UI 组件目录](./control-catalog.md) |
| Solution 文件打不开 | 扩展名编辑器是否注册，文件锁和权限是否正常 | [ColorVision.Solution](./ColorVision.Solution.md) |

不要因为问题发生在 WPF 窗口里，就直接归到 `ColorVision.UI`。先判断它属于发现链、控件目录、业务项目包还是 Engine 结果链路。

## 主链路

主程序启动后先加载配置和插件，插件程序集进入 `AssemblyHandler` / `AssemblyService`，再由菜单、设置、状态栏、热键、ImageEditor 工具、Socket、Scheduler 和 Solution 编辑器各自扫描扩展点。UI 扩展不出现时，先确认类型所在程序集已经进入 `AssemblyService`，再查具体扩展点。

## 发现机制

| 能力 | 发现入口 | 实现方式 | 常见失败原因 |
| --- | --- | --- | --- |
| 插件 | `PluginLoader.LoadPlugins("Plugins")` | `manifest.json`、`DllName`、`.deps.json` | 插件被禁用、DLL 缺失、依赖版本不满足 |
| 菜单 | `MenuManager.LoadMenuForWindow` | `IMenuItem`、`IMenuItemProvider`、`MenuItemAttribute` | `OwnerGuid` 错、权限过滤、窗口目标名不匹配 |
| 设置 | `ConfigSettingManager.GetAllSettings` | `IConfigSettingProvider`、`[ConfigSetting]` | 类型未加载、配置未进 `ConfigService`、搜索过滤隐藏 |
| 属性编辑 | `PropertyEditorWindow` | 属性元数据、`PropertyEditorTypeAttribute` | 属性不可写、编辑器构造失败、类型不匹配 |
| 状态栏 | `StatusBarManager` | `IStatusBarProvider`、`IStatusBarProviderUpdatable` | Provider 未发现、刷新事件没触发 |
| 热键 | `HotkeyService` | `IHotKey`、窗口热键注册 | 冲突、禁用、焦点范围不对 |
| 图像打开/工具 | ImageEditor 工厂 | `IImageOpen`、`IEditorTool`、右键菜单接口 | 扩展名不匹配、构造参数不匹配、可见性配置隐藏 |
| Socket | `SocketManager` | `ISocketJsonHandler`、`ISocketTextDispatcher` | 模式选错、`EventName` 不匹配、handler 程序集未加载 |
| 调度 | `QuartzSchedulerManager` | Quartz Job 和任务配置 | `scheduler_tasks.json`、Job 类型、历史库异常 |
| Solution 编辑器 | `EditorManager` | `IEditor` 和扩展名标注 | 扩展名未注册、文件锁、布局恢复异常 |

## 改动时怎么落点

| 你要做什么 | 优先落点 | 不要做什么 |
| --- | --- | --- |
| 新增共享契约、命令、基础 ViewModel | `ColorVision.Common` | 引入高层窗口或项目业务 |
| 新增菜单、设置、状态栏、热键 | `ColorVision.UI` 或实现对应 Provider | 直接操作主窗口控件 |
| 新增主题资源或窗口外观 | `ColorVision.Themes` | 把业务菜单塞进主题库 |
| 新增图像打开器、工具、overlay | `ColorVision.ImageEditor` 或 Engine result handler | 把客户导出/MES 字段写进 ImageEditor |
| 新增数据库浏览入口 | `ColorVision.Database` Provider | 在业务窗口里手写另一套数据库浏览器 |
| 新增本地 TCP 指令 | 项目包 handler + `ColorVision.SocketProtocol` | 在通用 Socket 模块里写项目流程 |
| 新增调度任务 | Scheduler Job 或项目任务入口 | 把长耗时算法写在 UI 调度窗口里 |
| 新增工作区编辑器 | `ColorVision.Solution` Editor | 把设备控制流程写进 Solution 壳层 |

## 常见故障

| 现象 | 判断顺序 |
| --- | --- |
| 插件安装后没有菜单 | 先看插件加载日志，再看 `AssemblyHandler`，最后看菜单 `OwnerGuid` 和权限 |
| 菜单有但点击无反应 | 查 `CanExecute`、目标服务初始化、异常日志 |
| 设置修改后丢失 | 查保存路径、文件权限、配置对象序列化 |
| PropertyGrid 空白 | 查属性 public get/set、对象是否为编辑副本、元数据是否完整 |
| 状态栏显示旧状态 | 查 Provider 是否实现可更新接口，刷新事件是否触发 |
| 搜索找不到入口 | 查菜单是否已扫描，搜索索引是否重建 |
| 图片 overlay 坐标不对 | 查 Draw 坐标系，再查 Engine result handler 坐标转换 |
| Socket 收到消息但项目没跑 | 查 `EventName`、handler 加载、项目入口流程 |
| 插件市场下载失败 | 查后端地址、下载器、目录权限；这不等于插件业务失败 |

结果 overlay 和业务判定问题继续看 [Engine 结果展示链路](../engine-components/result-handoff-chain.md)。项目私有流程、设备控制、MES 字段和客户导出格式不要放进通用 UI 运行时页。

## 最小验证

| 改动范围 | 最小验证 |
| --- | --- |
| 插件加载 | 放入测试插件，确认 manifest、依赖提示、程序集刷新正常 |
| 菜单/状态栏/热键 | 启动主程序，确认入口出现、排序正确、命令可执行 |
| 设置/PropertyGrid | 打开设置或属性编辑器，搜索、修改、保存、重启后仍生效 |
| 主题控件 | 切换 Dark/White/Pink/Cyan，确认资源、图标、标题栏正常 |
| ImageEditor | 打开普通图片和一类业务结果图，确认工具栏、缩放、overlay |
| 数据库/Socket/调度 | 打开管理窗口，确认连接、消息或任务历史能读写 |
| Solution | 打开 `.cvsln`，新建文件，打开编辑器，启动终端，保存布局 |
