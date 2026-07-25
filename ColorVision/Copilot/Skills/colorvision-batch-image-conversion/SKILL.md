---
name: colorvision-batch-image-conversion
description: Open and use ColorVision's native batch image processor for CVRAW, CVCIE, TIFF, PNG, JPEG, BMP, WebP, format conversion, and repeated image algorithms. Use for batch image conversion and Chinese intents including 批量转换CVRAW、CVRAW转TIFF、批量图片处理、批量执行算法.
---

# ColorVision batch image conversion

Prefer ColorVision's native batch processor over a generated Python decoder. CVRAW and CVCIE require the application-provided loader.

## Workflow

1. Call `OpenBatchImageProcessing`.
2. For conversion without image processing, choose `仅转换格式`; it clones the decoded image without applying an algorithm.
3. Add explicit files or a source folder. Review whether subfolders should be included.
4. Choose the output format. `与源格式相同` maps CVRAW and CVCIE inputs to TIFF because those proprietary source formats are not output encoders.
5. Review the suffix, output directory, folder-structure preservation, and overwrite protection in the native window.
6. Let the user start the batch after reviewing the file count and output settings. Do not claim conversion completed merely because the window opened.

Use `colorvision-script-automation` only for surrounding tasks such as renaming, manifest generation, copying, or processing standard image formats outside the native decoder.
