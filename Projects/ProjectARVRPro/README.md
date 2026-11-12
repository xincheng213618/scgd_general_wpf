# ProjectARVRPro

ColorVision 的 AR/VR 光学检测项目包，运行时加载 `ProjectARVRPro.dll`。支持亮色度、视场角、棋盘格、MTF 与 VR 圆环分区、畸变、光学中心、圆环鬼影、AOI 与 Demura 等流程，按产品方案配置测试顺序、解析类型和 Recipe。

## 运行前提

- Windows x64 / .NET 10 WPF；项目依赖 `ColorVision.Engine` 及其 Flow、通信、图像和数据库运行组件，不能只复制项目 DLL 单独运行。
- 宿主版本要求读取随包 `manifest.json` 的 `requires`。项目版本独立于主程序；手工修改 `ProjectARVRPro.csproj` 的 `VersionPrefix`，打包器从主 DLL 同步 manifest 版本。
- 运行检测需要现场的 Flow 模板、设备服务、Engine MySQL 数据和匹配的 Recipe；本地结果保存在 SQLite。
- “算法实证”支持发光区、十字 RGB 和点阵畸变，打开和换图只加载原图，点击“开始验证”才计算；需项目包与宿主算法组件配套更新。服务端 AA / 旧版畸变调用可选的 `CV_algorithm.dll`，需配套 `opencv_world401.dll` 和 VC++ 运行库，可从主服务发现或手选。参考参数不等于历史参数，可选模板或导入 JSON；不回写检测结果。
- 流程配置默认位于 `%APPDATA%\ColorVision\Config\ProjectARVRProProcessGroups.json`，结果库为同目录 `ProjectARVRPro.db`。首次升级仅在能确认旧共享 `ProcessGroups.json` 属于 ARVRPro 时复制迁移，原文件保留；Recipe 随流程或解析实例保存。
- 承接 LUX 客户端时，宿主 Socket 解析模式选 `Text`；在“流程处理配置 → 其他配置”填写 LUX 设备号和 VID 设置，活动流程组填写 SocketCode。原 Pro 对接继续选 `Json`。两种协议均须在“其他配置”选择 VID 相机；微聚焦仅支持本地取图，使用所选相机的对焦模板。VID/光通量指令不自动显示检测窗口。
- 需要三轴回包的步骤选择“ARVR → 光学与几何 → 光学中心（回包）”，切换后核对 Recipe 限值和 K/B；已有配置不会自动换类型。选择类型即启用回包，设备号只决定前缀、可保持默认 `H03`，SocketCode 按步骤配置，避开 `00`、`01`、`31`。三轴回包只读本次流程快照，缺值不沿用旧结果。FindCross 模板仍使用 `optCenter` / `ImageCenter` 名称区分光学中心与图像中心。
- 普通光学中心和回包类型的“输出Key”默认 `Optical_Center`，可按需修改 CSV 与批次指标分组；修改 Key 不改变三轴回包内容。相关流程原“导出名称”统一显示为“输出Key”，旧配置字段与默认值保留。
- 旧 LUX CSV：在“模组检测设置 → 结果保存”开启“保存客户报表”，报表格式选 `LUX_Current_CSV` 或 `LUX_Legacy_CSV`，设置报表保存目录。Legacy 的“MTF 点位格式”默认 058（0F/0.5F/0.8F），需要旧点位名称的项目可选 048（0F/0.4F/0.8F）。普通检测结束或 LUX 文本流程完成时生成 `C_<SN>.csv`，同 SN 后续命令更新该文件；留空目录使用结果输出目录。POI 没有单独限值时用默认 `0/0` 计算 pass/fail，未生成的固定字段沿用旧 LUX 模型默认值。VID/光通量仍输出原有 B_/D_ 文件。
- 真实运行可能切换图案、控制设备及写入结果。Demura 默认“仅返回CSV”，W128 文件存在且可读取即通过；关闭该选项后，完整处理的 `BurnAfterGenerate` 默认开启，须核对源文件和目标设备操作范围。

旧 AOI 模板升级时，将仅用于中转的通用传感器切图节点替换为「ProjectARVRPro → 外部切图」，核对等待超时及完成后延时，并保留外部客户端的主 Socket 连接。旧服务端 Flow 需改为宿主进程内执行。模板不会自动迁移；确认无其它指令引用后再清理专用传感器配置。协议事件名保持 `AoiSwitchPG` / `AOITestSwitchImageComplete`。

## 查找功能说明

| 任务 | 文档 |
| --- | --- |
| 配置流程组、解析映射、Recipe 和雷鸟切图；选择处理类型 | [流程与解析配置](../../docs/04-api-reference/projects/project-arvr-pro-processes.md) |
| Socket 自动化、输出格式、历史结果图、结果统计 | [ProjectARVRPro](../../docs/04-api-reference/projects/project-arvr-pro.md) |
| 配置旧 LUX Current/Legacy CSV 与现场核对 | [LUX 客户报表](../../docs/04-api-reference/projects/project-arvr-pro-lux-export.md) |
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
