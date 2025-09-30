# ColorVision.Solution

## 🎯 功能定位

解决方案和工程文件管理模块，提供项目文件的组织、管理和预览功能。

## 作用范围

工程管理层，为用户提供类似IDE的工程文件管理能力。

## 主要功能点

- **解决方案管理** - 创建、打开、保存解决方案文件
- **文件树视图** - 树形结构展示工程文件组织
- **文件预览** - 支持图像文件的快速预览
- **批处理操作** - 对工程中的文件进行批量操作
- **文件搜索** - 快速定位工程中的文件
- **工程配置** - 管理工程相关的配置信息

## 与主程序的依赖关系

**被引用方式**:
- ColorVision 主程序集成解决方案管理
- 通过菜单或快捷键打开解决方案窗口

**引用的程序集**:
- ColorVision.UI - 基础UI组件
- ColorVision.Common - 通用接口

## 使用方式

### 引用方式
```xml
<ProjectReference Include="..\ColorVision.Solution\ColorVision.Solution.csproj" />
```

### 基础使用
```csharp
// 创建新解决方案
var solution = new Solution("MySolution");

// 添加文件到解决方案
solution.AddFile("Images/test1.png");
solution.AddFile("Images/test2.png");

// 保存解决方案
solution.Save("path/to/solution.cvsln");

// 打开解决方案
var loadedSolution = Solution.Load("path/to/solution.cvsln");
```

## 开发调试

```bash
dotnet build UI/ColorVision.Solution/ColorVision.Solution.csproj
```

## 目录说明

- `Views/` - 解决方案管理界面
- `Models/` - 解决方案数据模型
- `Services/` - 文件操作服务

## 相关文档链接

- [用户界面指南](../../docs/user-interface-guide/)
- [入门指南](../../docs/getting-started/入门指南.md)

## 维护者

ColorVision UI团队
