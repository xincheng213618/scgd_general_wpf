# Database Operations

The most clear and stable database entry point currently is the "Database Browser." It is suitable for table-level browsing, searching, paginated viewing, and performing inserts, updates, and deletes when conditions permit.

## When to Read This Page

- You want to confirm whether a batch of data was actually written to the database
- You need to view actual table data in MySQL or SQLite
- You need to search records by keyword
- You need to perform minor table data maintenance
- You are troubleshooting issues like "was the data written" or "why can't I see it in the list"

## Current Actual Entry Point

The Database Browser currently has a clear menu entry:

- Tools → Database Browser

Based on the current implementation, it is not a SQL editor, but a window designed for table browsing and basic maintenance.

## What You Can Do in the Database Browser

### Browse Data Sources, Databases, and Tables

The left tree first displays data sources, then drills into databases and tables. Currently, at least the following sources can be registered and displayed by default:

- MySQL
- SQLite log database

### View Data by Table

After selecting a table, the right side displays:

- Table title and source
- Column information
- Data grid
- Search box
- Pagination controls
- Status bar

### Search and Pagination

The current window supports keyword search and paginated browsing when there is a large amount of table data. If you encounter "data clearly exists but is not visible," check search conditions and page number first, rather than directly concluding that nothing was written.

### Basic Write Operations

If the current data source and table both allow writes, and the table has a primary key, the interface provides these actions:

- Insert
- Save
- Undo
- Delete
- Refresh

If the table has no primary key, or the current data source does not allow writes, do not expect all edit buttons to be available.

## Common Usage Order

1. First open the Database Browser from the Tools menu.
2. Expand the correct data source, database, and table on the left.
3. First check the table name, source, and record count to confirm you are in the right database.
4. When there is too much data, search first, then paginate.
5. When maintenance is needed, confirm whether the current table allows writes.

## Key Points to Note

### This Window is a Table Browser, Not a Universal Query Console

It is suitable for viewing tables, filtering records, and performing minor maintenance, but the current documentation no longer describes it as a unified SQL console. If a module has its own dedicated query window, use that module's own entry point.

### Tables Without Primary Keys Are Not Suitable for Arbitrary Editing

The current implementation relies on primary keys to determine the reliability of updates and deletes. Tables without primary keys are better suited for read-only browsing or cautious inserts.

### Write Capability Depends on Data Source and Table

Even if you have opened the Database Browser, it does not mean all tables are writable. When buttons are unavailable, first determine whether the current table is not writable, or whether the data source itself is read-only.

## Common Issues

### Cannot Find the Desired Data Table

- First confirm that the correct data source and database are expanded
- Then refresh the data source tree
- For MySQL, confirm that the connection configuration and database name are correct

### Table Opens, but Data is Empty

- First confirm that results are not being filtered by search conditions
- Then confirm page number and page size
- If still empty, go back to [Workflow Execution & Debugging](../workflow/execution.md) or [Log Viewer](../interface/log-viewer.md) to see if the write was actually successful

### Buttons are Grayed Out, Cannot Edit

- First confirm whether the current table allows writes
- Then confirm whether the table has a primary key
- If there is no primary key, do not mistake "not editable" for a database anomaly

### Results After Editing Are Incorrect

- First confirm whether the save was successful
- Then refresh the current table and recheck
- For critical business data, prioritize verifying that the read path is correct before making manual edits

## Continue Reading

- [Data Management Overview](./README.md)
- [Data Export & Import](./export-import.md)
- [Workflow Execution & Debugging](../workflow/execution.md)
- [Log Viewer](../interface/log-viewer.md)

## Notes

- This page only retains the Database Browser and table maintenance perspective, and no longer maintains unified SQL usage instructions not verified by the current implementation.
- The relevant implementation is primarily located in `UI/ColorVision.Database/DatabaseBrowserWindow.xaml(.cs)`.