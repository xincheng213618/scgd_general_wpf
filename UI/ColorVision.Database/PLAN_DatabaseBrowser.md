# 数据库优先浏览器设计

这版思路比实体优先更适合做 Navicat 类工具：CRUD 的基本能力不依赖 C# 实体，只需要数据库连接、表结构、主键和列元数据。实体模型仍然有价值，但应该是业务层增强，不应该限制通用表浏览器。

## 核心抽象

入口接口：`IDatabaseBrowserProvider`

```csharp
public interface IDatabaseBrowserProvider
{
    string ProviderId { get; }
    string ProviderName { get; }
    DatabaseType DatabaseType { get; }
    bool CanWrite { get; }

    IReadOnlyList<DatabaseCatalogInfo> GetDatabases();
    IReadOnlyList<DatabaseTableInfo> GetTables(string databaseName);
    IReadOnlyList<DatabaseColumnInfo> GetColumns(DatabaseTableInfo table);
    DatabaseTablePage QueryPage(DatabaseTableInfo table, int pageIndex, int pageSize, string? keyword, string? sortColumn, ListSortDirection sortDirection);
    int InsertRow(DatabaseTableInfo table, IReadOnlyDictionary<string, object?> values);
    int UpdateRow(DatabaseTableInfo table, IReadOnlyDictionary<string, object?> keys, IReadOnlyDictionary<string, object?> values);
    int DeleteRow(DatabaseTableInfo table, IReadOnlyDictionary<string, object?> keys);
}
```

数据流：

```text
DatabaseBrowserProviderRegistry
  -> IDatabaseBrowserProvider
       -> GetDatabases()
       -> GetTables(databaseName)
       -> GetColumns(table)
       -> QueryPage(table, page, size, keyword, sort)
       -> InsertRow / UpdateRow / DeleteRow
```

## 已实现 provider

| Provider | 文件 | 说明 |
|----------|------|------|
| `mysql.default` | `MySqlDatabaseBrowserProvider.cs` | 使用 `MySqlControl.Config` 连接 MySQL，查询 `INFORMATION_SCHEMA` 获取库、表、列 |
| `sqlite.log` | `SqliteDatabaseBrowserProvider.cs` | 使用 `SqliteLogManager.SqliteDbPath`，查询 `sqlite_master` 与 `PRAGMA table_info` |

默认 provider 通过 `DatabaseBrowserProviderRegistry.GetProviders()` 懒加载注册。

## 当前能力

- MySQL：可列出服务器下业务数据库、数据库中的表、表字段。
- SQLite：一个文件视为一个 catalog，默认名称为 `main`。
- UI：`DatabaseBrowserWindow` 已提供数据源树、表格分页、搜索、排序、新增、保存、删除、撤销。
- 查询：统一返回 `DataTable`，右侧 `DataGrid` 直接绑定 `DefaultView`。
- 分页：MySQL / SQLite 均使用 `LIMIT @limit OFFSET @offset`。
- 搜索：对文本列做 `LIKE`；SQLite 会用 `CAST(column AS TEXT)` 处理弱类型列。
- 排序：使用列元数据校验列名后拼接，避免直接信任 UI 输入。
- 写入：支持 Insert / Update / Delete，Update/Delete 要求 UI 传入主键字典。

## 接入其他数据库

项目里的其他 SQLite 管理器可以注册成 provider：

```csharp
DatabaseBrowserProviderRegistry.Register(new SqliteDatabaseBrowserProvider(
    "sqlite.scheduler",
    "SQLite 调度任务",
    () => SchedulerDbManager.DbPath,
    SchedulerDbManager.CreateDbClient));
```

如果是其他数据库类型，实现 `IDatabaseBrowserProvider` 即可。

## 和实体方案的关系

实体优先方案适合业务对象编辑：可以复用 `DisplayName`、`Description`、PropertyGrid 和类型转换。

数据库优先方案适合交付维护工具：不需要提前写实体，也能查看第三方项目 MySQL 表、临时表、SQLite 文件和插件表。

实体浏览器旧链路已移除。后续如果需要恢复业务对象编辑，应作为数据库浏览器上的增强模式单独实现，而不是作为主入口。

## 已知边界

- 当前 provider 是同步接口；UI 层应使用 `Task.Run` 包一层，避免阻塞 WPF 线程。
- Update/Delete 依赖主键字典；没有主键的表默认不应该允许修改或删除。
- 通用 CRUD 暂不处理外键关系、事务批处理、二进制大对象预览等高级能力。
- SQLite 多文件场景通过注册多个 `SqliteDatabaseBrowserProvider` 解决。

## 验证命令

```powershell
dotnet build UI/ColorVision.Database/ColorVision.Database.csproj -f net8.0-windows -p:Platform=x64
```

当前验证结果：构建通过。
