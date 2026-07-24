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

---

# Desktop pet Copilot approval-card design QA

## Comparison target

- Source visual truth: `C:\Users\17917\AppData\Local\Temp\codex-clipboard-d9a3d5f5-e950-482e-bcca-c82b1f87d72d.png`
- Rendered implementation: `C:\Users\17917\AppData\Local\Temp\ColorVisionDesktopPetCaptureOutput\copilot-approval-card.png`
- Combined comparison evidence: `C:\Users\17917\AppData\Local\Temp\ColorVisionDesktopPetCaptureOutput\design-qa-action-card-comparison.png`
- State: a real pending `confirmation-required` Copilot action is surfaced above the ColorVision default desktop pet.

## Viewport and normalization

- Source pixels: 1938 x 1572.
- Implementation pixels: 522 x 915, captured from the monitor around a 300 x 360 DIP transparent WPF window and its separate approval popup at the monitor's device scale.
- Combined evidence: both artifacts were proportionally normalized to 900 pixels high, producing a 1623 x 900 side-by-side image.
- The source does not expose Codex's actionable approval overlay, so this is a reference-direction comparison rather than a pixel-identical state comparison. It evaluates pet visual language, compact hierarchy, and attention affordance without claiming exact overlay fidelity.

## Findings

- No actionable P0, P1, or P2 issue remains.
- Fonts and typography: the card uses the application's desktop UI font, a semibold 12.5-DIP action title, compact 11.5-DIP description, and a monospace tool identifier. The captured title, two-line description, tool, deadline, and button labels are readable without clipping.
- Spacing and layout rhythm: the 278-DIP popup keeps a compact five-row hierarchy, 13-DIP card padding, a clear 10-DIP action gap, and a centered pointer aligned to the pet. It extends outside the transparent pet window instead of enlarging the pet's desktop hit area.
- Colors and visual tokens: amber identifies a confirmation-required state, blue remains the sole primary action, and neutral secondary buttons preserve the approve/reject hierarchy. The light card intentionally follows ColorVision's existing notification bubble rather than copying the reference's dark settings surface.
- Image quality and asset fidelity: the visible pet is the real packaged `xiaocai.png` default asset with nearest-neighbor presentation and no placeholder, emoji, or synthesized icon.
- Copy and content: the card exposes the requested action, description, tool name, risk state, localized relative deadline, exact expiry time, and `查看 / 拒绝 / 批准` actions. The first capture mixed Chinese copy with `5m left · expires`; this was corrected to `5 分钟后到期 · HH:mm:ss`.
- Affordances and interaction states: approval keeps the existing warning confirmation dialog before any state change. Rejection is a deliberate direct action. Pending actions are refreshed every 15 seconds so an expired request cannot remain indefinitely actionable.

## Full-view comparison evidence

The combined image shows that ColorVision retains the Codex direction of a recognizable pixel pet that surfaces work needing attention, while adapting the interaction to a compact desktop overlay. The card does not obscure its own controls, the pet remains visually connected through the pointer, and the waiting badge remains visible.

## Focused region comparison evidence

The implementation screenshot is already a focused capture of the complete card and pet at native monitor density. Its text, border, state badge, pointer, and all three actions are legible, so an additional crop would not reveal more actionable detail.

## Comparison history

1. Initial rendered card: P2 copy inconsistency from mixed Chinese and English deadline text.
2. Fix: render a localized relative deadline and exact local time, while adding a periodic pending-action refresh.
3. Post-fix evidence: `copilot-approval-card.png` and the combined comparison show the corrected copy and unchanged compact layout. No P0/P1/P2 finding remains.

## Runtime interaction verification

- The WPF harness created a real action through `CopilotMcpConfirmationStore`; the initialized desktop-pet bridge surfaced it automatically.
- The rendered `拒绝` button's WPF click event was raised through the real control. The action transitioned from `Pending` to `Rejected`, and the harness completed with `result.txt = ok`.
- Shared approval-decision tests separately verify the three safety paths: client-confirmed approval without execution, immediate in-app execution after approval, and Agent Framework approval that resumes the same session without direct execution.

## Follow-up polish

- P3: add a dark-theme card variant if the product later standardizes theme-aware notification bubbles.
- P3: capture a source Codex approval overlay if that state becomes directly available; the supplied reference only shows pet selection.

final result: passed

---

# Desktop pet settings design QA

## Comparison target

