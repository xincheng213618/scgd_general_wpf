# ProjectARVRPro 客户对接 Demo

这个目录是给外部系统、客户 MES、PLC 上位机或自动化中控使用的最小对接示例。它是 .NET Framework 4.8 + WPF 窗口项目，不依赖 ColorVision、ARVRPro 内部项目、算法 DLL、数据库、流程配置或 NuGet 包，只保留可以公开给客户的通信和结果契约。

Demo 产品版本为 `1.0.0`，独立于 ColorVision 主程序和 ProjectARVRPro 插件版本；报文里的 `Version: "1.0"` 是 Socket 协议版本，也不是程序集版本。客户交付记录应从当次联调源码的 `Projects/ProjectARVRPro/ProjectARVRPro.csproj` 读取 `VersionPrefix`，单独注明已验证的插件版本，不要从 Demo 或协议版本推断兼容性。

它演示五件事：

1. 通过 TCP 连接 ARVRPRO 默认端口 `6666`。
2. 发送 `ProjectARVRInit` / `SwitchPGCompleted` / `RunAll` / `AOITestSwitchImageComplete`。
3. 同步发送 `SwitchGroup` / `GetProcessEnable` / `SetProcessEnable` 管理命令并读取对应响应。
4. 用 WPF 窗口查看 `ProjectARVRResult`、测试项表格和 `W51TestResult`。
5. 收到最终 `ProjectARVRResult` 后自动保存原始 JSON 并导出扁平 CSV。

## 公开代码边界

可以直接给客户看的对接模型在：

- [Contracts/ObjectiveTestResult.cs](Contracts/ObjectiveTestResult.cs)
- [Contracts/ObjectiveTestItem.cs](Contracts/ObjectiveTestItem.cs)
- [Contracts/Process/W51/W51TestResult.cs](Contracts/Process/W51/W51TestResult.cs)
- [Contracts/Process](Contracts/Process)
- [Contracts/Socket](Contracts/Socket)
- [Contracts/MVVM/ViewModelBase.cs](Contracts/MVVM/ViewModelBase.cs)

其中包含请求/响应壳、`SwitchPG`、当前标准 `ObjectiveTestResult`，以及按 ARVRPro 目录拆分的各类 `*TestResult`：

```csharp
public class W51TestResult : ViewModelBase
{
    public ObjectiveTestItem HorizontalFieldOfViewAngle { get; set; }
    public ObjectiveTestItem VerticalFieldOfViewAngle { get; set; }
    public ObjectiveTestItem DiagonalFieldOfViewAngle { get; set; }
}
```

这份契约代码只描述 JSON 字段，并内置了一个可独立复制的 `ViewModelBase`，不依赖 ARVRPro 的流程、算法、数据库、UI 或任何内部项目。

当前标准 `Data` 使用以下三类结果：

| 类型 | 顶层字段 | 说明 |
| --- | --- | --- |
| 键化结果 | `FieldOfViewTestResults`、`LuminanceChromaticityTestResults`、`LuminanceChromaticityYWTestResults`、`ChessboardTestResults`、`DynamicMTFHV058TestResults`、`MTFH07TestResults`、`MTFV07TestResults` | 第一层 Key 是流程配置的输出名称，例如 `White`、`YW`、`Chessboard`、`MTFH07`、`MTFV07` 或客户自定义名称；客户端不能把 Key 集合写死。YW 结果内分别保存 12X7、8X7 两组 POI，以及各组平均亮度、亮度均匀性和色度均匀性。 |
| 动态结果 | `DynamicTestResults`、`DynamicPoixyuvDatas`、`DynamicScreenDefectResults` | 分别承载动态 `ObjectiveTestItem`、POI 光色数据和屏幕缺陷汇总/缺陷框。 |
| 固定与兼容结果 | `W51TestResult`、`W255TestResult`、`BlackTestResult`、`ChessboardTestResult`、`MTFHVTestResult`、`MTFHV048TestResults`、`MTFHV058TestResults`、`DistortionTestResult`、`OpticCenterTestResult` | `FieldOfViewTestResults["White"]` 和 `LuminanceChromaticityTestResults["White"]` 会同时保留 W51/W255 兼容字段。 |

