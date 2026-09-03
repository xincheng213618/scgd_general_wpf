---
name: colorvision-batch-image-conversion
description: Convert CVRAW or CVCIE with ColorVision's native decoder, or run one explicitly whitelisted local/headless/deterministic Catalog image algorithm through the approved batch tool. Also covers explicit Python, CMD, or command-line conversion wrappers and the interactive batch processor. Use for CVRAW转TIFF、批量转换CVRAW、Python批量转换CVRAW、ColorVision命令行导出、批量图片处理、批量执行算法.
---

# ColorVision batch image conversion and approved algorithms

CVRAW and CVCIE are proprietary ColorVision formats. They must be decoded by ColorVision. Never parse their binary layout in Python, PowerShell, Pillow, OpenCV, tifffile, or another generic image package, and never install packages for that purpose.

## Choose one route

- Direct conversion or explicit image-algorithm request: call `ConvertBatchImages` with the exact approved files or directories. For an algorithm, also pass its stable Catalog ID or compatibility alias and only parameters from that descriptor's schema.
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
- `-q` / `--quiet`: run the native exporter without its options window. When the CVRAW/CVCIE quiet exporter is reached, it exits `0` after `SaveToTif` returns success and `1` on its reported failure. Earlier startup/export-dispatch failures can still show a dialog and exit `0`; quiet mode is not a guarantee that every startup path is non-interactive. Always use a timeout and verify output files.
- `-t` / `--type`: use exact lowercase `tif`, `png`, or `jpg`; output extensions are `.tiff`, `.png`, and `.jpg`. Omitted or unrecognized values select TIFF.
- `-mx` / `--mx`: TIFF compression (default `5`, LZW; `8`, ZIP), PNG level (`0`–`9`; omit for automatic), or JPEG quality (`0`–`100`, default `100`). Do not reuse the TIFF value for every format.
- The CLI starts with the source file stem as `Name` and adds source/channel suffixes. CVRAW exports its source image; CVCIE can emit `_X`, `_Y`, `_Z`, and an available associated `_Src`. The saver applies `Path.ChangeExtension`, so dotted names can shorten. Inspect actual outputs rather than assuming one file or a particular name.
- This CLI can overwrite same-named outputs and does not roll back a partially written channel set. Use a new empty directory per source and per run unless replacement is explicitly authorized. The CLI does not inherit `ConvertBatchImages` collision numbering.

Do not call a generic `--help` path or infer alternate argument names. Paths must be passed as separate process arguments so spaces remain intact.

## Python wrapper requirements

Use `colorvision-script-automation` to create and run the saved wrapper. The wrapper must:

1. Use the standard library only and call `subprocess.run([exe, "-e", source, "-o", output_dir, "-q", "-t", format], shell=False, capture_output=True, text=True, timeout=...)`. Append `["-mx", value]` only for a chosen encoder setting; omit it for automatic PNG compression.
2. Create a separate output directory with a unique empty subdirectory per source and run. Enumerate only the requested `.cvraw` or `.cvcie` files, and recurse only when explicitly requested. Treat that enumeration as the selected execution set. If an earlier search found a different count or broader scope, reconcile the difference before execution and never describe the narrower run as converting every discovered file.
3. Run sequentially by default. Use a small bounded worker count only when the user explicitly asks for parallelism because every item starts ColorVision.
4. Snapshot each source's output directory before invocation and record nonempty new or changed files afterward, using `.tiff` for `-t tif`. Require exit code `0` plus the requested output evidence; where the request names particular CVCIE channels, check each channel rather than accepting any one file. Treat a nonzero exit, timeout, missing output, or missing requested channel as failure. Keep bounded stdout/stderr, but do not assume a GUI executable reports all failures there.
5. Preserve every source file. Never delete, truncate, replace, or skip a file merely because it is small. Overwrite output only with explicit authorization.
6. Report the selected, processed, succeeded, failed, and skipped counts plus failed source paths. Exit with a nonzero process code when any selected source failed or was not processed; exit code `0` is reserved for a complete successful run over the selected set.

## Direct native tool requirements

For `ConvertBatchImages`, pass only paths from the current approved local scope and set `recursive: true` only when requested. `same-as-source` maps CVRAW/CVCIE to TIFF. One approved call is bounded to 500 files and repeated calls create numbered outputs instead of replacing files. The response includes at most 100 per-file rows plus full counts and `results_truncated`; report truncation instead of claiming every row was displayed. Account for `skipped_identity` separately from conversions. Opening a window is never conversion or algorithm-execution evidence.

The direct tool loads one image per source through the batch loader; it is not the CLI's CVCIE channel-set exporter. Use the native CLI for the available default channel set, or the native export window when the user needs to select channels; the CLI has no channel-selection arguments. Verify the requested channel outputs.

When `algorithm` is present, the tool resolves it through the unified Catalog and then enforces the explicit Copilot allowlist plus `Batch | Headless | Local | Deterministic | Copilot` capabilities. Never invent a provider ID, reflect over device/remote algorithms, pass an arbitrary model, or bypass a rejection with `ExecuteMenu`, shell, or generated code. Unknown, unrelated, and out-of-range parameter fields are rejected by the schema and Catalog contract. Format conversion remains an output policy rather than an algorithm.
