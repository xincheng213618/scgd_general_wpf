# ProjectLUX

ColorVision 的显示设备光学检测项目包，运行时加载 `ProjectLUX.dll`。覆盖白场与 RGB 亮色度、棋盘格对比度、MTF、畸变、光学中心、VID 和光通量，通过流程组及文本 Socket 命令连接现场自动化系统。

## 运行前提

- Windows x64 / .NET 10 WPF；需要兼容的 ColorVision 宿主、`ColorVision.Engine` 及其 Flow、通信、图像与数据库组件。
- 项目独立版本读取 `ProjectLUX.csproj` 的 `VersionPrefix`，宿主最低要求读取随包 manifest 的 `requires`；项目版本不随主程序版本自动变化。
- 运行前准备现场 Flow 模板、设备服务、Engine MySQL 数据、Recipe 限值与 Fix 修正。Recipe/Fix 按类型共享，不按每个流程步骤独立保存。
- 流程、Recipe、Fix 和生产摘要默认位于 `%APPDATA%\ColorVision\Config\`，本地结果库为 `ProjectLUX.db`。具体文件名和保存边界见项目主题。
- 外部对接使用 Socket 的 **Text** 模式，默认监听 `0.0.0.0:6666`；必须显式启用服务并核对当前活动组的 `SocketCode` 映射。命令可能触发真实设备，按现场授权范围操作。

## 查找功能说明

| 任务 | 文档 |
| --- | --- |
| 配置流程、Recipe/Fix、查询保存位置和处理类型 | [ProjectLUX](../../docs/04-api-reference/projects/project-lux.md) |
| 对接 `T00XX,SN;`、解释响应及处理超时 | [TCP 通讯协议](../../docs/04-api-reference/projects/project-lux-protocol.md) |
| 查询版本变化 | [CHANGELOG](./CHANGELOG.md) |

完整功能文档需在匹配版本的源码仓库或文档站点查看；独立交付包中的相对链接需要完整源码。

## 本地构建

在仓库根目录运行。命令生成本地产物；宿主复制条件成立时会更新宿主插件目录，不上传项目包：

```powershell
dotnet build .\Projects\ProjectLUX\ProjectLUX.csproj -c Release -p:Platform=x64
```

测试入口与打包上传命令见项目主题。只有明确发布该项目时才使用 `Scripts\package_project.bat ProjectLUX`。