- Source visual truth: `C:\Users\17917\AppData\Local\Temp\codex-clipboard-d9a3d5f5-e950-482e-bcca-c82b1f87d72d.png`
- Rendered implementation: `C:\Users\17917\AppData\Local\Temp\ColorVisionDesktopPetCaptureOutput\settings.png`
- Side-by-side evidence: `C:\Users\17917\AppData\Local\Temp\ColorVisionDesktopPetCaptureOutput\design-qa-comparison-final.png`
- Narrow-width evidence: `C:\Users\17917\AppData\Local\Temp\ColorVisionDesktopPetCaptureOutput\settings-min-width.png`
- Open-pet state evidence: `C:\Users\17917\AppData\Local\Temp\ColorVisionDesktopPetCaptureOutput\settings-pet-open.png`
- State: pet settings loaded, ColorVision default pet selected, nine local Codex pets discovered.

## Viewport and normalization

- Source pixels: 1938 x 1572.
- Source normalized for comparison: 888 x 720.
- Implementation pixels and WPF logical viewport: 820 x 720 at 96 DPI.
- Responsive check: 700 x 720 at 96 DPI.
- The source includes Codex application chrome and a settings sidebar; the implementation capture is the ColorVision settings content window. The source was proportionally downsampled to the same height, with no crop or density interpolation applied to the implementation.

## Findings

- No actionable P0, P1, or P2 differences remain.
- The first comparison found a P2 density mismatch: ColorVision showed only four complete pets while the reference showed almost the entire built-in catalog. The list also used separate oversized cards and fixed appearance/Copilot panels.
- Fixed by switching to one compact bordered list with divider rows, moving custom-pet, appearance, and Copilot controls into the same page scroll, and moving the folder action out of the header.
- The revised comparison shows the same hierarchy and scan pattern as Codex: title and actions, explanatory text, one continuous pet list, thumbnail/name/description, and a right-aligned selection action. Eight complete rows plus the next row are visible at 820 x 720, and the 700-pixel-width capture has no overlap or clipped persistent controls.

## Required fidelity surfaces

- Fonts and typography: ColorVision keeps its existing WPF font family and weights. Title, pet names, descriptions, badges, and action hierarchy match the reference closely; no truncation or unintended wrapping is visible.
- Spacing and layout rhythm: compact list rows, dividers, thumbnail scale, right-aligned actions, and page scrolling now follow the reference. The narrow-width header subtitle wraps without colliding with actions.
- Colors and visual tokens: the implementation intentionally uses the active ColorVision theme resources rather than hard-coding the reference's dark Codex palette. Contrast is clear in the captured light theme.
- Image quality and asset fidelity: the ColorVision default asset is crisp, and all nine Codex thumbnails are decoded directly from the installed Codex sprite sheets with nearest-neighbor scaling. No placeholder imagery is present.
- Copy and content: ColorVision-specific source labels, local-asset discovery status, and the required default pet are intentional additions. Create, refresh, select, wake/tuck-away, custom folder, appearance, and Copilot controls are represented.

## Focused evidence

The final full-view comparison is high enough resolution to read the list typography, source badges, thumbnails, buttons, and row spacing, so a separate focused crop was not needed. Directional sprite evidence is captured separately in:

- `C:\Users\17917\AppData\Local\Temp\ColorVisionDesktopPetCaptureOutput\codex-running-left.png`
- `C:\Users\17917\AppData\Local\Temp\ColorVisionDesktopPetCaptureOutput\codex-running-right.png`

## Comparison history

1. Initial implementation capture: P2 list density and fixed-footer mismatch.
2. Fix: compact continuous list, page-level scrolling, folder management section below the list.
3. Revised capture: the earlier mismatch is resolved; no actionable P0/P1/P2 findings remain.

## Runtime interaction verification

- A real WPF preview window was controlled with Windows input. Dragging the floating pet horizontally from `(150, 220)` to `(235, 220)` moved the transparent window and returned cleanly to its resting state.
- The settings action was clicked through the rendered UI and verified through two complete transitions: `收起宠物` to `唤醒宠物`, then `唤醒宠物` to `收起宠物`.
- The directional Codex frames decoded and rendered distinctly for both left and right movement, as shown by the focused sprite evidence paths above.

## Follow-up polish

- P3: replace the text refresh action with the repository's standard refresh icon if a matching shared icon style becomes available.
- P3: capture the same page under ColorVision's dark theme for a closer palette-only comparison.

final result: passed

---

# Desktop pet Codex creation-flow design QA

## Comparison target

- Source visual truth: `C:\Users\17917\AppData\Local\Temp\codex-clipboard-d9a3d5f5-e950-482e-bcca-c82b1f87d72d.png`
- Rendered Codex-creation state: `C:\Users\17917\AppData\Local\Temp\ColorVisionDesktopPetCaptureOutput\create.png`
- Rendered import-fallback state: `C:\Users\17917\AppData\Local\Temp\ColorVisionDesktopPetCaptureOutput\create-import.png`
- Side-by-side product-direction evidence: `C:\Users\17917\AppData\Local\Temp\ColorVisionDesktopPetCaptureOutput\design-qa-create-comparison.png`
- Generated-package result state: `C:\Users\17917\AppData\Local\Temp\ColorVisionDesktopPetCaptureOutput\settings-codex-created.png`
- State: Codex Desktop detected, Hatch Pet available for first-run installation, Codex creation selected by default, and manual sprite import available as the second tab.