`ChessboardTestResult` 当前包含 `ChessboardContrast` 和 `AverageBlackLuminance`。`DistortionTestResult.OpticDistortion` 在 JSON 中实际字段名是 `Optic_Distortion`，契约里保留了 `Optic_Distortion` 字段，并提供 `OpticDistortion` 便捷属性。

### 标准结果与 Legacy 结果

标准结果和 Legacy 结果是两个独立的 `Data` 形态，不是同一对象里同时出现的新旧字段：

- 未启用 `UseLegacyARVROutput` 时，`Data` 是本页描述的现代嵌套 `ObjectiveTestResult`。
- 启用 `UseLegacyARVROutput` 时，ProjectARVRPro 会先转换为扁平的 `LegacyARVRObjectiveTestResult`，再把这个独立对象放进 `Data`。
- 本目录的样例 JSON 使用标准结果形态。对接 Legacy 现场时，应以该现场实际报文和旧版字段清单为准，不能把本样例的嵌套路径套到 Legacy `Data` 上。

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
- 查看键化结果、动态 POI 和屏幕缺陷数据；POI 会展开为光色测试项，屏幕缺陷汇总与每个缺陷框的标量字段也会进入表格和 CSV。
- 设置输出目录、接收超时秒数和最大消息数。
- 连接 ARVRPRO TCP Server，执行 `ProjectARVRInit` 或 `RunAll`。
- 点击“仅连接”建立连接但不启动测试，再选择 `SwitchGroup`、`GetProcessEnable` 或 `SetProcessEnable`，填写 Params 后发送同步命令；响应显示在通信日志中。
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

随项目提供的样例覆盖九个现代键化/动态顶层字段：`FieldOfViewTestResults`、`LuminanceChromaticityTestResults`、`LuminanceChromaticityYWTestResults`、`ChessboardTestResults`、`DynamicMTFHV058TestResults`、`MTFH07TestResults`、`MTFV07TestResults`、`DynamicPoixyuvDatas`、`DynamicScreenDefectResults`，并覆盖 `ChessboardTestResult.AverageBlackLuminance`。离线解析后，键化 `ObjectiveTestItem`、YW 两组 POI 光色项、动态 POI、屏幕缺陷汇总和每个缺陷框的标量字段都会进入扁平 CSV，并保留可追溯到原始 JSON 的 `Path`。

## 光学参数说明

| 字段/参数 | 含义 | 常见单位 |
| --- | --- | --- |
| `HorizontalFieldOfViewAngle` | 水平视场角，画面水平方向可观察范围。 | degree |
| `VerticalFieldOfViewAngle` | 垂直视场角，画面垂直方向可观察范围。 | degree |
| `DiagonalFieldOfViewAngle` | 对角线视场角，画面对角方向可观察范围。 | degree |
| `LuminanceUniformity` | 亮度均匀性，通常按最小亮度/最大亮度*100% 计算，越高越均匀。 | % |
| `ColorUniformity` | 色度均匀性，通常取各测点最大 Delta u'v'，越小越均匀。 | 无 |
| `CenterLuminance` | 键化亮色度结果中的中心点亮度。 | cd/m^2 |
| `CenterLunimance` | W255 兼容结果中的历史拼写。 | cd/m^2 |
| `CenterCorrelatedColorTemperature` | 中心相关色温 CCT。 | K |
| `CenterCIE1931ChromaticCoordinatesx/y` | 中心点 CIE 1931 色品坐标 x/y。 | 无 |
| `CenterCIE1976ChromaticCoordinatesu/v` | 中心点 CIE 1976 色品坐标 u'/v'。 | 无 |
| `FOFOContrast` | 白场/黑场对比关系。 | % |
| `ChessboardContrast` | 棋盘格亮暗区域对比度。 | 由配置决定 |
| `AverageBlackLuminance` | 棋盘格本地修正后的暗区平均亮度；数据库结果模式下可能为空。 | cd/m^2 |
| `HorizontalTVDistortion` / `VerticalTVDistortion` | 水平/垂直 TV 几何畸变比例。 | % |
| `Optic_Distortion` | 光学畸变，表示镜头或系统引起的整体几何畸变。 | % |
| `DistortionTop/Bottom/Left/Right` | 九点法上/下/左/右局部畸变。 | % |
| `KeystoneHoriz` / `KeystoneVert` | 水平/垂直梯形畸变。 | % |
| `ImageCenter*` | 图像中心偏移、倾斜或旋转。 | degree |
| `OptCenter*` | 光学中心偏移、倾斜或旋转。 | degree |
| `MTF_*` | 调制传递函数，描述成像清晰度/解析力；H/V 表示方向，0F/0.3F/0.6F/0.7F/0.8F 表示视场位置。 | % |
| `DynamicPoixyuvDatas` | 按输出名称分组的 POI 光色数据，包含 XYZ、xy、uv、CCT 和波长。 | 按子字段 |
| `DynamicScreenDefectResults` | 按输出名称分组的屏幕缺陷汇总和缺陷框参数。 | 像素/算法值 |

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

