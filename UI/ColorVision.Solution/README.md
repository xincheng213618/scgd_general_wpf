# ColorVision.Solution

> 版本: 1.5.1.1 | 目标框架: .NET 8.0 / .NET 10.0 Windows | UI框架: WPF

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

### 新增功能 (v1.5+)
- **多图像查看器** (`MultiImageViewer`) - 支持文件夹内多张图片的预览和缩略图缓存
- **缩略图缓存管理** (`ThumbnailCacheManager`) - 高效的图像缩略图生成和缓存
- **Markdown 查看器** (`MarkdownViewWindow`) - 支持 Markdown 文件的预览
- **工作区管理** (`WorkspaceManager`) - 多工作区切换管理
- **可编辑文本块** (`EditableTextBlock`) - 支持点击编辑的文件名显示

## 技术架构

### 核心组件

```
ColorVision.Solution/
├── V/                          # 可视化对象模型
│   ├── VObject.cs              # 基础可视化对象
│   ├── VFile.cs                # 文件对象
│   ├── VFolder.cs              # 文件夹对象
│   ├── SolutionExplorer.cs     # 解决方案资源管理器
│   ├── SolutionEnvironments.cs # 解决方案环境变量
│   └── VObjectFactory.cs       # 对象工厂
├── Editor/                     # 编辑器系统
│   ├── EditorManager.cs        # 编辑器管理器
│   ├── IEditor.cs              # 编辑器接口
│   ├── TextEditor.cs           # 文本编辑器
│   ├── ImageEditor.cs          # 图像编辑器
│   ├── HexEditor.cs            # Hex编辑器
│   ├── WebEditor.cs            # Web编辑器
│   ├── ProjectEditor.cs        # 项目编辑器
│   ├── SystemEditor.cs         # 系统编辑器
│   ├── EditorSelectionWindow   # 编辑器选择窗口
│   ├── FolderEditorSelectionWindow # 文件夹编辑器选择
│   └── AvalonEditor/           # AvalonEdit 集成
│       ├── AvalonEditWindow    # 代码编辑窗口
│       ├── AvalonEditControll  # 代码编辑控件
│       └── TextJsonPropertiesEditor # JSON属性编辑
├── FileMeta/                   # 文件元数据
│   ├── IFileMeta.cs            # 文件元数据接口
│   ├── FileMetaRegistry.cs     # 文件元数据注册表
│   ├── CommonFile.cs           # 通用文件
│   ├── FileImage.cs            # 图像文件
│   ├── FileProcessorImage.cs   # 图像处理器
│   └── FileProcessorText.cs    # 文本处理器
├── FolderMeta/                 # 文件夹元数据
│   ├── IFolderMeta.cs          # 文件夹元数据接口
│   ├── FolderMetaRegistry.cs   # 文件夹元数据注册表
│   ├── BaseFolder.cs           # 基础文件夹
│   └── ProjectFolders.cs       # 项目文件夹
├── Rbac/                       # 权限控制系统
│   ├── RbacManager.cs          # RBAC管理器
│   ├── RbacManagerConfig.cs    # RBAC配置
│   ├── LoginWindow             # 登录窗口
│   ├── RegisterWindow          # 注册窗口
│   ├── UserManagerWindow       # 用户管理窗口
│   ├── CreateUserWindow        # 创建用户窗口
│   ├── EditUserRolesWindow     # 编辑用户角色窗口
│   ├── PermissionManagerWindow # 权限管理窗口
│   ├── Entity/                 # 实体模型
│   │   ├── UserEntity.cs       # 用户实体
│   │   ├── RoleEntity.cs       # 角色实体
│   │   ├── PermissionEntity.cs # 权限实体
│   │   ├── UserRoleEntity.cs   # 用户角色关联
│   │   ├── UserDetailEntity.cs # 用户详情
│   │   ├── TenantEntity.cs     # 租户实体
│   │   ├── UserTenantEntity.cs # 用户租户关联
│   │   ├── SessionEntity.cs    # 会话实体
│   │   └── AuditLogEntity.cs   # 审计日志实体
│   ├── Services/               # 服务层
│   │   ├── AuthService         # 认证服务
│   │   ├── UserService         # 用户服务
│   │   ├── RoleService         # 角色服务
│   │   ├── PermissionService   # 权限服务
│   │   ├── SessionService      # 会话服务
│   │   ├── TenantService       # 租户服务
│   │   ├── AuditLogService     # 审计日志服务
│   │   └── PermissionChecker   # 权限检查器
│   └── Security/               # 安全相关
│       └── PasswordHashing.cs  # 密码哈希
├── MultiImageViewer/           # 多图像查看器 (v1.5+)
│   ├── MultiImageViewer        # 多图像查看控件
│   ├── MultiImageViewerConfig  # 查看器配置
│   ├── ImageFileInfo           # 图像文件信息
│   ├── ThumbnailCacheManager   # 缩略图缓存管理
│   └── ThumbnailCacheEntry     # 缓存条目
├── RecentFile/                 # 最近文件管理
│   ├── RecentFileList.cs       # 最近文件列表
│   ├── IRecentFile.cs          # 最近文件接口
│   ├── MenuRecentFile.cs       # 菜单最近文件
│   └── RegistryPersister.cs    # 注册表持久化
├── Workspace/                  # 工作区管理
│   ├── WorkspaceManager.cs     # 工作区管理器
│   ├── WorkspaceMainView       # 工作区主视图
│   └── SoloutionEditorControl  # 解决方案编辑控件
├── TreeViewControl             # 树视图控件
├── SolutionManager             # 解决方案管理器
├── NewCreatWindow              # 新建解决方案窗口
├── OpenSolutionWindow          # 打开解决方案窗口
└── MarkdownViewWindow          # Markdown查看窗口
```

