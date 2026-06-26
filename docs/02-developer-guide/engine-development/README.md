# Engine 开发指南

Engine 开发要先确认你改的是哪条业务链。不要把设备、模板、Flow、算法结果和项目判定混在一个地方改。

## 先读总览

如果你第一次维护 Engine，请先读：

- [Engine 组件总览](../../04-api-reference/engine-components/README.md)

本目录下的页面用于补充具体开发主题。

## 常见修改点

| 修改目标 | 主要目录 | 说明 |
| --- | --- | --- |
| 新增设备服务 | `Engine/ColorVision.Engine/Services/Devices/` | 通过 `DeviceServiceFactoryRegistry` 注册 |
| 新增模板 | `Engine/ColorVision.Engine/Templates/` | 实现 `ITemplate<T>` 或 `ITemplateJson<T>` |
| 新增流程节点 | `Engine/ColorVision.Engine/Templates/Flow/`、`Engine/FlowEngineLib/` | 同时补节点和配置器 |
| 修改 MQTT 行为 | `Engine/ColorVision.Engine/MQTT/`、设备服务目录 | 检查 topic、命令参数和返回结果 |
| 修改 OpenCV/native | `Engine/cvColorVision/`、`UI/ColorVision.Core/` | 检查 native DLL 和 runtime 打包 |
| 修改结果展示 | `Templates/*/ViewHandle*.cs`、`UI/ColorVision.ImageEditor/` | 不要把客户判定写进通用 handler |

## 本章节页面

- [服务开发](./services.md)
- [模板系统开发](./templates.md)
- [MQTT 消息处理](./mqtt.md)
- [OpenCV 集成开发](./opencv-integration.md)

## 开发验证

至少做三类验证：

1. 构建目标模块和主程序：

```powershell
dotnet build Engine/ColorVision.Engine/ColorVision.Engine.csproj -c Release -p:Platform=x64
dotnet build ColorVision/ColorVision.csproj -c Release -p:Platform=x64
```

2. 运行一条能覆盖修改点的流程模板。

3. 如果修改结果解析，打开图像结果和项目包导出文件同时确认。

## 维护原则

- 设备服务只处理设备状态、命令和配置，不承载客户项目判定。
- 模板只管理参数、编辑和算法命令，不直接写客户 CSV。
- Flow 节点只做可视化执行单元，业务结果由模板/项目层解析。
- 项目包的 Process/Recipe/Fix 承载客户规则。
