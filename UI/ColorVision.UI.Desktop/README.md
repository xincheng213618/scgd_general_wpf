# ColorVision.Solution

## 🎯 功能定位

解决方案和工程文件管理模块，提供项目文件的组织、管理和预览功能。类似于 Visual Studio 的解决方案资源管理器，为 ColorVision 系统提供强大的工程管理能力。

## 作用范围

工程管理层，为用户提供类似IDE的工程文件管理能力，支持文件的浏览、编辑、搜索和批量操作。

## 主要功能点

### 核心功能
- **解决方案管理** - 创建、打开、保存解决方案文件（.cvsln格式）
- **文件树视图** - 树形结构展示工程文件组织，支持文件夹和文件的可视化管理
- **多编辑器支持** - 集成文本、图像、Hex、Web等多种编辑器
- **文件搜索** - 快速定位工程中的文件和内容
- **工程配置** - 管理工程相关的配置信息

### 扩展功能
- **权限控制（RBAC）** - 基于角色的访问控制，支持用户、角色、权限管理
- **插件系统** - 支持插件动态加载和管理
- **最近文件** - 记录和管理最近打开的解决方案
- **文件监控** - 实时监控文件系统变化并自动更新视图
- **终端集成** - 内置终端管理窗口

## 架构设计

### 核心组件

```
ColorVision.Solution/
├── V/                          # 可视化对象模型
│   ├── VObject.cs              # 基础可视化对象
│   ├── VFile.cs                # 文件对象
│   ├── VFolder.cs              # 文件夹对象
│   ├── SolutionExplorer.cs     # 解决方案资源管理器
│   └── VObjectFactory.cs       # 对象工厂
├── Editor/                     # 编辑器系统
│   ├── EditorManager.cs        # 编辑器管理器
│   ├── IEditor.cs              # 编辑器接口
│   ├── TextEditor.cs           # 文本编辑器
│   ├── ImageEditor.cs          # 图像编辑器
│   └── AvalonEditor/           # AvalonEdit 集成
├── FileMeta/                   # 文件元数据
│   ├── IFileMeta.cs            # 文件元数据接口
│   └── FileMetaRegistry.cs     # 文件元数据注册表
├── FolderMeta/                 # 文件夹元数据
│   ├── IFolderMeta.cs          # 文件夹元数据接口
│   └── FolderMetaRegistry.cs   # 文件夹元数据注册表
├── Rbac/                       # 权限控制系统
│   ├── RbacManager.cs          # RBAC管理器
│   ├── Entity/                 # 实体模型
│   └── Services/               # 服务层
├── Searches/                   # 搜索功能
│   └── SolutionView.xaml       # 解决方案视图
└── RecentFile/                 # 最近文件管理
    └── RecentFileList.cs       # 最近文件列表
```

## 与主程序的依赖关系

**被引用方式**:
- ColorVision 主程序通过 `SolutionManager` 集成解决方案管理
- 通过菜单或快捷键打开解决方案窗口
- 支持命令行参数启动时自动打开解决方案

**引用的程序集**:
- `ColorVision.UI` - 基础UI组件和扩展
- `ColorVision.Database` - 数据库支持
- `ColorVision.ImageEditor` - 图像编辑功能
- `AvalonEdit` - 代码编辑器控件
- `WPFHexaEditor` - Hex编辑器控件
- `Microsoft.Web.WebView2` - Web视图支持

## 使用方式

### 引用方式
```xml
<ProjectReference Include="..\ColorVision.Solution\ColorVision.Solution.csproj" />
```

### 基础使用

#### 创建和打开解决方案
```csharp
// 获取解决方案管理器实例
var solutionManager = SolutionManager.GetInstance();

// 创建新解决方案
string solutionPath = @"C:\Projects\MySolution";
solutionManager.CreateSolution(solutionPath);

// 打开现有解决方案
string existingSolution = @"C:\Projects\MySolution\MySolution.cvsln";
bool success = solutionManager.OpenSolution(existingSolution);

// 访问当前解决方案
var currentSolution = solutionManager.CurrentSolutionExplorer;
```

#### 使用文件树控件
```csharp
// 在XAML中使用
<solution:TreeViewControl x:Name="TreeView" />

// 在代码中访问
var treeView = new TreeViewControl();
// 树视图自动绑定到 SolutionManager.SolutionExplorers
```

#### 注册自定义编辑器
```csharp
// 使用特性注册编辑器
[EditorForExtension(".txt", ".log", ".md")]
public class MyCustomEditor : EditorBase
{
    public override void Open(string filePath)
    {
        // 编辑器打开逻辑
    }
}

// 获取文件的编辑器
var editor = EditorManager.Instance.GetDefaultEditor(".txt");
```

#### 处理解决方案事件
```csharp
var solutionManager = SolutionManager.GetInstance();

// 监听解决方案创建事件
solutionManager.SolutionCreated += (sender, args) =>
{
    Console.WriteLine("解决方案已创建");
};

// 监听解决方案加载事件
solutionManager.SolutionLoaded += (sender, args) =>
{
    Console.WriteLine("解决方案已加载");
};
```

## 开发调试

### 构建项目
```bash
# 构建解决方案模块
dotnet build UI/ColorVision.Solution/ColorVision.Solution.csproj

# 构建整个解决方案
dotnet build scgd_general_wpf.sln
```

### 运行测试
```bash
# 如果有测试项目
dotnet test
```

## 目录说明

- `V/` - 可视化对象模型（VObject, VFile, VFolder等）
- `Editor/` - 编辑器系统及各类编辑器实现
- `FileMeta/` - 文件元数据定义和注册
- `FolderMeta/` - 文件夹元数据定义和注册
- `Rbac/` - 基于角色的访问控制系统
- `Searches/` - 搜索和解决方案视图
- `RecentFile/` - 最近文件历史管理
- `Plugins/` - 插件管理系统
- `Properties/` - 资源文件

## 配置文件

解决方案配置文件（.cvsln）采用 JSON 格式：
```json
{
  "FilePath": "",
  "VirtualPath": "",
  "IsSetting": false,
  "IsSetting1": false,
  "Paths": []
}
```

## 相关文档链接

- [详细架构文档](../../docs/04-api-reference/ui-components/ColorVision.Solution.md)
- [用户界面指南](../../docs/01-user-guide/)
- [入门指南](../../docs/00-getting-started/README.md)

## 技术特性

- ✅ MVVM 架构模式
- ✅ 依赖注入和服务定位
- ✅ 文件系统监控（FileSystemWatcher）
- ✅ 工厂模式和注册表模式
- ✅ 命令模式（RelayCommand）
- ✅ 事件驱动架构
- ✅ 可扩展的插件系统

## 维护者

ColorVision UI团队

## 版本历史

当前版本：1.3.8.5

## License

参见项目根目录的 LICENSE 文件
