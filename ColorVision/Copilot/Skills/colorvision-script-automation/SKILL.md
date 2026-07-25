---
name: colorvision-script-automation
description: Create and run bounded Python, Node.js, JavaScript, TypeScript, PowerShell, CMD, or batch automation in the active workspace. Use for scripts, command-line processing, file automation, batch conversion, python, node, npm, npx, pwsh, cmd, bat, and Chinese intents including 创建脚本、执行脚本、运行Python、运行Node、批量处理、自动化.
---

# ColorVision script automation

Use the generic workspace patch and shell primitives. Do not invent a separate tool or duplicate arguments for each programming language.

## Workflow

1. Identify the exact input scope, output scope, runtime, working directory, and success condition from the request and current workspace.
2. For more than a short one-line command, create or update a script with `PreviewWorkspacePatchEnvelope`, inspect the preview, then call `ApplyWorkspacePatchEnvelope` after approval.
3. Keep the saved script self-contained and non-interactive. Use command-line arguments or clearly named constants for paths instead of hidden machine-specific locations.
4. Call `RunShellCommand` from the script's exact working directory:
   - Python: `python .\script.py ...` or `py .\script.py ...`
   - Node.js: `node .\script.js ...`
   - npm/npx: use only when the workspace already declares the dependency or the user explicitly authorizes installation.
   - PowerShell: `& .\script.ps1 ...`
   - CMD/batch: set `shell` to `cmd` and invoke the exact `.cmd` or `.bat` path.
5. Report the actual exit code and the useful bounded output. A code block or command suggestion is not execution.

## Safety

- Preserve the user's source files by default. Write to a separate output directory and avoid overwrite unless the user explicitly requests replacement.
- Validate paths inside the request-scoped workspace before writing or executing.
- Do not install packages, download executables, change execution policy, elevate privileges, or invoke an interactive shell unless explicitly requested and supported by the approval flow.
- Prefer an existing ColorVision application tool for proprietary formats and product workflows. In particular, do not pretend that Pillow or stock OpenCV can decode CVRAW/CVCIE.
- For repeatable product workflows, use the matching ColorVision skill instead of copying a large bespoke program into the command argument.
