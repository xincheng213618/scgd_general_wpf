# ProjectARVRPro

ColorVision 的 AR/VR 光学检测项目包，运行时加载 `ProjectARVRPro.dll`。支持亮色度、视场角、棋盘格、MTF、畸变、光学中心、AOI 与 Demura 等流程，按产品方案配置测试顺序、解析类型和 Recipe。

## 运行前提

- Windows x64 / .NET 10 WPF；项目依赖 `ColorVision.Engine` 及其 Flow、通信、图像和数据库运行组件，不能只复制项目 DLL 单独运行。
- 宿主版本要求读取随包 `manifest.json` 的 `requires`。项目版本独立于主程序；手工修改 `ProjectARVRPro.csproj` 的 `VersionPrefix`，打包器从主 DLL 同步 manifest 版本。
- 运行检测需要现场的 Flow 模板、设备服务、Engine MySQL 数据和匹配的 Recipe；本地结果保存在 SQLite。
- 流程配置默认位于 `%APPDATA%\ColorVision\Config\ProcessGroups.json`，结果库为同目录 `ProjectARVRPro.db`。Recipe 随流程或解析实例保存；使用其他项目的配置前必须核对格式与业务含义。
- 真实运行可能切换图案、控制设备及写入结果。Demura 的 `BurnAfterGenerate` 默认开启，运行该类型前须核对源文件和目标设备操作范围。

## 查找功能说明

| 任务 | 文档 |
| --- | --- |
| 配置流程组、解析映射、Recipe 和雷鸟切图；选择处理类型 | [流程与解析配置](../../docs/04-api-reference/projects/project-arvr-pro-processes.md) |
| Socket 自动化、输出格式、历史结果图、结果统计 | [ProjectARVRPro](../../docs/04-api-reference/projects/project-arvr-pro.md) |
| 对接外部产线控制程序 | [TCP 通讯协议](../../docs/04-api-reference/projects/project-arvr-pro-protocol.md) |
| 配置 Demura、生成 GECS 帧、定位烧录失败 | [Demura 烧录与 PG 通信](../../docs/04-api-reference/projects/project-arvr-pro-demura.md) |
| 查询版本变化 | [CHANGELOG](./CHANGELOG.md) |

完整功能文档需在匹配版本的源码仓库或文档站点查看；独立交付包中的 README 保留运行前提，仓库相对链接需要完整源码。

## 本地构建

在仓库根目录运行。构建会生成本地产物，并向宿主 Debug/Release 的插件目录复制项目文件，不上传项目包：

```powershell
dotnet build .\Projects\ProjectARVRPro\ProjectARVRPro.csproj -c Release -p:Platform=x64
```

测试入口与打包上传命令见项目主题。只有明确发布该项目时才使用 `Scripts\package_project.bat ProjectARVRPro`。