联机 CLI 会继续等待最终 `ProjectARVRResult`；`RunAll` 的 `Code=0` 仅表示请求已接收，不是流程完成。收到任意负 `Code`、最终 `TotalResult=false`、等待超时、连接提前断开或达到消息上限时，CLI 都以非零退出码结束，便于 MES/脚本可靠判定失败。

### 同步管理命令

三个管理命令仍使用同一个 `6666` TCP JSON 连接。请求外壳不变，差异只在 `EventName` 和字符串类型的 `Params`：

| EventName | Params | 成功响应 Data |
| --- | --- | --- |
| `SwitchGroup` | 非空流程组名称，例如 `Model_A_Group` | `{GroupName, MetaCount}` |
| `GetProcessEnable` | 空字符串 | `{ActiveGroupName, Count, Items}`；每项含 `Index`、`Name`、`FlowTemplate`、`ProcessTypeName`、`IsEnabled` |
| `SetProcessEnable` | 推荐使用 `{"Items":[{"Index":0,"IsEnabled":true}]}` JSON 字符串 | `{ActiveGroupName, Applied, NotFound}`；全部应用时 `Code=0`，部分索引不存在时 `Code=1` |

`SetProcessEnable.Params` 在 TCP 外层报文中仍是字符串，而不是嵌套对象；序列化后的报文会把内部双引号转义。服务端也接受直接数组 `[{"Index":0,"IsEnabled":true}]` 或单个 `{ "Index":0,"IsEnabled":true }`，但建议统一使用 `Items` 外壳。索引应先从当前组的 `GetProcessEnable` 响应读取；Legacy 模式可能改变索引偏移，不要自行按列表位置猜测。

CLI 用法：

```powershell
dotnet run --project Projects/ProjectARVRPro.IntegrationDemo -- --host 127.0.0.1 --port 6666 --switch-group Model_A_Group
dotnet run --project Projects/ProjectARVRPro.IntegrationDemo -- --host 127.0.0.1 --port 6666 --get-process-enable
dotnet run --project Projects/ProjectARVRPro.IntegrationDemo -- --host 127.0.0.1 --port 6666 --set-process-enable '{"Items":[{"Index":0,"IsEnabled":true}]}'
```

每次 CLI 调用只发送一个同步命令；它只把 `EventName` 和 `MsgID` 都与请求一致的报文视为对应响应，同名但 `MsgID` 不同的报文会跳过并继续等待。匹配后会打印 `Code`、`Msg` 和完整 JSON；`Code < 0` 时以非零退出码结束，其他响应正常结束。同步命令不能和 `--mode init|runall` 同时使用，等待仍受 `--timeout-seconds` 和 `--max-messages` 保护。

可按现场情况调整等待保护：

```powershell
dotnet run --project Projects/ProjectARVRPro.IntegrationDemo -- --host 127.0.0.1 --port 6666 --sn SN001 --timeout-seconds 300 --max-messages 200
```

## 发布给客户

客户只需要 Windows + .NET Framework 4.8 Runtime。建议发布为一个普通文件夹：

```powershell
dotnet publish Projects/ProjectARVRPro.IntegrationDemo/ProjectARVRPro.IntegrationDemo.csproj -f net48 -c Release -p:Platform=x64 -o artifacts/ProjectARVRPro.IntegrationDemo
```

把输出目录发给客户即可。发布目录里包含产品版本为 `1.0.0` 的 exe 和 `Samples`。如果客户要把代码复制到自己的老软件里，优先复制：

