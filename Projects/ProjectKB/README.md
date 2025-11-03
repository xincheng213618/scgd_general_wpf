# ProjectKB - 键盘测试系统

## 🎯 功能定位

键盘背光亮度测试系统 - 专为键盘产品的亮度、均匀性和光学性能测试提供的完整解决方案。

## 作用范围

专注于键盘产品的质量检测，包括按键亮度、光晕检测、亮度均匀性等光学参数的测试和分析。

## 主要功能点

### 亮度测试
- **按键亮度测量** - 精确测量单个按键的亮度值
- **光晕检测** - 检测按键周围的光晕现象
- **键帽整体测量** - 字符+光源的整体亮度测试
- **最小/最大/平均亮度** - 全键盘亮度统计分析

### 均匀性分析
- **亮度一致性** - 计算键盘整体亮度均匀性 (Uniformity=min/max)
- **局部对比度** - 检测暗键和亮键的局部对比度
- **颜色一致性** - 分析键盘的颜色均匀性
- **色差测试** - 测量键盘的色差值

### POI模板管理
- **关注点配置** - 在主程序中配置测试关注点
- **模板导入** - 将POI配置导入到KB测试流程
- **参数管理** - 管理测试相关的参数信息

### 测试流程
- **流程配置** - 配置完整的测试流程
- **相机取图** - 添加相机模块获取键盘图像
- **KB模板设置** - 配置KB测试模板和参数
- **灰度计算** - 计算图像灰度值
- **亮度校正** - 通过校正获取准确的亮度值
- **结果判定** - 根据SPEC标准判定测试结果

## 测试规格 (SPEC)

### 测试项目
- **最小亮度 (MinLv)** - 所有按键中的最小亮度值
- **最大亮度 (MaxLv)** - 所有按键中的最大亮度值
- **平均亮度 (AvgLv)** - 全键盘的平均亮度
- **亮度一致性 (Uniformity)** - 亮度的均匀程度
- **局部对比度** - 暗键和亮键的局部对比度
- **色差 (ColorDifference)** - 键盘的色差值
- **杂光 (StrayLight)** - 杂散光检测

### 判定标准
测试结果根据以下条件判定：
- 平均亮度大于最小亮度限制
- 平均亮度小于最大亮度限制
- 平均亮度大于预设值
- 亮度一致性大于预设值
- 所有条件满足时判定为**PASS**，否则为**FAIL**

## 数据输出

### CSV报告格式
测试数据自动写入CSV文件，包含以下字段：
- 基本信息：Id, Model, SerialNumber, POISet, DateTime
- 测试结果：AvgLv, MinLv, MaxLv, LvUniformity
- 详细数据：DarkestKey, BrightestKey, ColorDifference
- 失效信息：NbrFailedPts, LvFailures, LocalContrastFailures
- 对比度：DarkKeyLocalContrast, BrightKeyLocalContrast
- 其他参数：LocalDarkestKey, LocalBrightestKey, StrayLight
- 判定结果：Result
- 限制值：MinKeyLv, MaxKeyLv, MinAvgLv, MaxAvgLv, MinLvUniformity等

### 测试报告
- 自动生成详细测试报告
- 保存到用户指定的路径
- 包含图像和测试数据

## Modbus集成

### 触发模式
- **手动执行** - 通过界面手动启动测试
- **Modbus触发** - 根据Modbus信号自动触发测试

### 默认配置
- **IP地址**: 192.168.6.1
- **端口**: 502
- **寄存器地址**: D0 (0号地址)
- **自动连接**: UI启动时自动尝试连接
- **状态显示**: 连接状态在状态栏中显示

## MES系统集成

项目支持与MES（制造执行系统）集成，通过FunTestDll.dll提供的接口实现：
- 版本检查
- 工号验证
- 条码状态检查
- 测试数据上传
- 工单信息查询

**注意**: MES接口的详细说明请参考技术文档或联系技术支持团队。

## 与主程序的依赖关系

**引用的程序集**:
- ColorVision.Engine - 核心引擎功能
- ColorVision.Engine.Templates - 模板管理
- ColorVision.UI - 基础UI组件
- ColorVision.ImageEditor - 图像处理和标注

**被引用方式**:
- 作为插件集成到主程序
- 支持独立窗口模式运行

## 使用方式

### 测试流程步骤
1. **配置POI模板** - 在主程序中设置关注点
2. **配置KB模板** - 设置测试参数和判定标准
3. **添加相机模块** - 配置图像采集设备
4. **执行测试流程** - 运行完整的测试流程
5. **分析结果** - 查看测试结果和生成报告
6. **导出数据** - 保存CSV和测试报告

### 引用方式
```xml
<ProjectReference Include="..\..\Engine\ColorVision.Engine\ColorVision.Engine.csproj" />
<ProjectReference Include="..\..\UI\ColorVision.UI\ColorVision.UI.csproj" />
```

## 开发调试

```bash
dotnet build Projects/ProjectKB/ProjectKB.csproj
```

## 目录说明

- `Views/` - 测试界面和窗口
- `Models/` - 数据模型和配置
- `Services/` - 业务服务和算法接口
- `Config/` - 配置文件目录
- `Templates/` - KB测试模板

## 术语说明

- **Key (按键)** - 单个按键字符
- **Halo (光晕)** - 按键周围的光晕效果
- **键帽** - 字符+光源等整体
- **Uniformity (均匀性)** - 亮度一致性，计算公式: min/max

## 相关文档链接

- [测试流程配置](../../docs/04-api-reference/engine-components/README.md)
- [算法引擎文档](../../docs/04-api-reference/algorithms/README.md)
- [MES集成说明](../../docs/integration/mes-integration.md) *(如有)*

## 维护者

ColorVision 项目团队
