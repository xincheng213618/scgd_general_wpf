# YOLO 工业检测插件

这是一个 ColorVision 平台插件实践项目，用于把独立 WPF YOLO demo 改造成平台可扫描、可安装、可菜单启动的插件。

## 功能

- 打开图片检测
- 打开视频检测
- 摄像头实时检测
- 调节置信度阈值和 NMS 阈值
- 显示 FPS、单帧推理耗时、模型输入尺寸、输出形状和类别数量
- 保存当前帧
- 导出 JSON / CSV 检测结果
- 支持只检测 ROI 区域
- 支持工业 OK/NG 区域判定

## 模型文件

默认配置读取 `Models/yolov8n.onnx`。仓库中不提交 ONNX 模型文件，避免插件包过大。使用前请把自己的模型放到：

```text
Plugins/YoloObjectDetection/Models/yolov8n.onnx
```

如果模型文件名不同，请修改 `appsettings.json` 的 `modelPath`。

## 类别文件

`classes.txt` 每行一个类别名，必须与模型训练时的类别顺序一致。训练自己的工业缺陷模型后，需要同步替换这个文件。

## 平台内构建

默认使用平台源码项目引用，适合当前仓库内开发：

```powershell
dotnet build .\Plugins\YoloObjectDetection\YoloObjectDetection.csproj -c Debug -p:Platform=x64
```

构建后插件会复制到：

```text
ColorVision/bin/x64/Debug/net10.0-windows/Plugins/YoloObjectDetection
```

## NuGet 引用模式

当 `ColorVision.Common`、`ColorVision.UI` 等包发布到 NuGet 且版本一致后，可切换为：

```powershell
dotnet build .\Plugins\YoloObjectDetection\YoloObjectDetection.csproj -c Release -p:Platform=x64 -p:UseColorVisionNuGet=true -p:ColorVisionPackageVersion=1.5.5.1
```

外部插件作者建议使用 NuGet 引用模式，避免依赖平台源码目录。

## OK/NG 判定

`appsettings.json` 中的 `inspectionRegions` 用于配置工业检测区域。插件当前规则是：检测框中心点落在区域内，并且类别匹配 `requiredClass`，数量达到 `minCount` 则该区域 OK；所有启用区域 OK 后整帧为 OK，否则 NG。