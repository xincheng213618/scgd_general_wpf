# 实体浏览器 (EntityBrowser) 改进计划

## 背景

目标：实现一个类似 Navicat 的通用数据库管理界面，不需要传入 `BaseTableDao<T>`，通过菜单入口自动发现所有 `IEntity` 实现类，选择后直接进入增删改查。

## 当前已完成

### 新增文件
| 文件 | 说明 |
|------|------|
| `EntityCrudControl.xaml` + `.cs` | 通用 CRUD UserControl，接受 `(Type, SqlSugarClient)` |
| `EntityBrowserWindow.xaml` + `.cs` | 主窗口，左侧面板 + 右侧嵌入 EntityCrudControl |
| `MenuEntityBrowser.cs` | 工具菜单入口 |
| `DatabaseSourceAttribute.cs` | `[DatabaseSource(DatabaseType.Sqlite)]` 标记属性 |

### 修改文件
| 文件 | 变化 |
|------|------|
| `EntityCrudWindow.xaml` + `.cs` | 简化为薄包装，内部使用 EntityCrudControl |
| `SqliteLog/LogEntry.cs` | 添加 `[DatabaseSource(DatabaseType.Sqlite)]` |

---

## 存在的问题

### 问题 1：左侧 DataGrid 不够像 Navicat
- 当前只有 3 列（表名、数据库、记录数），信息太少
- 应该显示：表名、类名、数据库类型、记录数、描述、命名空间等
- 需要支持排序、筛选

### 问题 2：右侧 CRUD DataGrid 编辑体验差
- `CellEditEnding` 自动保存可能不生效（Binding 更新时机问题）
- 应该用 `RowEditEnding` 或手动 commit 方式
- 编辑后需要刷新 DataGrid 以反映保存结果
- 枚举列、DateTime 列的编辑需要特殊处理（ComboBox、DatePicker）

### 问题 3：SQLite 实体识别不完整
- 当前只给 `LogEntry` 加了 `[DatabaseSource]` 标记
- 其他可能使用 SQLite 的实体没有标记（需要排查所有 IEntity 实现）
- 兜底逻辑（命名空间推断）不可靠

### 问题 4：SQLite 表的 CRUD 不工作
- `EntityCrudControl` 的 `SaveItem<T>` 方法使用 `_db.Insertable<T>()`，但 SQLite 的 SqlSugarClient 和 MySQL 的混用可能出问题
- 需要确认 `SqliteLogManager.CreateDbClient()` 返回的 SqlSugarClient 能正确操作表
- SQLite 表可能不存在（未初始化），需要先建表

### 问题 5：记录数查询报错
- 非 MySQL 表用 MySQL 连接查 count 会报错
- 需要确保每个实体用正确的数据库连接查 count
- 异步加载记录数时的异常处理不够完善

---

## 改进计划

### Step 1：完善左侧 DataGrid

文件：`EntityBrowserWindow.xaml` + `.cs`

```
目标效果（类似 Navicat 左侧表列表）：
┌──────────────┬──────┬──────┬──────┬──────────┐
│ 表名         │ 类型  │ 数据库│ 记录数│ 描述      │
├──────────────┼──────┼──────┼──────┼──────────┤
│ LogEntry     │ 实体  │SQLite│ ...  │ 日志条目  │
│ sys_user     │ 实体  │ MySQL│   56 │ 用户表    │
│ sys_tenant   │ 实体  │ MySQL│    3 │ 租户表    │
└──────────────┴──────┴──────┴──────┴──────────┘
```

改动点：
- 增加列：类名(ClassType)、描述(Description)、命名空间(Namespace)
- 支持点击列头排序
- 搜索框支持多字段模糊匹配
- 记录数加载失败时显示具体错误信息而非"错误"
- DataGrid 选中行高亮

### Step 2：修复右侧 CRUD DataGrid 编辑

文件：`EntityCrudControl.xaml.cs`

改动点：
- 用 `RowEditEnding` 替代 `CellEditEnding`（更可靠）
- 编辑时先取消 DataGrid 的自动提交，手动控制保存时机
- 枚举列：编辑时显示 ComboBox
- DateTime 列：编辑时显示 DatePicker
- bool 列：用 CheckBox 编辑
- 编辑成功后只刷新当前页，不重新加载全部数据
- 编辑失败时回滚 DataGrid 值并显示错误提示

