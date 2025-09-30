# ColorVision.Core

## 🎯 功能定位

核心接口模块，提供C++算法库的.NET互操作接口。

## 作用范围

底层接口层，为上层应用提供高性能算法调用接口。

## 主要功能点

- **C++互操作** - .NET与C++算法库的接口封装
- **内存管理** - 跨语言内存安全管理
- **类型转换** - .NET和C++数据类型的转换
- **异常处理** - 统一的异常处理和错误传递
- **性能优化** - 高效的数据传递和调用机制

## 与主程序的依赖关系

**被引用方式**:
- cvColorVision 引用用于算法实现
- ColorVision.Engine 间接引用

**引用的外部依赖**:
- C++ 算法动态链接库
- P/Invoke 接口定义

## 使用方式

### 引用方式
```xml
<ProjectReference Include="..\ColorVision.Core\ColorVision.Core.csproj" />
```

### 在主程序中的启用
- 通过cvColorVision模块自动加载
- 算法节点执行时自动调用

## 开发调试

```bash
dotnet build UI/ColorVision.Core/ColorVision.Core.csproj
```

## 相关文档链接

- [算法组件文档](../../docs/algorithms/README.md)
- [性能优化指南](../../docs/performance/README.md)

## 维护者

ColorVision 核心团队