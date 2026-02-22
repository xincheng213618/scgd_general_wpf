# ColorVision.Database

> 版本: 1.5.1.1 | 目标框架: .NET 8.0 / .NET 10.0 Windows | UI框架: WPF

## 🎯 功能定位

数据库访问层，提供统一的数据库操作接口和辅助控件。支持 MySQL 和 SQLite 双数据库，提供可视化的数据库连接管理、查询工具和本地服务管理功能。

## 作用范围

UI数据层，为界面组件提供数据库连接管理和数据访问功能。

## 主要功能点

### 数据库连接管理
- **MySQL 连接配置** - 可视化的连接参数设置（服务器、端口、数据库、用户名、密码）
- **连接状态监控** - 实时连接状态检测和显示
- **连接池管理** - 高效的数据库连接复用
- **安全认证** - 支持用户名密码和高级安全选项，密码加密存储
- **连接测试** - 一键测试数据库连接

### 可视化管理工具
- **数据库连接窗口** (`MySqlConnect`) - 图形化配置数据库连接参数
- **管理工具窗口** (`MySqlToolWindow`) - 集成的数据库管理界面
- **通用查询窗口** (`GenericQueryWindow`) - 执行 SQL 查询并显示结果
- **本地服务管理** (`MySqlLocalServicesManager`) - MySQL 本地服务的启停控制

### 数据访问抽象
- **泛型 DAO** (`BaseTableDao<T>`) - 基于泛型的数据访问对象，支持 CRUD 操作
- **实体接口** (`IEntity`) - 标准化实体定义
- **SQL 命令抽象** (`IMysqlCommand`) - 命令模式的数据库操作
- **配置向导** (`MysqlWizardStep`) - 向导式数据库配置流程

## 技术架构

```
┌─────────────────────────────────────────────────────────────┐
│                    ColorVision.Database                       │
│                                                              │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐      │
│  │  UI 层      │    │  数据访问层  │    │  配置管理层  │      │
│  │             │    │             │    │             │      │
│  │ MySqlConnect│───▶│ MySqlControl│◀───│ MySQLConfig │      │
│  │ GenericQuery│    │ BaseTableDao│    │ MysqlSetting│      │
│  │ MySqlToolWin│    │ IEntity     │    │ MysqlWizard │      │
│  └─────────────┘    └─────────────┘    └─────────────┘      │
│         │                   │                   │            │
│         ▼                   ▼                   ▼            │
│  ┌────────────────────────────────────────────────────────┐ │
│  │                      MySQL / SQLite                      │ │
│  └────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

## 与主程序的依赖关系

**被引用方式**:
- ColorVision.UI 引用用于数据显示控件
- ColorVision.Engine 引用用于数据持久化
- ColorVision.Solution 引用用于 RBAC 权限数据存储

**引用的程序集**:
- MySQL.Data - MySQL 连接器
- System.Data.SQLite - SQLite 连接器
- ColorVision.UI - 基础UI组件

## 使用方式

### 引用方式
```xml
<ProjectReference Include="..\ColorVision.Database\ColorVision.Database.csproj" />
```

### 基础使用示例

#### 1. 配置数据库连接
```csharp
// 配置数据库连接
var config = MySQLConfig.Instance;
config.Server = "localhost";
config.Port = 3306;
config.Database = "colorvision_db";
config.Username = "admin";
config.Password = "password";
config.Save();

// 测试连接
if (MySqlControl.TestConnection())
{
    Console.WriteLine("数据库连接成功");
}
```

#### 2. 显示连接配置窗口
```csharp
// 显示数据库连接配置窗口
var connectWindow = new MySqlConnect();
if (connectWindow.ShowDialog() == true)
{
    // 连接配置完成并保存
}
```

#### 3. 使用 DAO 进行数据操作
```csharp
// 定义实体
public class User : IEntity
{
    public int Id { get; set; }
    public string Username { get; set; }
    public string Email { get; set; }
    public DateTime CreatedAt { get; set; }
}

// 创建 DAO
public class UserDao : BaseTableDao<User>
{
    public UserDao() : base("users") { }
    
    public User GetByUsername(string username)
    {
        var sql = $"SELECT * FROM {{TableName}} WHERE Username = @username";
        var parameters = new Dictionary<string, object> { { "@username", username } };
        return ExecuteQuery(sql, parameters).FirstOrDefault();
    }
}

// 使用 DAO
var userDao = new UserDao();

// 插入
var newUser = new User
{
    Username = "john_doe",
    Email = "john@example.com",
    CreatedAt = DateTime.Now
};
userDao.Insert(newUser);

