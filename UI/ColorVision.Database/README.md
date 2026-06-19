# ColorVision.Database

> 版本: 1.5.5.1 | 目标框架: .NET 8.0 / .NET 10.0 Windows | UI框架: WPF

## 功能定位

数据库访问层，提供统一的数据库操作接口和可视化管理工具。支持 MySQL 连接，并可通过 provider 扩展浏览其他 SQLite 文件，提供数据库优先的表浏览器、查询工具、连接管理等功能。

## 主要功能

### 数据库浏览器 (DatabaseBrowserWindow)
- **数据库优先** — 通过连接先查询库，再查询库里的表，不依赖 C# 实体
- **可扩展数据源** — 默认注册 `MySQL`，其他 SQLite 文件可通过 provider 注册
- **左侧树** — 按数据源、库、表展开，表节点显示估算记录数
- **右侧表格** — `DataGrid` 直接绑定 `DataTable`，支持分页、搜索、排序、编辑、保存、删除
- **通用 CRUD** — 通过 `IDatabaseBrowserProvider` 执行 Insert / Update / Delete，修改和删除要求表有主键

### 通用查询 (GenericQueryWindow)
- **动态条件** — 按字段添加查询条件，支持 =、>、<、>=、<=、LIKE 操作符
- **SQL 预览** — 实时显示生成的 SQL 语句
- **结果统计** — 显示查询记录数和耗时
- **表操作** — 支持清空表、截断表

### 数据库连接管理
- **MySQL 连接配置** (`MySqlConnect`) — 可视化连接参数设置
- **连接状态监控** — 实时检测数据库连接状态
- **MySQL 设置向导** (`MysqlWizardStep`) — 向导式配置流程

### 数据访问
- **泛型 DAO** (`BaseTableDao<T>`) — 基于 SqlSugar 的扩展方法（CRUD、分页、批量）
- **实体接口** (`IEntity`) — 标准化实体定义（`int Id` 属性）
- **实体基类** (`EntityBase` / `ViewEntity`) — 带 `[SugarColumn]` 主键映射的基类
- **数据库浏览 Provider** (`IDatabaseBrowserProvider`) — 注册可浏览的数据源

## 技术架构

```
ColorVision.Database/
├── IEntity.cs                  # 实体接口 (int Id)
├── EntityBase.cs               # 实体基类 (POCO)
├── ViewEntity.cs               # 可绑定实体基类 (INotifyPropertyChanged)
├── BaseTableDao.cs             # 泛型 DAO + 扩展方法 (CRUD/分页/批量)
├── DatabaseType.cs             # 数据库类型枚举
├── IDatabaseBrowserProvider.cs # 数据库浏览器 Provider 接口
├── DatabaseBrowserModels.cs    # 库/表/列/分页模型
├── DatabaseBrowserWindow.xaml  # 数据库浏览器主窗口
├── DatabaseRowEditWindow.xaml  # 通用新增行弹窗
│
├── GenericQueryWindow.xaml     # 高级查询窗口
│
├── MySqlControl.cs             # MySQL 连接管理 (单例)
├── MySQLConfig.cs              # MySQL 配置 (IConfig)
├── MySqlConnect.xaml           # 连接配置窗口
├── MySqlSetting.cs             # 设置管理
├── MysqlWizardStep.cs          # 配置向导步骤
├── IMysqlCommand.cs            # SQL 命令接口
└── ExportMySqlInitTables.cs    # 初始化表导出
```

## 使用方式

### 注册数据库浏览 Provider
```csharp
DatabaseBrowserProviderRegistry.Register(new SqliteDatabaseBrowserProvider(
    "sqlite.demo",
    "SQLite Demo",
    () => DemoDbManager.DbPath,
    DemoDbManager.CreateDbClient));
```

### 使用数据库浏览器
```csharp
// 通过菜单打开（工具 → 数据库浏览器）
// 或手动打开
new DatabaseBrowserWindow().Show();
```

### 使用 DAO 扩展方法
```csharp
var dao = new BaseTableDao<UserEntity>();

// 查询
var all = dao.GetAll();
var user = dao.GetById(1);
var filtered = dao.GetAllByParam(new Dictionary<string, object> { { "Username", "admin" } });

// 分页
var (items, total) = dao.GetPaged(pageIndex: 1, pageSize: 20);

// 保存（Id <= 0 插入，否则更新）
dao.Save(user);

// 删除
dao.DeleteById(1);
```

### 使用高级查询
```csharp
var db = new SqlSugarClient(/* ... */);
var results = new List<UserEntity>();
var query = new GenericQuery<UserEntity>(db, results);
var window = new GenericQueryWindow(query);
window.Show();
```

## 依赖关系

- **引用**: ColorVision.UI, SqlSugarCore 5.1.4.214, log4net 3.3.0, Newtonsoft.Json 13.0.4
- **被引用**: ColorVision.SocketProtocol, ColorVision.Solution, ColorVision.UI.Desktop

## 构建

```bash
dotnet build UI/ColorVision.Database/ColorVision.Database.csproj -f net8.0-windows -p:Platform=x64
```