- `Contracts` 整个文件夹
- `Program.cs` 中完整的通信/解析辅助类型：`DemoOptions`、`ArvrClient`、`JsonStreamMessageReader`、`PoixyuvDataJavaScriptConverter`、`ResultParser`、`ParsedProjectArvrResult`、`ResultItem`、`OpticalParameterDescriptions`。不要只摘取 `ResultParser`；它依赖 POI converter 及后面的解析结果、行模型和字段说明类型。

这些代码不依赖本仓库其他文件。WPF 窗口只是演示壳，客户自己的 WinForms 软件可以只复用通信和解析部分。

### 发布到 ColorVision 下载服务

仓库提供了独立的 Demo 发布入口。它会发布 `net48/x64`、运行样例解析冒烟检查、生成版本化 ZIP 和 `latest.json`，并在上传后重新下载核对文件大小与 SHA-256：

```powershell
# 只构建和校验，不上传
.\Scripts\publish_project_arvrpro_integration_demo.bat --validate-only

# 上传 ZIP，校验成功后再更新 latest.json
.\Scripts\publish_project_arvrpro_integration_demo.bat
```

如需保留本地制品，可增加 `--output-dir artifacts\ProjectARVRPro.IntegrationDemo`。远端目录固定为 `Tool/ProjectARVRPro.IntegrationDemo/`；ProjectARVRPro 的“外部对接”页读取其中的 `latest.json`，再通过现有 HTTP 下载服务获取 ZIP。HTTP 传输不提供链路加密，因此客户端会在下载完成后强制校验清单中的文件大小和 SHA-256；校验失败的文件不会作为有效交付包保留。

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
    "FieldOfViewTestResults": {
      "White": {
        "HorizontalFieldOfViewAngle": {
          "Name": "Horizontal_Field_Of_View_Angle",
          "Value": 95.2,
          "LowLimit": 90,
          "UpLimit": 100,
          "Unit": "degree",
          "TestResult": true
        }
      }
    },
    "ChessboardTestResult": {
      "AverageBlackLuminance": {
        "Name": "AverageBlackLuminance",
        "Value": 5.7,
        "UpLimit": 8.0,
        "Unit": "cd/m2",
        "TestResult": true
      }
    },
    "TotalResult": true,
    "TotalResultString": "PASS"
  }
}
```

Demo 同时提供两种解析方式：

- 强类型契约：见 `Contracts` 文件夹，适合客户在自己的 C# 项目里直接复制当前标准结果字段模型。
- 通用扁平化：递归遍历自定义 Key，并按 `ObjectiveTestItem` 的形态识别测试项；对象里需包含 `Value`，并且包含 `LowLimit` / `UpLimit` / `TestResult` 中至少一个字段。
- 结构化展开：把 `PoixyuvDatas` / `DynamicPoixyuvDatas` 转成 Lv、Cx、Cy、u'、v'、CCT、Wave 等行，并把 `DynamicScreenDefectResults` 的汇总字段与 `Defects[i]` 标量字段转成带原始 `Path` 的行。

结构化展开只覆盖约定的 POI 和屏幕缺陷字段，不会把任意 JSON 标量都当成测试项。Legacy `Data` 是另一套扁平契约，应按现场模式单独解析。

## 对接建议

- 业务判定优先读 `Data.TotalResult` 或 `Data.TotalResultString`。
- 详细测试项读取 `ObjectiveTestItem.Value`，单位读 `Unit`，上下限读 `LowLimit` / `UpLimit`。
- 键化字典的 Key 来自流程配置；按返回的 Key 枚举，不要只处理示例中的 `White`。
- 对接前确认现场输出的是标准结果还是 Legacy 结果，并固定对应解析路径。
- 修改流程启用状态时先调用 `GetProcessEnable`，再把响应中的 `Index` 原样用于 `SetProcessEnable`。
- TCP 是流式协议，客户端读取时要能处理半包和粘包；本 demo 的 `JsonStreamMessageReader` 用大括号配平方式拆 JSON 对象。
- 联机等待应设置超时和最大消息数，避免流程中断后客户端一直卡住。
- `SwitchPG` / `AoiSwitchPG` 确认建议按 `MsgID` 或 `EventName + SerialNumber + ARVRTestType` 去重，避免同一切图请求被重复确认。
- 现场联调时，建议保存完整原始 JSON。字段变化时，原始报文比截图和口头描述更容易定位问题。
