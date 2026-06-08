# ColorVision.Database

本页只描述 UI/ColorVision.Database 当前已经落地的数据访问与数据库浏览能力，不再继续维护旧模板里那种“数据库教程 + 示例片段 + 构建验证记录”的混合写法。

## 模块定位

`ColorVision.Database` 当前同时承担两类职责：

- 业务实体和 DAO 的基础数据访问层
- 面向运行时维护的数据库浏览器与 Provider 体系

其中现在更值得优先关注的主线，是“数据库优先”的浏览器链，而不是传统的实体类扫描模式。

## 当前最关键的目录和文件

从项目目录看，最值得先认识的是：

- `DatabaseBrowserWindow.xaml(.cs)`：数据库浏览器主窗口
- `DatabaseBrowserProviderRegistry.cs`：Provider 注册与懒加载入口
- `IDatabaseBrowserProvider.cs`：浏览器 Provider 契约
- `DatabaseBrowserModels.cs`：库、表、列、分页模型
- `MySqlControl.cs`：MySQL 配置和 Provider 创建
- `BaseTableDao.cs`、`EntityBase.cs`、`ViewEntity.cs`：业务实体访问层基础类型

## 关键入口类型

### DatabaseBrowserWindow

`DatabaseBrowserWindow` 是当前数据库维护体验的主入口。它负责：

- 展示数据源、库、表的树形结构
- 在右侧按 `DataTable` 方式浏览结果集
- 支持搜索、分页、排序
- 执行新增、更新、删除等通用表级操作

它的关键特点是：当前浏览器不再依赖 C# 实体定义来驱动 UI，而是先从真实数据库连接拿库、表、列信息，再决定如何展示和写回。

### DatabaseBrowserProviderRegistry

`DatabaseBrowserProviderRegistry` 负责统一管理可浏览的数据源。它当前会懒加载默认 Provider，并向浏览器暴露：

- MySQL 默认 Provider
- 其他调用方自行注册的 Provider

因此它是当前数据库浏览器体系的调度入口。

### IDatabaseBrowserProvider

`IDatabaseBrowserProvider` 是数据库浏览器最重要的抽象边界。当前它要求实现方提供：

- 库列表
- 表列表
- 列信息
- 分页查询
- 插入、更新、删除

所以这个模块的核心扩展点不是“加一个实体类”，而是“注册一个新的 Provider”。

### MySqlControl

`MySqlControl` 当前不只是连接配置对象，它还承担：

- MySQL 配置持久化
- 连接字符串构造
- MySQL 浏览器 Provider 创建

因此 MySQL 相关入口应直接顺着它去看，而不是只看 `BaseTableDao<T>`。

### BaseTableDao / EntityBase / ViewEntity

这些类型仍然是当前业务层实体访问的基础：

- `IEntity` 统一 `Id`
- `EntityBase` 提供主键映射基类
- `ViewEntity` 用于可绑定实体
- `BaseTableDao<T>` 继续服务已有业务代码

但它们已经不是当前数据库 UI 浏览链的唯一中心。

## 当前运行时主链

这套模块当前更接近下面这条链：

1. `DatabaseBrowserWindow` 向 `DatabaseBrowserProviderRegistry` 取可用 Provider。
2. Provider 返回库、表、列信息。
3. 浏览器按表结构动态展示 `DataTable` 结果。
4. 新增、编辑、删除通过 Provider 的通用写接口落回数据库。
5. 对于业务代码，实体和 DAO 体系仍可以并行使用，但不再控制浏览器 UI。

## 当前实现有哪些边界

### 浏览器主线已经是“数据库优先”

这是当前最重要的边界变化。旧思路更偏向“先有实体，再有表格界面”；现在更重要的是直接从真实数据库结构生成浏览和维护界面。

### Provider 比实体更关键

如果要扩一个新的数据库来源，当前更优先的切入点是实现 `IDatabaseBrowserProvider` 并注册，而不是给系统补一批实体类。

### DAO 体系仍在，但不是唯一入口

`BaseTableDao<T>` 等类型依然服务现有业务代码，但阅读这个模块时不能再把它们写成数据库能力的唯一中心。

## 当前更适合怎样读这个模块

### 想看数据库浏览器主链

先看：

- `DatabaseBrowserWindow.xaml.cs`
- `DatabaseBrowserProviderRegistry.cs`
- `IDatabaseBrowserProvider.cs`

### 想看 MySQL 的实际接入

先看：

- `MySqlControl.cs`

### 想看业务实体访问层

先看：

- `IEntity.cs`
- `EntityBase.cs`
- `ViewEntity.cs`
- `BaseTableDao.cs`

## 这页不再做什么

本页不再继续维护这些高风险内容：

- 教程式示例代码堆叠
- “最佳实践”式泛化段落
- 手工构建验证记录
- 把数据库模块写成只围绕实体类工作的旧模型

## 继续阅读

- [UI组件概览](./README.md)
- [ColorVision.SocketProtocol](./ColorVision.SocketProtocol.md)
- [ColorVision.UI.Desktop](./ColorVision.UI.Desktop.md)