## 与主程序的依赖关系

**被引用方式**:
- ColorVision 主程序通过 `SolutionManager` 集成解决方案管理
- 通过菜单或快捷键打开解决方案窗口
- 支持命令行参数启动时自动打开解决方案

**引用的程序集**:
- `ColorVision.UI` - 基础UI组件和扩展
- `ColorVision.UI.Desktop` - 桌面应用程序服务
- `ColorVision.Database` - 数据库支持
- `ColorVision.ImageEditor` - 图像编辑功能
- `ColorVision.Themes` - 主题支持
- `AvalonEdit` - 代码编辑器控件
- `WPFHexaEditor` - Hex编辑器控件
- `Microsoft.Web.WebView2` - Web视图支持
- `Markdig` - Markdown解析

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

#### 使用多图像查看器 (v1.5+)
```csharp
// 创建多图像查看器
var viewer = new MultiImageViewer();
viewer.LoadFolder(@"C:\Images");
viewer.Show();

// 或使用配置
var config = new MultiImageViewerConfig
{
    ThumbnailSize = 128,
    CacheEnabled = true,
    ShowFileName = true
};
viewer.ApplyConfig(config);
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

#### RBAC 权限使用
```csharp
// 登录
var authService = new AuthService();
var result = await authService.LoginAsync("username", "password");

// 检查权限
if (RbacManager.Instance.HasPermission("FILE_DELETE"))
{
    // 允许删除文件
}

// 记录审计日志
var auditService = new AuditLogService();
auditService.LogAction("FILE_DELETE", $"删除文件: {filePath}");
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
- `MultiImageViewer/` - 多图像查看器
- `RecentFile/` - 最近文件历史管理
- `Workspace/` - 工作区管理
- `Properties/` - 资源文件和多语言支持

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

## 多语言支持

支持以下语言：
- 简体中文 (zh-Hans)
- 繁体中文 (zh-Hant)
- 英语 (en)
- 法语 (fr)
- 日语 (ja)
- 韩语 (ko)
- 俄语 (ru)

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
- ✅ 多语言支持（7种语言）
- ✅ 缩略图缓存管理
- ✅ 完整的 RBAC 权限系统

## 更新日志

### v1.5.1.1 (2025-02)
- ✅ 新增多图像查看器 (`MultiImageViewer`)
- ✅ 新增缩略图缓存管理 (`ThumbnailCacheManager`)
- ✅ 新增 Markdown 查看器 (`MarkdownViewWindow`)
- ✅ 新增工作区管理 (`WorkspaceManager`)
- ✅ 新增可编辑文本块 (`EditableTextBlock`)
- ✅ 支持 .NET 10.0
- ✅ 优化文件系统监控性能
- ✅ 改进 RBAC 权限系统

### v1.4.1.1 (2025-02)
- 新增图像文件夹预览
- 优化解决方案加载速度
- 改进编辑器选择逻辑

### v1.3.18.1 (2025-02)
- 增加 RBAC 权限系统
- 增加多语言支持
- 增加 AvalonEdit 代码编辑器

## 维护者

ColorVision UI团队

## License

参见项目根目录的 LICENSE 文件
