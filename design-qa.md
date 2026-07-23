# Copilot attachment composer design QA

## Evidence

- Source visual truth:
  - `C:\Users\17917\AppData\Local\Temp\codex-clipboard-901804ac-87a7-4b4d-b5d3-d8a174a27fd0.png` (477 x 111, 96 DPI): attached live-context chip is too wide.
  - `C:\Users\17917\AppData\Local\Temp\codex-clipboard-5c11ebe9-60e8-4f6d-a33d-64eff502ecf4.png` (438 x 93, 96 DPI): the row disappears after removal and offers no visible recovery path.
  - Earlier removable-pill references remain the structural target for `x / +`, type icon, and one-line ellipsized text.
- Rendered implementation: Computer Use Windows.Graphics.Capture of `C:\Users\17917\Desktop\scgd_general_wpf\ColorVision\bin\x64\Debug\net10.0-windows\ColorVision.exe`, capture `screenshot-0` (1268 x 714). The capture API exposed an in-session image rather than a persistent bitmap path.
- Viewport: ColorVision main window at 1268 x 714; Copilot is docked on the right at its compact desktop width.
- State: light theme. The live Flow context was available, attached by clicking the compact `+` pill, removed through the `x` control, and immediately remained visible as the compact `+` recovery pill.
- Density normalization: both source crops are 96-DPI images. The implementation capture is a 96-DPI logical desktop capture. Comparisons used the composer region, because the source images intentionally omit surrounding application chrome.

## Findings

- No actionable P0, P1, or P2 mismatch remains.
- Fonts and typography: attachment labels use the existing application font at 11 px, stay on one line, and ellipsize inside a constrained column.
- Spacing and layout rhythm: every attachment pill is fixed at 168 x 26 DIPs, with a 16-DIP remove/add affordance, 14-DIP type icon, compact inset, and 6-DIP inter-pill gap. Long labels can no longer expand across the composer.
- Colors and visual tokens: the pill continues to use theme-aware `ButtonBackground`, `ButtonBorderBrush`, `GlobalTextBrush`, and `PrimaryBrush`; it introduces no separate card background.
- Image quality and assets: no custom raster asset is needed. The controls use the application's existing Segoe MDL2 icon library, while image attachments keep their real thumbnail.
- Copy and content: attached state shows only `x`, type icon, and name. Available state shows only `+`, type icon, and name. No new explanatory label was added.
- Affordances and interaction states: `+` attaches the current live context and changes to `x`; `x` removes it from the next request and changes back to `+`, so removal is reversible without reopening a menu.
- Responsiveness and accessibility: each pill has a stable width, labels truncate, multiple items stay in one horizontally scrollable row, and UI Automation exposes the remove control as `移除附件`.

## Full-view comparison evidence

The first source crop showed one attachment consuming almost the entire composer width. The verified debug build keeps the same item to a compact fixed-width pill, leaving visible breathing room before the composer edge. The second source crop showed a blank row after removal; the verified build retains a same-size `+` pill in that state.

## Focused region comparison evidence

Each source crop and its matching live implementation state were emitted together in one comparison result. The focused composer region confirms the two intended state changes: variable-width attached pill to fixed-width attached pill, and blank post-removal row to an in-place recovery pill.

## Comparison history

1. Earlier P1: duplicate current-window card and redundant status/explanation copy. Fixed by using a single attachment row.
2. Earlier P2: attachment type badge and extra explanation consumed another line. Fixed by reducing the row to icon, name, and direct action.
3. Current P2: a long live-context title expanded the pill across most of the composer. Fixed with a 168 x 26 DIP component and constrained ellipsis layout.
4. Current P1: removing live context left no visible way to restore it. Fixed with a reversible two-state pill: attached uses `x`; available uses `+`.
5. Post-fix evidence: in the x64 debug build, `+ -> attached x -> removed +` was exercised through the real WPF controls. No P0/P1/P2 issue remained.

## Implementation checklist

- [x] Use a compact fixed attachment size.
- [x] Keep long names on one truncated line.
- [x] Preserve the single horizontal row.
- [x] Make live-context removal reversible in place.
- [x] Keep send-time live-context refresh from the preceding iteration.
- [x] Compile the WPF XAML and pass the focused Copilot tests.

## Follow-up polish

- P3: the revised fixed-size and recovery states were not recaptured under the dark theme; all changed colors remain dynamic theme resources.

final result: passed
