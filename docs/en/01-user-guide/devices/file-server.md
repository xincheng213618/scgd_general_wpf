# File Server

The file server is used to integrate remote file lists, file downloads, and file uploads into the unified device system. It is suitable for scenarios like "result files are on the server," "need to fetch images from the device side," or "need to send files to a remote location."

## When to Read This Page

- You need to add or configure a file server
- You need to view available remote file lists
- You need to download a source file to the current system
- You need to upload a file to a remote service
- You are troubleshooting issues like "file visible but cannot open" or "no response after upload"

## How This Service is Typically Used

### First Complete Configuration

First ensure that basic configurations like device code, send topic, and subscribe topic are correct. Many file server issues appear as "file operation failure" but actually get stuck earlier at connection or topic configuration.

### Then View File List

If you don't even know what files are currently available remotely, first fetch the file list, then decide whether to download or upload; do not guess file names directly.

### Open or Download When Content is Needed

Common file actions in the current implementation include:

- Get all file list
- Open or download specified file
- Upload specified file

## Common Usage Order

1. First create and save the file server in the device list.
2. Confirm that connection-related configurations are correct.
3. First pull the file list once to confirm that the target file actually exists remotely.
4. Then execute download or open actions.
5. When files need to be sent back, execute upload.

## Key Points During Use

### Confirm File Name First

The file server operates based on file names. If the file name is wrong, later viewing of display windows is not very meaningful.

### List and Content are Two Steps

Being able to see the list does not mean specific files can definitely be opened normally; similarly, successful upload does not mean you are already viewing it in the correct directory or correct type.

### First Exclude Topic and Device Code Issues

The current implementation has explicit matching for device code and subscribe topic. When encountering no response, prioritize checking these basic conditions.

## Common Issues

### Cannot Retrieve File List

- First confirm that send topic and subscribe topic are correct
- Then confirm whether the current device code matches the return message
- If the server has heartbeat but no file events, prioritize checking specific file operation configuration

### File Can Be Listed but Opening Fails

- First confirm whether the file name is correct
- Then confirm whether the requested file type is the target type
- If the problem persists, use [Log Viewer](../interface/log-viewer.md) to see if there are clearer error messages in the return

### Cannot See File After Upload

- First confirm whether the upload action was actually sent and completed
- Then pull the file list again rather than relying only on the old list
- If the remote has directory or type distinctions, confirm you are viewing the correct scope

### File Server Unstable in Workflows

- First verify manually whether file list, download, or upload can succeed
- Then go back to [Workflow Execution & Debugging](../workflow/execution.md) to see if the workflow uses the same file server
- If necessary, also check whether device code and topic configuration match manual testing

## Continue Reading

- [Device Service Overview](./overview.md)
- [Image Editor Overview](../image-editor/overview.md)
- [Workflow Execution & Debugging](../workflow/execution.md)
- [Log Viewer](../interface/log-viewer.md)

## Notes

- This page only retains the usage perspective of the file server and no longer maintains MQTT message structure analysis.
- The current implementation is primarily located in `Engine/ColorVision.Engine/Services/Devices/FileServer/`.