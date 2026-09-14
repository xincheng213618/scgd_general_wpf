---
knowledge_id: "algorithms.fov-local"
knowledge_type: "topic"
status: "current"
summary: "本地 FOV V2 的相机标定参数、角点复用与自动定位、视场角公式、ImageView 叠图和 FOV 2.0 结果兼容契约。"
aliases: ["本地FOV计算(V2)","FOV 计算 (V2)","FovDist","cameraDegrees","LocalFovNode","FovCalculator","FovImageViewRunner","HorizontalFieldOfViewAngle","DiagonalFieldOfViewAngle"]
code_paths: ["UI/ColorVision.Core/FovCalculation.cs","Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalFovNode.cs","Engine/ColorVision.Engine/PropertyEditor/CameraDegreesPropertiesEditor.cs","Engine/ColorVision.Engine/Templates/Jsons/FOV2","UI/ColorVision.ImageEditor/EditorTools/Algorithms/Calculate/AlgorithmResultOverlay.cs","UI/ColorVision.ImageEditor/Draw/FovOverlayRenderer.cs"]
test_paths: ["Test/ColorVision.UI.Tests/FovCalculationTests.cs","Test/ColorVision.UI.Tests/AlgorithmResultOverlayTests.cs"]
related: ["algorithms.find-light-area","algorithms.roi-routes","engine.results","flow.node-extension"]
---

# 本地 FOV V2

本地 FOV V2 在 ColorVision 进程内完成发光区四角定位和水平、垂直、对角视场角计算，不依赖 `CV_algorithm.dll` 或远端算法服务。原有 FOV 2.0 模板和远端链路继续保留；本地流程节点以相同结果类型、版本和 JSON 字段写入现有结果链，供历史结果及 ProjectARVRPro 继续读取。

## 参数归属

`FovDist` 和 `cameraDegrees` 都是与相机及镜头标定相关的 FOV 运行参数。Flow 和 ImageView 均直接展示并允许调整，默认值分别为 `9410` 和 `74.2`；不从物理相机配置隐式覆盖。

本地 FOV 默认测量稳定发光区四角对应的几何视场。`LuminanceBoundaryRatio` 仅保留旧配置读取兼容，不显示为 FOV 参数，也不参与定位或计算。画面内部亮度下降不能自动改写几何边界；若产品规范要求亮度或对比度限定的有效视场，应另行确认参考量、阈值、扫描方向和测量条件，不能将任意等亮度线的四边拟合视为标准测量。

`cameraDegrees` 输入框右侧提供统一的计算入口，Flow 和 ImageView 使用同一个属性编辑器。计算窗口可输入传感器有效宽度、有效高度和镜头有效焦距，并按水平、垂直或对角方向选择回填值：

\[
cameraDegrees = 2\arctan\left(\frac{SensorLength}{2FocalLength}\right)
\]

窗口只回填 `cameraDegrees`，不会修改 `FovDist`。所选传感器方向必须与 `FovDist` 代表的参考像素跨度一致；使用相机 ROI 或裁图时，应填写有效成像区域的传感器尺寸，而不是未经裁切的名义尺寸。

两项参数都必须是大于零的有限数值，且 `cameraDegrees` 小于 `180` 度。参数虽然可调，但同一台相机的生产流程应使用已确认的配对值，不应用调参吸收发光区定位误差。

旧模板中的 `DarkRatio`、`threshold`、`ExactCorner`、`AnglesFov`、横竖开关等字段不迁入本地节点。它们属于旧服务的定位或开关配置，不参与本地角度换算。读取旧模板和调用旧服务的兼容行为不变。

## Flow 使用

在流程中加入 **本地FOV计算(V2)** 并接入图像或发光区结果：

1. 上游是 `FindLightArea` 或 `LightArea` 且已有四条有效角点明细时，节点直接使用 LT、RT、RB、LB 计算，保留原始四角，不再读取像素重定位。
2. 上游只有图像时，节点对当前内存帧运行与本地发光区相同的 `RobustV2`，直接使用成功结果的四角；没有内存帧时再读取上游结果对应的图像文件。
3. 两种输入使用相同的四角角度公式，不经过 `FovLuminanceBoundary`。
4. 相机优先取上游图像记录的设备编码；旧发光区结果没有 `SourceMasterId` 时，会按其图像路径反查最近的图像记录。这个相机关联只用于追溯和 ImageView 落库，不改写节点中的两项 FOV 参数；普通图片没有相机服务也可以计算。
5. 成功后写入现有 FOV 2.0 结果并将本次主结果传给后续节点。

发光区明细只有有效数据库角点，没有可用内存帧或图像文件时仍可计算。需要自行定位但没有图像、`RobustV2` 拒绝结果、角点不是四个、顺序无效或 FOV 参数无效时拒绝执行。`MinimumConfidence` 只约束自动运行的 `RobustV2`，不会用图像中的亮度再次筛选有效上游角点。

## ImageView 使用和绘制

打开图像，在整图或矩形区域右键选择 **算法调用 → FOV 计算 (V2)...**。矩形入口将选择框作为 `RobustV2` 搜索区域；整图入口搜索完整图像。独立入口自行定位并直接计算，ImageView 绘制本次几何边界：

- LT、RT、RB、LB 四角标签及蓝色四边形；
- 橙色水平中轴、绿色垂直中轴；
- 两条紫色虚线对角线；
- H、V、D 三项角度标签。

FOV 标签圆的直径与当前结果文字字号一致；修改固定结果字号时圆和文字同步缩放，启用图层自动刷新时二者也随视图缩放保持一致。该规则只用于 FOV 结果标签，不改变普通测量圆的几何半径。

