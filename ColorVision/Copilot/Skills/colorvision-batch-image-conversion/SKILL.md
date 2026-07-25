---
name: colorvision-batch-image-conversion
description: Execute approved native CVRAW, CVCIE, TIFF, PNG, JPEG, BMP, or WebP format conversion, or open ColorVision's interactive batch image processor for manual algorithm configuration. Use for batch image conversion and Chinese intents including 批量转换CVRAW、CVRAW转TIFF、批量图片处理、批量执行算法.
---

# ColorVision batch image conversion

Prefer ColorVision's native batch processor over a generated Python decoder. CVRAW and CVCIE require the application-provided loader.

## Workflow

1. For an explicit conversion request with exact files or directories, call `ConvertBatchImages`.
2. Pass only paths from the current approved local scope. Use `recursive: true` only when the user requested nested folders.
3. Choose the requested output format. `same-as-source` maps CVRAW and CVCIE inputs to TIFF because those proprietary source formats are not output encoders.
4. Keep the no-overwrite guarantee. Report the returned success/failure counts and output paths; do not infer success for missing rows.
5. Call `OpenBatchImageProcessing` only when the user asks to open the UI or needs manual algorithm/options configuration beyond format-only conversion.
6. Opening the window is not conversion evidence.

One approved `ConvertBatchImages` call is bounded to 500 files. Repeated calls produce numbered output names rather than replacing existing files.

Use `colorvision-script-automation` only for surrounding tasks such as renaming, manifest generation, copying, or processing standard image formats outside the native decoder.
