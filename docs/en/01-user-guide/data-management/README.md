# Data Management

This chapter only answers three questions: where to typically view data, when to go to the database page, and when to use import/export.

## When to Read This Chapter

- You want to confirm where test results or business data are stored
- You need to view, organize, or clean up database content
- You need to perform batch import or export
- You are troubleshooting issues like "data not saved," "export failed," or "cannot see historical records"

## How to Read This Chapter

### Start with Database

- [Database Operations](./database.md)

Suitable scenarios:

- Confirm whether data was actually written to the database
- Query results by record, time, or condition
- Troubleshoot connection, write, or query issues

### Then Read Import/Export

- [Data Export & Import](./export-import.md)

Suitable scenarios:

- Need to deliver results to external systems or for manual analysis
- Need to migrate a batch of configuration or data
- Want to confirm the currently supported export formats and processing order

## Common Usage Order

1. First confirm that the current project, device, or workflow has been properly executed.
2. If you suspect data was not saved, first go to [Database Operations](./database.md) to check records.
3. Only after confirming data already exists, proceed to [Data Export & Import](./export-import.md) for further processing.
4. If the exported content is incorrect, first go back and verify the source data instead of repeatedly retrying the export button.

## Common Issues

### Cannot See Desired Historical Data

- First confirm that the currently connected database instance is correct
- Then confirm that filter conditions, time range, and project objects are correctly selected
- If the workflow was just executed, also check [Log Viewer](../interface/log-viewer.md) to see whether the write was actually successful

### Data Exists, but Export Results Are Incorrect

- First confirm that the exported objects are the same batch of data currently being viewed
- Then confirm that the export format matches your downstream usage scenario
- If fields are missing, prioritize going back to the source data page to verify, rather than directly concluding the export function is malfunctioning

### Unsure if the Problem is in the Workflow or Data Layer

- First check [Workflow Execution & Debugging](../workflow/execution.md)
- Then use [Log Viewer](../interface/log-viewer.md) to confirm whether the failure occurred during execution, write, or export phase

## Continue Reading

- [Database Operations](./database.md)
- [Data Export & Import](./export-import.md)
- [Workflow Overview](../workflow/README.md)
- [Common Issues](../troubleshooting/common-issues.md)

## Notes

- This page only retains the reading path for data management and no longer maintains generalized data storage descriptions.