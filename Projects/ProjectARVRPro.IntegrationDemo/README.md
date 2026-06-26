# ProjectARVRPro 客户对接 Demo

这个目录是给外部系统、客户 MES、PLC 上位机或自动化中控使用的最小对接示例。它是 .NET Framework 4.8 + WPF 窗口项目，不依赖 ColorVision、ARVRPro 内部项目、算法 DLL、数据库、流程配置或 NuGet 包，只保留可以公开给客户的通信和结果契约。

它演示四件事：

1. 通过 TCP 连接 ARVRPRO 默认端口 `6666`。
2. 发送 `ProjectARVRInit` / `SwitchPGCompleted` / `RunAll` / `AOITestSwitchImageComplete`。
3. 用 WPF 窗口查看 `ProjectARVRResult`、测试项表格和 `W51TestResult`。
4. 收到最终 `ProjectARVRResult` 后自动保存原始 JSON 并导出扁平 CSV。

## 公开代码边界

可以直接给客户看的对接模型在：

- [Contracts/ObjectiveTestResult.cs](Contracts/ObjectiveTestResult.cs)
- [Contracts/ObjectiveTestItem.cs](Contracts/ObjectiveTestItem.cs)
- [Contracts/Process/W51/W51TestResult.cs](Contracts/Process/W51/W51TestResult.cs)
- [Contracts/Process](Contracts/Process)
- [Contracts/Socket](Contracts/Socket)
- [Contracts/MVVM/ViewModelBase.cs](Contracts/MVVM/ViewModelBase.cs)

其中包含请求/响应壳、`SwitchPG`、完整 `ObjectiveTestResult`，以及按 ARVRPro 目录拆分的各类 `*TestResult`：

```csharp
public class W51TestResult : ViewModelBase
{
    public ObjectiveTestItem HorizontalFieldOfViewAngle { get; set; }
    public ObjectiveTestItem VerticalFieldOfViewAngle { get; set; }
    public ObjectiveTestItem DiagonalFieldOfViewAngle { get; set; }
}
```

这份契约代码只描述 JSON 字段，并内置了一个可独立复制的 `ViewModelBase`，不依赖 ARVRPro 的流程、算法、数据库、UI 或任何内部项目。

`ObjectiveTestResult` 已覆盖当前 ARVRPro `ObjectiveTestResult` 的所有顶层静态属性：`W25TestResult`、`W51TestResult`、`W255TestResult`、`BlackTestResult`、`RedTestResult`、`GreenTestResult`、`BlueTestResult`、`ChessboardTestResult`、`MTFHVTestResult`、`MTFHV048TestResults`、`MTFHV058TestResults`、`DistortionTestResult`、`OpticCenterTestResult`、`DynamicTestResults`、`Msg`、`TotalResult`、`TotalResultString`。

其中每个 `*TestResult` 内部的公开测试项也按当前 ARVRPro 输出字段补齐。`DistortionTestResult.OpticDistortion` 在 JSON 中实际字段名是 `Optic_Distortion`，契约里保留了 `Optic_Distortion` 字段，并提供 `OpticDistortion` 便捷属性。

## 窗口版

无参数启动会打开 WPF 窗口：

```powershell
dotnet run --project Projects/ProjectARVRPro.IntegrationDemo
```

如果已经编译或发布，也可以直接运行：

```powershell
ProjectARVRPro.IntegrationDemo.exe
```

窗口里可以：

- 加载样例 JSON。
- 打开客户现场保存的 `ProjectARVRResult` JSON。
- 直观看到 `W51TestResult` 的三个字段。
- 查看所有 `ObjectiveTestItem` 的扁平表格，表格列可拖动调整，长字段可横向滚动。
- 设置输出目录、接收超时秒数和最大消息数。
- 连接 ARVRPRO TCP Server，执行 `ProjectARVRInit` 或 `RunAll`。
- 自动去重确认 `SwitchPG` / `AoiSwitchPG`，避免同一切图请求被重复确认。
- 收到 `ProjectARVRResult` 后自动保存 JSON 并导出 CSV，路径会显示在通信日志里。
- 在“字段说明”页查看常用光学参数含义。
- 手动保存当前扁平 CSV。

也可以显式指定窗口模式：

```powershell
dotnet run --project Projects/ProjectARVRPro.IntegrationDemo -- --ui
```

## 离线解析样例

先不连接设备，只验证解析方式：

```powershell
dotnet run --project Projects/ProjectARVRPro.IntegrationDemo -- --parse-file Projects/ProjectARVRPro.IntegrationDemo/Samples/project-arvr-result.json
```

编译后 exe 用法：

```powershell
ProjectARVRPro.IntegrationDemo.exe --parse-file Samples\project-arvr-result.json
```

输出目录默认为 `output`，会生成：

- `ProjectARVRResult_*.json`：保存后的原始响应
- `ProjectARVRResult_*_items.csv`：扁平化后的测试项清单，包含 `Description` 字段说明列

## 光学参数说明

