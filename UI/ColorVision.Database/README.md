# ColorVision.Database

## 🎯 功能定位

数据库访问层，提供统一的数据库操作接口和辅助控件。

## 作用范围

UI数据层，为界面组件提供数据库连接管理和数据访问功能。

## 主要功能点

- **数据库连接管理** - 支持MySQL和SQLite双数据库
- **ORM映射** - 基于SqlSugar的对象关系映射
- **连接池管理** - 高效的数据库连接复用
- **事务处理** - 支持数据库事务的统一管理
- **数据库迁移** - 自动数据库结构升级
- **配置管理** - 数据库连接配置的界面管理

## 与主程序的依赖关系

**被引用方式**:
- ColorVision.UI 引用用于数据显示控件
- ColorVision.Engine 引用用于数据持久化

**引用的程序集**:
- SqlSugar - ORM框架
- MySQL.Data - MySQL连接器
- System.Data.SQLite - SQLite连接器

## 使用方式

### 引用方式
```xml
<ProjectReference Include="..\ColorVision.Database\ColorVision.Database.csproj" />
```

### 在主程序中的启用
- 通过配置系统自动初始化数据库连接
- 界面组件通过依赖注入获取数据访问服务

## 开发调试

```bash
dotnet build UI/ColorVision.Database/ColorVision.Database.csproj
```

## 目录说明

- 包含数据库访问封装和UI控件
- 数据库配置管理界面

## 相关文档链接

- [数据存储文档](../../docs/data-storage/README.md)
- [配置管理指南](../../docs/getting-started/入门指南.md)

## 维护者

ColorVision UI团队

