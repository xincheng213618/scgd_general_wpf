# ColorVision.Database

> 版本: 1.5.5.1 | 目标框架: .NET 8.0 / .NET 10.0 Windows | UI框架: WPF

## 功能定位

数据库访问层，提供统一的数据库操作接口和可视化管理工具。支持 MySQL 和 SQLite 双数据库，提供通用实体浏览器（类似 Navicat）、查询工具、连接管理等功能。

## 主要功能

### 实体浏览器 (EntityBrowserWindow)
- **自动发现** — 启动时扫描所有已加载程序集，自动发现实现 `IEntity` 的实体类
- **双数据库支持** — 通过 `[DatabaseSource]` 属性标记实体属于 MySQL 或 SQLite
- **左侧 DataGrid** — 显示表名、数据库类型、记录数，支持搜索过滤
- **右侧 CRUD** — 嵌入 `EntityCrudControl`，支持增删改查、分页、搜索
- **单元格编辑** — DataGrid 支持直接编辑单元格，自动保存

### 通用 CRUD 控件 (EntityCrudControl)
- **零配置使用** — 只需传入 `Type` 和 `SqlSugarClient`，无需 `BaseTableDao`
- **自动生成列** — 根据实体属性自动配置 DataGrid 列（类型、格式、只读）
- **分页支持** — 内置分页控件（10/20/50/100 条/页）
- **搜索过滤** — 全字段模糊搜索
- **自动保存** — 编辑单元格后自动保存到数据库

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
- **数据库标记** (`DatabaseSourceAttribute`) — 标记实体属于哪个数据库

## 技术架构

```
ColorVision.Database/
├── IEntity.cs                  # 实体接口 (int Id)
├── EntityBase.cs               # 实体基类 (POCO)
├── ViewEntity.cs               # 可绑定实体基类 (INotifyPropertyChanged)
├── BaseTableDao.cs             # 泛型 DAO + 扩展方法 (CRUD/分页/批量)
├── DatabaseSourceAttribute.cs  # [DatabaseSource(MySql/Sqlite)] 标记
│
├── EntityBrowserWindow.xaml    # 实体浏览器主窗口 (左列表 + 右 CRUD)
├── EntityCrudControl.xaml      # 通用 CRUD 控件 (DataGrid + 分页)
├── EntityCrudWindow.xaml       # 独立 CRUD 窗口 (薄包装)
├── EntityEditWindow.xaml       # 属性编辑弹窗
├── GenericQueryWindow.xaml     # 高级查询窗口
│
├── MySqlControl.cs             # MySQL 连接管理 (单例)
├── MySQLConfig.cs              # MySQL 配置 (IConfig)
├── MySqlConnect.xaml           # 连接配置窗口
├── MySqlSetting.cs             # 设置管理
├── MysqlWizardStep.cs          # 配置向导步骤
├── IMysqlCommand.cs            # SQL 命令接口
├── ExportMySqlInitTables.cs    # 初始化表导出
│
└── SqliteLog/                  # SQLite 日志子系统
    ├── LogEntry.cs             # 日志实体 [DatabaseSource(Sqlite)]
    ├── SqliteLogManager.cs     # SQLite 连接管理 + 日志写入
    ├── SqliteLogInitializer.cs # 初始化器
    └── SqliteLogWindow.xaml    # 日志查看窗口
```

## 使用方式

### 定义实体
```csharp
// MySQL 实体（默认，无需标记）
public class UserEntity : EntityBase
{
    [SugarColumn(IsNullable = true)]
    public string Username { get; set; }
    public string Email { get; set; }
}

// SQLite 实体（需要标记）
[DatabaseSource(DatabaseType.Sqlite)]
public class LogEntry : IEntity
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }
    public string Message { get; set; }
    public DateTime Date { get; set; }
}
```

### 使用实体浏览器
```csharp
// 通过菜单打开（已注册在 工具 → 实体浏览器）
// 或手动打开
new EntityBrowserWindow().Show();
```

### 使用 CRUD 控件
```csharp
// 嵌入到任何窗口/面板
var db = new SqlSugarClient(new ConnectionConfig
{
    ConnectionString = MySqlControl.GetConnectionString(),
    DbType = SqlSugar.DbType.MySql,
    IsAutoCloseConnection = true
});
var control = new EntityCrudControl(typeof(UserEntity), db);
container.Children.Add(control);
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
dotnet build UI/ColorVision.Database/ColorVision.Database.csproj
```