新的 FOV 运行会清除上一组 FOV 叠图，打开新图也会清除旧结果。若当前图像能关联到已有测量批次，ImageView 同时写入相同的 FOV 2.0 数据库结果；普通磁盘图片仍可计算和绘制，但结果窗口会明确提示没有关联批次，因此未写数据库。

ImageView 的计算与绘制不以数据库成功为前提：数据库保存或结果通知失败时仍保留本次有效叠图，并在结果窗口显示失败原因。流程节点继续使用严格结果链；持久化失败会使节点失败，不能把只有内存数值的运行交给后续节点。

## 计算口径

`RobustV2` 寻找稳定几何边缘；FOV 不再改变其四角。优先复用上游角点是为了让定位、绘图和角度换算保持一致，而非宣称几何四角等于所有产品规范中的有效视场。上游采用不同定位方法时，其差异也会保留到 FOV 中。

独立保留的 `FovLuminanceBoundary` 仅是实验等亮度线拟合：内部暗角低于中心亮度比例时，外沿可能根本没有该比例交点，算法会命中内部渐变并产生倾斜边。增加交点数量或放宽置信度不能修复测量对象的改变。它不是本地 FOV 的默认路径，也不代表 IEC/IDMS 符合性。

像素线段长度为 `L`，相机标定距离为 `FovDist`，相机参考角为 `cameraDegrees` 时，使用针孔模型换算式：

```text
angle(L) = 2 * atan((L / FovDist) * tan(cameraDegrees / 2 * PI / 180)) * 180 / PI
```

弧度到角度使用精确的 `180 / PI`，不保留旧实现的 `57.3` 近似常量。因此相同角点和标定参数下，本地结果会比旧 DLL 数值低约 `0.00737%`；这属于换算精度修正，不应用参数补偿。四角顺序固定为 LT、RT、RB、LB：

| 结果 | 线段与汇总方式 |
| --- | --- |
| `H_Fov` | 上边 LT-RT 与下边 LB-RB 的角度均值 |
| `V_FOV` | 左边 LT-LB 与右边 RT-RB 的角度均值 |
| `leftDownToRightUp` | 对角线 LB-RT |
| `leftUpToRightDown` | 对角线 LT-RB |
| `D_Fov` | 两条对角线角度均值 |
| `clolorVisionH_Fov` | 左右边中点之间的水平中轴 |
| `clolorVisionV_Fov` | 上下边中点之间的垂直中轴 |

后两个字段保留历史拼写和存储位置，但本地用对边中点，旧 DLL 另有中心方向边界定位，两者不能宣称数值或测量方法等价。ProjectARVRPro 的 W51 正是读取这两个字段及 `D_Fov`，经过 Recipe 修正后比较上下限；角度合格阈值不定义像素边界的亮度阈值。替换前应在固定曝光、测试图案和相机标定下用现场图集比较这三项及单独对角线，确认原 Recipe 的适用性；不能调整 `FovDist` 或放宽出厂限值吸收定位差异。

## 结果兼容

成功结果仍创建类型 `FOV`、版本 `2.0` 的主记录和一条 `DetailCommon` 明细。明细 JSON 的 `ResultFileName` 指向本地结果文件，文件的 `result` 对象保留以下字段及大小写：

```text
D_Fov, H_Fov, V_FOV, clolorVisionH_Fov, clolorVisionV_Fov,
leftDownToRightUp, leftUpToRightDown, message
```

七项测量值保存在结果文件中；角点、相机编码、`FovDist`、`cameraDegrees`、`BoundaryMode`（`UpstreamCorners` 或 `RobustV2`）及定位诊断保存在主记录参数中，便于追溯。下游业务判定、导出和协议字段仍由原有结果处理器及客户项目负责。流程保存要求已有批次和可用数据库连接，ImageView 无批次时不创建虚构批次。

内存帧若直接来自普通图片且未经过校正或翻转，主记录 `ImgFile` 会保存该原始图片路径，结果列表的“文件”列可正常关联原图。若当前主缓冲区已校正或翻转，只使用与当前缓冲区严格对应的 `CvRawFilePath` / `CvCieFilePath`；没有这类已保存文件时保持为空，避免历史叠图错误加载变换前的源图。

## 验证边界

托管专项测试验证上游角点不需像素且不受旧比例参数影响，并用已部署服务的样例四角验证精确角度换算至 `1e-6`；同时检查旧 JSON 字段、参数可见性、源图路径和 ImageView 叠图结构。原生集成测试以暗角渐变矩形验证几何边缘，并保留实验算法的失败特征对照。运行：

```powershell
dotnet test .\Test\ColorVision.UI.Tests\ColorVision.UI.Tests.csproj -p:Platform=x64 --filter "FullyQualifiedName~FovCalculationTests"
```

现场离线测试 `FovFieldImageTests` 通过环境变量 `COLORVISION_FOV_FIELD_IMAGE` 显式指定该 9568×6380 暗角 TIFF，`COLORVISION_FOV_FIELD_REPORT` 可指定 JSON 和叠图输出位置；未提供文件时跳过。`COLORVISION_RUN_LUMINOUS_NATIVE_V2_TESTS=1` 启用合成图原生集成回归。测试不连接生产数据库、不运行相机，不证明所有现场图像均能成功定位。正式同位替换验收仍应覆盖旋转、暗角、漏光、低对比、饱和和裁边样本，比较四角、七项 FOV、重复性、耗时、出厂判定、结果落库与历史读取。
