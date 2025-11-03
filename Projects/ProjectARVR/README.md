# ProjectARVR - AR/VR 测试系统

## 🎯 功能定位

AR/VR显示设备光学性能测试系统 - 提供完整的AR/VR显示设备质量检测解决方案，支持FOV、MTF、畸变、鬼影等全方位光学性能测试。

## 作用范围

专为AR/VR显示设备设计的综合测试系统，覆盖显示质量、光学性能、缺陷检测等完整测试流程，适用于生产线和研发测试场景。

## 主要功能点

### 光学测试
- **FOV测试** - 视场角(Field of View)测量
- **亮度测试** - 亮度均匀性和颜色均匀性检测
- **对比度测试** - 序列对比度和棋盘格对比度测试
- **MTF测试** - 水平和垂直调制传递函数测量
- **畸变测试** - 图像畸变分析和量化
- **鬼影检测** - 鬼影现象识别和测量
- **光轴偏角** - 光轴偏心(OC)测试

### 测试流程
- **双眼测试** - 支持左眼/右眼独立测试流程
- **多图案切换** - 白屏、黑屏、9点图、棋盘格、MTF图卡、网格图
- **自动化测试** - 全自动测试流程执行
- **结果分析** - 客观测试结果分析和判定

### AA区域定位
- **自动定位** - 自动AA(Active Area)区域识别
- **关注点算法** - POI(Point of Interest)自动标注
- **中心亮度比例** - 基于中心区域的亮度计算

### 通信协议
- **流程触发** - 支持通过MQTT/Socket触发测试
- **状态反馈** - 实时测试状态和结果反馈
- **数据导出** - 左右眼测试结果导出

## 测试时序

完整的测试流程包含以下步骤：

| 序号 | 责任方 | 测试时序 | 测试画面 | 测试项目 | 核心算法 |
|------|--------|----------|----------|----------|----------|
| 1 | KTD | 产品上线扫描录入产品SN | - | - | - |
| 2 | KTD | 产品移动到左眼测试位置 | - | - | - |
| 3 | KTD | 上位机通知测试开始 | - | - | - |
| 4 | KTD | 产品点亮白屏 | 白 | FOV,亮度均匀性,颜色均匀性 | AA定位+FOV+均匀性 |
| 5 | ColorVision | NED测试白色屏 | 白 | 对比度 | AA定位+序列对比度 |
| 6 | KTD | 产品点亮黑屏 | 黑 | 亮度,序列对比度 | AA定位+序列对比度 |
| 7 | ColorVision | NED测试黑色屏 | 黑 | 对比度 | AA定位+序列对比度 |
| 8 | KTD | 产品点亮9点图 | 9点图 | 畸变/鬼影 | 畸变算法+鬼影算法 |
| 9 | ColorVision | NED测试9点图 | 9点图 | 畸变/鬼影 | 畸变算法+鬼影算法 |
| 10 | KTD | 产品切换4×4棋盘格 | 棋盘格 | 棋盘格对比度 | 棋盘格对比度算法 |
| 11 | ColorVision | NED测试棋盘格 | 棋盘格 | 对比度 | 棋盘格对比度算法 |
| 12-15 | KTD/ColorVision | 水平MTF测试 | MTF | 水平MTF | MTF算法 |
| 16-19 | KTD/ColorVision | 垂直MTF测试 | MTF | 垂直MTF | MTF算法 |
| 20-23 | KTD/ColorVision | 瑕疵检测 | 白圆图 | 瑕疵检测 | 瑕疵检测算法 |
| 24-27 | KTD/ColorVision | 光轴测试 | 网格图 | 光轴偏角 | 光轴偏心OC |
| 28-39 | - | 右眼测试(重复左眼步骤) | - | - | - |
| 40 | - | 左右眼结果导出 | - | - | - |

## 通信协议

### 流程执行
```json
{
  "Version": "1.0",
  "MsgID": "12345",
  "EventName": "ProjectARVR",
  "SerialNumber": "SN001",
  "Params": "Flow"
}
```

### 菜单操作
```json
{
  "Version": "1.0",
  "MsgID": "12345",
  "EventName": "Menu",
  "SerialNumber": "SN001",
  "Code": 0,
  "Msg": "RunTemplate",
  "Data": null
}
```

## 与主程序的依赖关系

**引用的程序集**:
- ColorVision.Engine - 核心引擎功能
- ColorVision.Engine.Templates - 模板管理
- ColorVision.UI - 基础UI组件
- FlowEngineLib - 流程引擎

**被引用方式**:
- 作为插件集成到主程序
- 支持大流程和直接流程两种模式

## 使用方式

### 测试模式
- **大流程模式** - 完整的自动化测试流程
- **直接流程** - 单独执行特定测试项目
- **手动模式** - 手动控制测试步骤

### 引用方式
```xml
<ProjectReference Include="..\..\Engine\ColorVision.Engine\ColorVision.Engine.csproj" />
```

## 开发调试

```bash
dotnet build Projects/ProjectARVR/ProjectARVR.csproj
```

## 目录说明

- `Views/` - 测试界面和窗口
- `Models/` - 数据模型和配置
- `Services/` - 测试服务和算法接口
- `Config/` - 配置文件目录
- `Algorithms/` - 算法实现模块

## 相关文档链接

- [ARVR测试规范](../../docs/testing/ARVR-testing.md)
- [光学测试算法](../../docs/algorithms/optical-testing.md)
- [流程引擎文档](../../docs/04-api-reference/engine-components/README.md)

## 维护者

ColorVision 项目团队