// 查询
var users = userDao.GetAll();
var user = userDao.GetById(1);

// 更新
user.Email = "new@example.com";
userDao.Update(user);

// 删除
userDao.Delete(1);
```

#### 4. 执行自定义 SQL 查询
```csharp
// 执行查询
var dataTable = MySqlControl.ExecuteQuery("SELECT * FROM users WHERE IsActive = 1");

// 带参数的查询
var parameters = new Dictionary<string, object> 
{ 
    { "@status", "active" },
    { "@date", DateTime.Now.AddDays(-30) }
};
var results = MySqlControl.ExecuteQuery(
    "SELECT * FROM users WHERE Status = @status AND CreatedAt > @date", 
    parameters);
```

#### 5. 显示通用查询窗口
```csharp
// 显示 SQL 查询窗口
var queryWindow = new GenericQueryWindow();
queryWindow.Show();
```

## 主要组件

### MySQLConfig
数据库连接配置类，单例模式管理连接参数，支持属性变更通知和配置持久化。

```csharp
public class MySQLConfig : IConfig
{
    public static MySQLConfig Instance { get; } = new MySQLConfig();
    
    public string Server { get; set; }
    public int Port { get; set; }
    public string Database { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public string ConnectionString { get; }
    
    public void Save();
    public void Load();
}
```

### MySqlControl
数据库连接控制类，提供连接管理和 SQL 执行功能。

```csharp
public static class MySqlControl
{
    public static MySqlConnection GetConnection();
    public static bool TestConnection();
    public static bool TestConnection(string connectionString);
    public static DataTable ExecuteQuery(string sql, Dictionary<string, object> parameters = null);
    public static int ExecuteNonQuery(string sql, Dictionary<string, object> parameters = null);
}
```

### BaseTableDao<T>
泛型数据访问对象基类，提供标准 CRUD 操作。

```csharp
public abstract class BaseTableDao<T> where T : class, IEntity, new()
{
    public virtual List<T> GetAll();
    public virtual T GetById(int id);
    public virtual int Insert(T entity);
    public virtual int Update(T entity);
    public virtual int Delete(int id);
    protected List<T> ExecuteQuery(string sql, Dictionary<string, object> parameters = null);
}
```

### MySqlConnect
数据库连接配置窗口，提供可视化的连接参数设置界面。

### GenericQueryWindow
通用 SQL 查询窗口，支持执行查询、显示结果、导出数据。

### MySqlLocalServicesManager
MySQL 本地服务管理器，提供服务的启动、停止、状态检测功能。

## 目录说明

- `MySQLConfig.cs` - MySQL 配置类
- `MySqlControl.cs` - MySQL 连接控制
- `MySqlConnect.xaml/cs` - 连接配置窗口
- `MySqlToolWindow.xaml/cs` - 管理工具窗口
- `GenericQueryWindow.xaml/cs` - 通用查询窗口
- `BaseTableDao.cs` - 泛型 DAO 基类
- `IEntity.cs` - 实体接口
- `IMysqlCommand.cs` - SQL 命令接口
- `MySqlSetting.cs` - MySQL 设置
- `MySqlLocalServicesManager.cs` - 本地服务管理
- `MysqlWizardStep.cs` - 配置向导步骤
- `ExportMySqlInitTables.cs` - 初始化表导出

## 开发调试

```bash
# 构建项目
dotnet build UI/ColorVision.Database/ColorVision.Database.csproj

# 运行测试
dotnet test
```

## 最佳实践

### 1. 连接管理
- 使用连接池避免频繁创建连接
- 及时释放数据库资源
- 实现连接超时和重试机制

### 2. 安全考虑
- 密码加密存储（使用 EncryptionHelper）
- SQL 注入防护（使用参数化查询）
- 避免在代码中硬编码连接字符串

### 3. 性能优化
- 使用索引优化查询
- 分页处理大数据集
- 缓存常用查询结果

### 4. 错误处理
- 完善的异常处理机制
- 用户友好的错误信息
- 详细的日志记录

## 相关文档链接

- [数据存储文档](../../docs/05-resources/data-storage.md)
- [配置管理指南](../../docs/00-getting-started/README.md)
- [RBAC 权限系统](../../UI/ColorVision.Solution/Rbac/README.md)

## 更新日志

### v1.5.1.1 (2025-02)
- 支持 .NET 10.0
- 优化连接池管理

### v1.4.1.1 (2025-02)
- 数据库自动更新功能
- 优化日志系统

### v1.3.18.1 (2025-02)
- 增加数据库日志优化
- 支持多语言

## 维护者

ColorVision UI团队
