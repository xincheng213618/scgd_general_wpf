# ColorVision.Database

This page only describes the data access and database browsing capabilities currently implemented in UI/ColorVision.Database, no longer continuing the old template's mixed writing style of "database tutorial + sample snippets + build verification records."

## Module Positioning

`ColorVision.Database` currently handles two types of responsibilities simultaneously:

- Basic data access layer for business entities and DAOs
- Database browser and Provider system for runtime maintenance

Among these, the main line more worth prioritizing now is the "database-first" browser chain, rather than the traditional entity class scanning model.

## Most Critical Directories and Files

From the project directory, the most worthwhile to recognize first are:

- `DatabaseBrowserWindow.xaml(.cs)`: Database browser main window
- `DatabaseBrowserProviderRegistry.cs`: Provider registration and lazy loading entry point
- `IDatabaseBrowserProvider.cs`: Browser Provider contract
- `DatabaseBrowserModels.cs`: Database, table, column, pagination models
- `MySqlControl.cs`: MySQL configuration and Provider creation
- `SqliteLog/SqliteLogManager.cs`: SQLite log database and Provider creation
- `BaseTableDao.cs`, `EntityBase.cs`, `ViewEntity.cs`: Business entity access layer base types

## Key Entry Point Types

### DatabaseBrowserWindow

`DatabaseBrowserWindow` is the main entry point for the current database maintenance experience. It is responsible for:

- Displaying a tree structure of data sources, databases, and tables
- Browsing result sets in `DataTable` format on the right side
- Supporting search, pagination, and sorting
- Performing common table-level operations such as insert, update, and delete

Its key feature is: the current browser no longer relies on C# entity definitions to drive the UI, but first obtains database, table, and column information from real database connections, then decides how to display and write back.

### DatabaseBrowserProviderRegistry

`DatabaseBrowserProviderRegistry` is responsible for uniformly managing browsable data sources. It currently lazy-loads default Providers and exposes to the browser:

- MySQL default Provider
- SQLite log Provider
- Other Providers registered by callers

Therefore, it is the dispatch entry point for the current database browser system.

### IDatabaseBrowserProvider

`IDatabaseBrowserProvider` is the most important abstraction boundary of the database browser. It currently requires implementers to provide:

- Database list
- Table list
- Column information
- Paginated queries
- Insert, update, delete

So the core extension point of this module is not "add an entity class," but "register a new Provider."

### MySqlControl

`MySqlControl` is currently not just a connection configuration object — it also handles:

- MySQL configuration persistence
- Connection string construction
- MySQL browser Provider creation

Therefore, MySQL-related entry points should be approached directly through it, rather than only looking at `BaseTableDao<T>`.

### SqliteLogManager

`SqliteLogManager` is both a SQLite log manager and an actual Provider source within the browser system. It provides the log database path and corresponding SQLite browsing entry points.

This also shows that `ColorVision.Database` currently does not only serve "business data," but also takes on some runtime log landing and browsing responsibilities.

### BaseTableDao / EntityBase / ViewEntity

These types remain the foundation of the current business layer entity access:

- `IEntity` unifies `Id`
- `EntityBase` provides primary key mapping base class
- `ViewEntity` is used for bindable entities
- `BaseTableDao<T>` continues to serve existing business code

But they are no longer the sole center of the current database UI browsing chain.

## Current Runtime Main Chain

This module is currently closer to the following chain:

1. `DatabaseBrowserWindow` fetches available Providers from `DatabaseBrowserProviderRegistry`.
2. Providers return database, table, and column information.
3. The browser dynamically displays `DataTable` results based on table structure.
4. Inserts, edits, and deletes land back to the database through the Provider's generic write interface.
5. For business code, the entity and DAO system can still be used in parallel, but no longer controls the browser UI.

## What Boundaries the Current Implementation Has

### The Browser Main Line Is Already "Database-First"

This is the most important current boundary change. The old approach leaned more toward "entities first, then table interface"; the more important thing now is to generate browse and maintenance interfaces directly from real database structures.

### Providers Are More Critical Than Entities

If expanding a new database source, the current higher-priority entry point is implementing `IDatabaseBrowserProvider` and registering it, rather than supplementing the system with a batch of entity classes.

### The DAO System Still Exists, But Is Not the Only Entry Point

Types like `BaseTableDao<T>` still serve existing business code, but when reading this module they can no longer be written as the sole center of database capabilities.

## How to Better Read This Module Currently

### To View the Database Browser Main Chain

Read first:

- `DatabaseBrowserWindow.xaml.cs`
- `DatabaseBrowserProviderRegistry.cs`
- `IDatabaseBrowserProvider.cs`

### To View Actual MySQL and SQLite Integration

Read first:

- `MySqlControl.cs`
- `SqliteLog/SqliteLogManager.cs`

### To View the Business Entity Access Layer

Read first:

- `IEntity.cs`
- `EntityBase.cs`
- `ViewEntity.cs`
- `BaseTableDao.cs`

## What This Page No Longer Does

This page no longer continues to maintain these high-risk contents:

- Tutorial-style sample code stacking
- "Best practice" style generalized paragraphs
- Manual build verification records
- Writing the database module as a model that only works around entity classes

## Continue Reading

- [UI Components Overview](./README.md)
- [ColorVision.SocketProtocol](./ColorVision.SocketProtocol.md)
- [ColorVision.UI.Desktop](./ColorVision.UI.Desktop.md)