| 字段/参数 | 含义 | 常见单位 |
| --- | --- | --- |
| `HorizontalFieldOfViewAngle` | 水平视场角，画面水平方向可观察范围。 | degree |
| `VerticalFieldOfViewAngle` | 垂直视场角，画面垂直方向可观察范围。 | degree |
| `DiagonalFieldOfViewAngle` | 对角线视场角，画面对角方向可观察范围。 | degree |
| `LuminanceUniformity` | 亮度均匀性，通常按最小亮度/最大亮度*100% 计算，越高越均匀。 | % |
| `ColorUniformity` | 色度均匀性，通常取各测点最大 Delta u'v'，越小越均匀。 | 无 |
| `CenterLunimance` | 中心点亮度，字段名保留 ARVRPro 当前拼写。 | cd/m^2 |
| `CenterCorrelatedColorTemperature` | 中心相关色温 CCT。 | K |
| `CenterCIE1931ChromaticCoordinatesx/y` | 中心点 CIE 1931 色品坐标 x/y。 | 无 |
| `CenterCIE1976ChromaticCoordinatesu/v` | 中心点 CIE 1976 色品坐标 u'/v'。 | 无 |
| `FOFOContrast` | 白场/黑场对比关系。 | % |
| `ChessboardContrast` | 棋盘格亮暗区域对比度。 | 由配置决定 |
| `HorizontalTVDistortion` / `VerticalTVDistortion` | 水平/垂直 TV 几何畸变比例。 | % |
| `Optic_Distortion` | 光学畸变，表示镜头或系统引起的整体几何畸变。 | % |
| `DistortionTop/Bottom/Left/Right` | 九点法上/下/左/右局部畸变。 | % |
| `KeystoneHoriz` / `KeystoneVert` | 水平/垂直梯形畸变。 | % |
| `ImageCenter*` | 图像中心偏移、倾斜或旋转。 | degree |
| `OptCenter*` | 光学中心偏移、倾斜或旋转。 | degree |
| `MTF_*` | 调制传递函数，描述成像清晰度/解析力；H/V 表示方向，0F/0.3F/0.6F/0.8F 表示视场位置。 | % |

`ObjectiveTestItem` 的通用字段：`Value` 是数值型测试值，`TestValue` 是格式化显示值，`LowLimit` / `UpLimit` 是判定上下限，`Unit` 是单位，`TestResult` 是单项判定结果。

## 联机测试

标准外部触发流程：

```powershell
dotnet run --project Projects/ProjectARVRPro.IntegrationDemo -- --host 127.0.0.1 --port 6666 --sn SN001 --mode init
```

收到 `SwitchPG` 后，CLI 模式会提示是否发送 `SwitchPGCompleted`。如果现场想自动确认：

```powershell
dotnet run --project Projects/ProjectARVRPro.IntegrationDemo -- --host 127.0.0.1 --port 6666 --sn SN001 --mode init --auto-confirm-switchpg --auto-confirm-aoi
```

一键执行流程：

```powershell
dotnet run --project Projects/ProjectARVRPro.IntegrationDemo -- --host 127.0.0.1 --port 6666 --sn SN001 --mode runall
```

可按现场情况调整等待保护：

```powershell
dotnet run --project Projects/ProjectARVRPro.IntegrationDemo -- --host 127.0.0.1 --port 6666 --sn SN001 --timeout-seconds 300 --max-messages 200
```

## 发布给客户

客户只需要 Windows + .NET Framework 4.8 Runtime。建议发布为一个普通文件夹：

```powershell
dotnet publish Projects/ProjectARVRPro.IntegrationDemo/ProjectARVRPro.IntegrationDemo.csproj -f net48 -c Release -p:Platform=x64 -o artifacts/ProjectARVRPro.IntegrationDemo
```

把输出目录发给客户即可。发布目录里包含 exe 和 `Samples`。如果客户要把代码复制到自己的老软件里，优先复制：

- `Contracts` 整个文件夹
- `Program.cs` 里的 `JsonStreamMessageReader` 和 `ResultParser`

这些代码不依赖本仓库其他文件。WPF 窗口只是演示壳，客户自己的 WinForms 软件可以只复用通信和解析部分。

## 报文说明

请求是 UTF-8 JSON 字符串，不额外追加换行符：

```json
{
  "Version": "1.0",
  "MsgID": "req-001",
  "EventName": "ProjectARVRInit",
  "SerialNumber": "SN001",
  "Params": ""
}
```

最终结果是 `ProjectARVRResult`：

```json
{
  "Version": "1.0",
  "EventName": "ProjectARVRResult",
  "Code": 0,
  "Msg": "ARVR Test Completed",
  "SerialNumber": "SN001",
  "Data": {
    "W51TestResult": {
      "HorizontalFieldOfViewAngle": {
        "Name": "Horizontal_Field_Of_View_Angle",
        "Value": 95.2,
        "LowLimit": 90,
        "UpLimit": 100,
        "Unit": "degree",
        "TestResult": true
      }
    },
    "TotalResult": true,
    "TotalResultString": "PASS"
  }
}
```

Demo 同时提供两种解析方式：

- 强类型契约：见 `Contracts` 文件夹，适合客户在自己的 C# 项目里直接复制字段模型。
- 通用扁平化：按 `ObjectiveTestItem` 的形态识别测试项，对象里包含 `Value`，并且包含 `LowLimit` / `UpLimit` / `TestResult` 中至少一个字段。

## 对接建议

- 业务判定优先读 `Data.TotalResult` 或 `Data.TotalResultString`。
- 详细测试项读取 `ObjectiveTestItem.Value`，单位读 `Unit`，上下限读 `LowLimit` / `UpLimit`。
- TCP 是流式协议，客户端读取时要能处理半包和粘包；本 demo 的 `JsonStreamMessageReader` 用大括号配平方式拆 JSON 对象。
- 联机等待应设置超时和最大消息数，避免流程中断后客户端一直卡住。
- `SwitchPG` / `AoiSwitchPG` 确认建议按 `MsgID` 或 `EventName + SerialNumber + ARVRTestType` 去重，避免同一切图请求被重复确认。
- 现场联调时，建议保存完整原始 JSON。字段变化时，原始报文比截图和口头描述更容易定位问题。
