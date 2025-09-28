# ColorVision.FileIO

## 功能定位

专用文件IO处理模块，负责ColorVision专有格式文件的读写操作。

## 作用范围

文件处理引擎，为整个系统提供统一的文件访问接口。

## 主要功能点

- **CVRaw 文件处理** - ColorVision 原始图像格式的读写
- **CVCIE 文件处理** - ColorVision CIE 色彩数据格式的读写
- **文件格式验证** - 确保文件完整性和格式正确性
- **批量文件操作** - 支持大批量文件的高效处理
- **文件压缩解压** - 数据文件的压缩存储和解压读取
- **异步IO操作** - 支持异步文件读写，避免界面阻塞
- **文件监控** - 支持文件系统监控和自动处理

## 与主程序的依赖关系

**被引用方式**:
- ColorVision.Engine 引用用于流程中的文件操作
- ColorVision.UI 引用用于界面文件显示

**引用的程序集**:
- System.IO - 基础文件操作
- 图像处理库 - 用于图像格式转换

## 使用方式

### 引用方式
```xml
<ProjectReference Include="..\ColorVision.FileIO\ColorVision.FileIO.csproj" />
```

### 在主程序中的启用
- 通过文件服务自动注册
- 流程节点中自动调用文件读写接口

## 开发调试

```bash
dotnet build Engine/ColorVision.FileIO/ColorVision.FileIO.csproj
```

## 目录说明

- 包含专有文件格式的读写实现
- 文件操作工具类和辅助方法

## 相关文档链接

- [文件IO组件详细文档](../../docs/engine-components/ColorVision.FileIO.md)
- [文件格式说明](../../docs/data-storage/README.md)

## 维护者

ColorVision 数据团队
