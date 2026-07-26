---
name: colorvision-batch-image-conversion
description: Convert CVRAW or CVCIE with ColorVision's native decoder, including direct conversion and explicit Python, CMD, or command-line batch wrappers around ColorVision.exe. Also covers the interactive batch processor. Use for CVRAW转TIFF、批量转换CVRAW、Python批量转换CVRAW、ColorVision命令行导出、批量图片处理、批量执行算法.
---

# ColorVision batch image conversion

CVRAW and CVCIE are proprietary ColorVision formats. They must be decoded by ColorVision. Never parse their binary layout in Python, PowerShell, Pillow, OpenCV, tifffile, or another generic image package, and never install packages for that purpose.

## Choose one route

- Direct conversion request: call `ConvertBatchImages` with the exact approved files or directories.
- Explicit Python, CMD, PowerShell, CLI, or reusable-script request: create a wrapper that invokes `ColorVision.exe` with the CLI contract below. Do not substitute `ExecuteMenu`, `OpenBatchImageProcessing`, or a generated decoder.
- Interactive request: call `OpenBatchImageProcessing` only when the user asks to open the UI or needs manual algorithm/options configuration.

## ColorVision.exe CLI contract

Resolve the executable in this order:

1. Use `application_executable` from the current environment when it is an existing `ColorVision.exe`.
2. Try `C:\Program Files\ColorVision Inc\ColorVision\ColorVision.exe`.
3. If needed, perform one bounded search under `C:\Program Files\ColorVision Inc`. Do not search an entire drive.

Invoke one source file as an argument list:

```text
ColorVision.exe -e "<source.cvraw>" -o "<output-directory>" -q -t tif -mx 5
```

- `-e` / `--export`: one `.cvraw` or `.cvcie` source.
- `-o` / `--output`: existing or newly created output directory.
- `-q` / `--quiet`: required for automation so the export process exits. Exit code `0` means the native export completed; nonzero means failure.
- `-t` / `--type`: `tif`, `png`, or `jpg`.
- `-mx` / `--mx`: encoder compression or quality value.
- Output names can come from embedded ColorVision metadata and may not match the source file stem. CVCIE can also create multiple channel outputs.

Do not call a generic `--help` path or infer alternate argument names. Paths must be passed as separate process arguments so spaces remain intact.

## Python wrapper requirements

Use `colorvision-script-automation` to create and run the saved wrapper. The wrapper must:

1. Use the standard library only and call `subprocess.run([exe, "-e", source, "-o", output_dir, "-q", "-t", format, "-mx", value], shell=False, capture_output=True, text=True, timeout=...)`.
2. Create a separate output directory, enumerate only the requested `.cvraw` or `.cvcie` files, and recurse only when explicitly requested.
3. Run sequentially by default. Use a small bounded worker count only when the user explicitly asks for parallelism because every item starts ColorVision.
4. Snapshot the output directory before each invocation and record new or changed files with the requested extension afterward. Treat exit code `0` plus that output evidence as success; never predict the output name from the source path. Treat a nonzero exit, timeout, or missing output as failure and retain stdout/stderr for the summary.
5. Preserve every source file. Never delete, truncate, replace, or skip a file merely because it is small. Overwrite output only with explicit authorization.
6. Report the processed, succeeded, failed, and skipped counts plus failed source paths.

## Direct native tool requirements

For `ConvertBatchImages`, pass only paths from the current approved local scope and set `recursive: true` only when requested. `same-as-source` maps CVRAW/CVCIE to TIFF. One approved call is bounded to 500 files and repeated calls create numbered outputs instead of replacing files. Report returned rows and counts; opening a window is never conversion evidence.
