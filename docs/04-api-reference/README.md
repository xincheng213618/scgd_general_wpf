# 模块参考

本章是源码模块参考入口。项目业务说明已经提升到 [项目说明](../00-projects/README.md)，现有插件能力已经提升到 [现有插件能力说明](./plugins/README.md)。这里重点保留 UI、Engine、算法模板和扩展点这些需要回到源码模块的交接资料。

- UI DLL 怎么发布、怎么被其他模块引用、哪些资源会进入包，以及菜单、设置、插件加载、ImageEditor 等运行时组件如何交接。
- Engine 的设备、模板、流程、MQTT、结果和数据库业务链路怎么串起来。
- 现有插件从 manifest、菜单、外部依赖、现场验收到回退记录怎么交接。
- 算法模板、Flow 节点、扩展点和源码目录之间怎么对应。

## 推荐阅读顺序

1. [UI 组件与 DLL 发布](./ui-components/README.md)：先确认 UI 类库的发布形态和依赖关系。
2. [Engine 组件与业务交接](./engine-components/README.md)：再理解设备、模板、流程和结果处理的主链路。
3. [算法与模板](./algorithms/README.md)：需要细看算法模板和结果处理时再进入。
4. [扩展点概览](./extensions/README.md)：需要新增流程节点或扩展宿主能力时再进入。
5. [项目说明](../00-projects/README.md)：需要接手客户项目时从这里进入。
6. [现有插件能力说明](./plugins/README.md)：需要确认通用插件能力、现场验收和交付边界时从这里进入。

## 当前章节地图

| 章节 | 覆盖源码 | 交接重点 |
| --- | --- | --- |
| [UI 组件](./ui-components/README.md) | `UI/` | DLL/NuGet 发布、运行时发现、组件交接、资源打包、宿主引用 |
| [Engine 组件](./engine-components/README.md) | `Engine/` | 业务主链路、设备服务、模板、流程、MQTT、结果 |
| [现有插件](./plugins/README.md) | `Plugins/` | manifest、入口、依赖、能力矩阵、现场验收和回退 |
| [算法与模板](./algorithms/README.md) | `Engine/ColorVision.Engine/Templates/` | 算法模板、JSON 模板、POI/ROI、结果解析 |
| [扩展点](./extensions/README.md) | `Engine/FlowEngineLib/`、`UI/ColorVision.UI/` | Flow 节点和插件扩展入口 |

## 交接时先确认的事实

- 主程序输出目录是插件和项目包运行时的装载根。
- UI 类库多数启用了 `GeneratePackageOnBuild`，不仅是源码引用，也可以作为 DLL/NuGet 包发布。
- `Engine/ColorVision.Engine` 在源码存在时引用 UI 项目；源码缺失时部分 UI 模块会回退到 `ColorVision.*` 包引用。
- 插件与项目包通过 `manifest.json` 暴露 `Id`、`name`、`version`、`dllpath`、`requires`。
- 构建插件和项目包时，PostBuild 会把 DLL、`manifest.json`、`README.md`、`CHANGELOG.md` 复制到主程序 `Plugins/<Name>/`。
- `Scripts/package_plugin.bat` 和 `Scripts/package_project.bat` 会调用 `Scripts/package_cvxp.py` 生成 `.cvxp` 包。

## 不再作为主入口的内容

- 与当前源码目录对不上的历史插件功能页。
- 只描述理想架构、但无法回到具体类和文件的旧式说明。
- 和用户手册重复的操作步骤。

如果文档与源码不一致，以当前源码、项目文件和运行时装载行为为准，并优先更新本章对应页面。