## Viewport and normalization

- Source pixels: 1938 x 1572.
- Source normalized for the creation comparison: 801 x 650.
- Creation and import implementation pixels and WPF logical viewport: 660 x 650 at 96 DPI.
- Combined evidence: 1485 x 650, using a proportional source downsample and a 24-pixel separator. The implementation was not rescaled.
- The supplied source shows the Codex pet-selection settings page rather than its create-task dialog. The comparison therefore checks product hierarchy, density, copy direction, and real Codex asset/workflow use; it does not claim pixel-identical state fidelity.

## Findings

- No actionable P0, P1, or P2 difference remains.
- Fonts and typography: the window uses ColorVision's existing WPF font family and hierarchy. The 24-DIP title, 18-DIP section title, semibold field labels, explanatory text, status copy, and primary action remain readable without truncation or unintended wrapping.
- Spacing and layout rhythm: the two tabs keep one clear task per state. The creation tab groups readiness, optional concept, handoff explanation, and primary action in reading order; the import tab preserves the existing name, description, file, version, constraint, and action flow. Both 660 x 650 captures have consistent margins and no clipped persistent controls.
- Colors and visual tokens: the implementation deliberately uses the active ColorVision light-theme resources, with blue reserved for selection and the primary tab state. Status and bordered information groups remain subordinate to the main action.
- Image quality and asset fidelity: the creation dialog introduces no fake pet art, emoji, handcrafted SVG, or placeholder. The resulting settings capture uses the actual decoded Codex sprite sheet and shows the new package alongside the nine real installed Codex pets and the packaged ColorVision default.
- Copy and content: the UI states honestly that ColorVision opens a prefilled Codex task and the user confirms sending it there. It does not imply that ColorVision's text Copilot can generate image assets internally. The fallback tab explicitly supports PNG/WebP and the v1/v2 grid constraints.
- Icons and surfaces: no new icon approximation is used. Standard WPF tabs, text boxes, combo box, and buttons match existing ColorVision controls; the restrained bordered status and explanation groups clarify readiness and handoff without creating a competing card stack.
- States and interactions: the primary button is disabled during availability detection, Codex creation is the default, tab switching updates the primary action, and manual import remains reachable. A real WPF harness also exercised package discovery and automatic selection after a completed `pet.json` appeared.
- Accessibility and viewport resilience: the flow uses native keyboard-focusable WPF controls, visible labels, conventional tab order, wrapped explanatory copy, and a scrollable content region. The minimum window is 580 x 560; the captured default size has no overlap.

## Full-view comparison evidence

The combined image shows that the new dialog keeps the reference's clear pet-management hierarchy and compact desktop density while extending it with an explicit creation handoff. The visual language intentionally remains ColorVision-native instead of copying Codex's dark application shell. No source pet imagery was replaced or approximated.

## Focused region comparison evidence

The native 660 x 650 captures are readable focused views of both core states. `create.png` verifies readiness, concept entry, explanatory handoff, and the Codex primary action. `create-import.png` verifies every import field, the PNG/WebP and grid-size constraint copy, and the changed `导入并选择` primary action. A separate result-state capture verifies that a newly completed Codex package is labeled `Codex 自定义`, selected, and announced.

## Comparison history

1. First post-implementation comparison: no actionable P0/P1/P2 finding was visible, so no visual fix was required.
2. Interaction verification: the harness switched to the import tab, created a complete Codex-compatible package under an isolated `CODEX_HOME`, waited for the real four-second watcher, and confirmed automatic selection as `codex-custom:pets:qa-generated`.
3. Post-verification evidence: `create.png`, `create-import.png`, and `settings-codex-created.png` were recaptured from the final build; the harness completed with `result.txt = ok`.

## Implementation checklist

- [x] Make Codex generation the default creation path.
- [x] Explain the prefilled-task handoff accurately.
- [x] Keep a complete manual import fallback.
- [x] Detect and preserve an existing user Hatch Pet installation.
- [x] Discover a completed package and select it automatically.
- [x] Use actual Codex and packaged ColorVision assets.
- [x] Verify both tab states and the result state in rendered WPF.

## Follow-up polish

- P3: capture the creation dialog under ColorVision's dark theme and at 150% display scaling if those variants become release-gating visual targets.
- P3: if Codex exposes a stable completion callback in a future installed version, replace the bounded directory watcher with that callback while keeping the same visible flow.

final result: passed