关键代码模式：
```csharp
private void EntityDataGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
{
    if (e.EditAction != DataGridEditAction.Commit) return;
    var entity = e.Row.Item;
    // 延迟执行，等 Binding 更新
    Dispatcher.BeginInvoke(() =>
    {
        try
        {
            SaveEntity(entity);
            StatusText.Text = "保存成功";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"保存失败: {ex.Message}";
            // 可选：重新加载数据以回滚
        }
    }, DispatcherPriority.Background);
}
```

### Step 3：排查并标记所有 SQLite 实体

文件：所有 IEntity 实现类

需要做的事：
1. 搜索所有使用 `DbType.Sqlite` 或 `SqliteLogManager.CreateDbClient()` 的地方
2. 找出对应的实体类
3. 给它们加上 `[DatabaseSource(DatabaseType.Sqlite)]`

已知的 SQLite 使用场景：
- `SqliteLog/LogEntry` — 已标记 ✅
- `TimedButtonOperationStatsSqliteRepository` — 需检查对应实体
- `SchedulerDbManager` — 需检查对应实体
- `SocketMessageManager` — 需检查对应实体
- `ThumbnailCacheManager` — 需检查对应实体
- `SolutionCache` — 需检查对应实体
- `RbacManager` — 需检查对应实体

搜索关键词：`DbType.Sqlite`、`SqliteLogManager.CreateDbClient`、`new SqlSugarClient.*Sqlite`

### Step 4：修复 SQLite 表的 CRUD

文件：`EntityCrudControl.xaml.cs`

改动点：
- `CreateDbForEntity` 方法需要支持不同 SQLite 数据库路径
- 当前所有 SQLite 实体都用 `SqliteLogManager.CreateDbClient()`，但不同实体可能在不同 SQLite 文件中
- 需要一种机制来指定每个 SQLite 实体的数据库路径

方案：扩展 `DatabaseSourceAttribute`：
```csharp
[DatabaseSource(DatabaseType.Sqlite, DbPath = "path/to/db.sqlite")]
public class SomeEntity : IEntity { ... }
```

或者：用一个注册表/配置来映射实体类型 → 数据库路径

### Step 5：完善错误处理和用户体验

- 记录数查询失败时，点击可重试
- 数据库连接失败时显示友好提示
- 加载中显示进度指示
- DataGrid 空数据显示占位提示
- 右侧无选中时显示使用说明

---

## 关键文件清单

| 文件 | 作用 |
|------|------|
| `EntityBrowserWindow.xaml` | 主窗口 XAML |
| `EntityBrowserWindow.xaml.cs` | 主窗口逻辑：实体发现、类型列表、嵌入 CRUD |
| `EntityCrudControl.xaml` | CRUD 控件 XAML |
| `EntityCrudControl.xaml.cs` | CRUD 逻辑：查询、分页、搜索、增删改、自动保存 |
| `DatabaseSourceAttribute.cs` | 数据库来源标记属性 |
| `SqliteLog/LogEntry.cs` | SQLite 实体示例（已标记） |
| `EntityCrudWindow.xaml` + `.cs` | 独立 CRUD 窗口（薄包装） |
| `MenuEntityBrowser.cs` | 菜单入口 |
| `BaseTableDao.cs` | 扩展方法（Save/Delete 等） |
| `MySqlControl.cs` | MySQL 连接管理 |
| `SqliteLog/SqliteLogManager.cs` | SQLite 连接管理 |

## 编译验证

```powershell
dotnet build UI/ColorVision.Database/ColorVision.Database.csproj
# 期望：0 errors
```

## 注意事项

1. `EntityCrudControl` 的列是代码动态生成的（`SetupDataGrid`），不是 XAML AutoGenerate
2. `EnumToDescriptionConverter` 定义在 `EntityCrudControl.xaml.cs` 底部
3. `BaseTableDao<T>` 是空类，所有方法都是 `BaseTableDaoExtensions` 的扩展方法
4. SqlSugar 的 `Queryable<T>()` 需要 `T : class, new()` 约束
5. `EntityBrowserWindow` 异步加载记录数，用 `EntityDataGrid.Items.Refresh()` 刷新显示
