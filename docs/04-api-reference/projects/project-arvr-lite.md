# ProjectARVRLite

`Projects/ProjectARVRLite/` 是轻量 AR/VR 项目包，运行时加载 `ProjectARVRLite.dll`。它保留 Socket 切图、Flow 执行、结果汇总和 CSV 输出，但测试项顺序由配置决定，不是固定流程。

## 先查什么

| 现象 | 第一检查点 |
| --- | --- |
| 初始化后没有第一条 `SwitchPG` | `ProjectARVRLiteTestTypeConfig.json` 是否至少启用一个测试项 |
| 反复请求同一图案 | 是否启用了没有自动分支的测试类型 |
| Flow 找不到模板 | 模板名是否包含 `White51`、`White255_Ghost_Test`、`MTF_HV` 等关键字 |
| 预处理失败 | `PreProcessManager.ExecuteAsync(...)`、当前 Flow 名、服务节点 |
| Ghost 有数据但总判定不变 | 当前没有单独 `FlowGhostTestReslut` 汇总标志 |
| CSV 没生成 | `ViewResultManager.Config.IsSaveCsv`、保存路径、目录权限 |

## 自动链路

外部发送 `ProjectARVRInit` 后，`FlowInit.Handle()` 记录 `NetworkStream`；窗口不存在时自动创建 `ARVRWindow`。`InitTest(SN)` 重置步骤和结果，`TestTypeConfigManager.GetFirstEnabledTestType()` 返回第一条 `SwitchPG`。外部发回 `SwitchPGCompleted` 后，项目按启用配置找下一项，用关键字选择 Flow 模板，先跑预处理，再创建批次并启动 Flow。全部启用项完成后，`TestCompleted()` 汇总 `ObjectiveTestResult`、写 CSV，并返回 `ProjectARVRResult`。

旧代码还有一个空白事件名 `"  "` 的直接运行入口，只建议兼容旧现场，不要继续扩展。

## 测试项配置

配置文件：

```text
%AppData%\ColorVision\Config\ProjectARVRLiteTestTypeConfig.json
```

| 测试类型 | 模板关键字 | 主要结果 |
| --- | --- | --- |
| `W51` | `White51` | FOV |
| `White` | `White255_Ghost_Test` | W255 亮度/色度/均匀性/Ghost1 |
| `W25` | `White25` | W25 亮度/色度 |
| `Chessboard` | `Chessboard` | 棋盘格 POI 和对比度 |
| `MTFHV` | `MTF_HV` | 水平/垂直 MTF |
| `Distortion` | `Distortion` | 水平/垂直 TV 畸变 |
| `Ghost` | `White_Ghost_Test` | 独立 Ghost |
| `OpticCenter` | `OpticCenter` | 光轴和图像中心倾角/旋转 |

枚举里还能看到 `DotMatrix`、`WscreeenDefectDetection`、`BKscreeenDefectDetection`，但当前自动链路没有对应 `SwitchPGCompleted()` 分支。除非已经补实现，交付前必须保持禁用。

## 关键源码

| 文件 | 作用 |
| --- | --- |
| `ARVRWindow.xaml.cs` | 主窗口、测试状态机、预处理、Flow 执行、结果回传 |
| `TestTypeConfig.cs` | 测试类型启用/禁用和 AppData 配置 |
| `ProjectARVRReuslt.cs` | 单流程结果实体 |
| `ObjectiveTestResult.cs` | 整机结果 DTO 和 CSV |
| `ARVRRecipeConfig.cs` | W51、W255、W25、MTFHV、畸变、Ghost、光轴限值 |
| `ObjectiveTestResultFix.cs` | 结果修正系数 |
| `Services/SocketControl.cs` | `ProjectARVRInit`、`SwitchPGCompleted` |

## 输出和判定

`ObjectiveTestResult` 汇总 W51、W255、W25、Chessboard、MTFHV、Distortion、Ghost、OpticCenter 结果。整机 `TotalResult` 当前显式汇总 W51、White、W25、Chessboard、MTFHV、Distortion、OpticCenter 的 Flow 标志；Ghost 数据会解析，但没有单独 Flow 汇总标志。

CSV 输出由 `ViewResultManager.Config.IsSaveCsv` 控制，文件名格式：

```text
TestResults_{SN}_{yyyyMMdd_HHmmss}_.csv
```

`SaveByDate` 打开时会按日期建目录。结果图保存还受 `SaveImageReusltDelay`、`CodeUseSN`、`CodeDateFormat` 等配置影响。

## 版本和构建

`manifest.json` 当前为 `Id=ProjectARVRLite`、`version=1.0`、`dllpath=ProjectARVRLite.dll`、`requires=1.3.15.6`。

```powershell
dotnet build Projects/ProjectARVRLite/ProjectARVRLite.csproj -c Release -p:Platform=x64
Scripts\package_project.bat ProjectARVRLite --no-upload
```

## 交付验收

| 验收项 | 通过标准 |
| --- | --- |
| 项目装载 | 主程序发现项目包，菜单能打开 `ARVRWindow` |
| 自动开窗 | 窗口未开时发 `ProjectARVRInit`，能自动创建窗口并返回第一条 `SwitchPG` |
| 测试项配置 | 未实现的 `DotMatrix`、白/黑瑕疵检测保持禁用 |
| 切图链路 | 每个启用项只请求一次，能跑到最后一个启用项 |
| 预处理 | 预处理成功后才启动 Flow，失败时能返回失败结果 |
| 模板匹配 | 启用项对应模板名包含约定关键字 |
| 结果汇总 | W51/W255/W25/MTF/Distortion/Ghost/OpticCenter 字段能填充 |
| CSV/图像 | CSV、日期目录和图像保存符合现场配置 |
| 交付包 | `.cvxp` 内含 DLL、manifest、README、CHANGELOG 和默认配置说明 |
