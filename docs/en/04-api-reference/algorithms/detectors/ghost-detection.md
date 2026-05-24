# Ghost Detection

This page only describes the Ghost detection integration chain that actually exists in the current repository, no longer maintaining the old "standalone `ghost-detection` algorithm API" draft.

## What This Page Actually Covers

Based on current source code status, Ghost detection is not an independent public algorithm package, but a branch of the ARVR template family within `ColorVision.Engine`. It currently consists of these layers:

- Ghost parameter template
- Ghost algorithm UI host
- Image input and color selection interface
- MQTT command packaging
- Result loading, overlay display, and CSV export

Therefore, what this page really covers is "how Ghost is hosted and run within the main application," not a fictional Process API that exists independently of the host.

## Most Critical Files

- `Engine/ColorVision.Engine/Templates/ARVR/Ghost/TemplateGhost.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Ghost/GhostParam.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Ghost/AlgorithmGhost.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Ghost/DisplayGhost.xaml.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Ghost/ViewHandleGhost.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Ghost/AlgResultGhostDao.cs`

If you just want to understand how Ghost is currently configured, how it sends commands, and how it displays results, these files already cover the main path.

## How the Current Main Chain Runs

### Template Entry Point

`TemplateGhost` is the parameter template entry point for Ghost. The current implementation is very straightforward:

- Inherits `ITemplate<GhostParam>`
- `TemplateDicId = 7`
- `Code = ghost`

This indicates that Ghost currently follows the classic strongly-typed parameter template chain, not a JSON template or independent configuration file chain.

### Parameter Model

`GhostParam` currently exposes a set of parameters targeting ghost lattice detection, rather than the generalized set of thresholds, area, and morphological switches from the old draft. The core fields currently directly visible include:

- `Ghost_radius`
- `Ghost_cols`
- `Ghost_rows`
- `Ghost_ratioH`
- `Ghost_ratioL`

From the field naming and descriptions, this set of parameters is more oriented toward geometric and grayscale constraints of the "ghost lattice to be detected," rather than a generic parameter table for arbitrary image defect detectors.

### Algorithm Host

`AlgorithmGhost` is currently not a low-level image processing kernel, but a host class derived from `DisplayAlgorithmBase`. Its main responsibilities are:

- Opening the `TemplateGhost` editing window
- Providing the `DisplayGhost` user control
- Maintaining the current color selection `CVOLEDCOLOR`
- Packaging template, color, device information, and image path into a message

Ultimately, it publishes a message with event name `Ghost`, rather than exposing a unified `ghost-detection` call interface to the outside.

### Input and Runtime Interface

`DisplayGhost` is the runtime interface that users actually interact with. It handles more specific tasks than the "input image + parameters" of old documentation:

- Binding to `TemplateGhost.Params`
- Providing `BLUE`, `GREEN`, `RED` three `CVOLEDCOLOR` choices
- Getting image source device from `ServiceManager`
- Supporting three input paths: batch number, Raw/CIE files, and local images
- Allowing refresh of device-side Raw/CIE file lists
- Allowing images to be opened locally or on the device side

Therefore, the current Ghost runtime surface is essentially a WPF panel with device interaction capabilities, not a pure algorithm function entry point.

### MQTT Command Chain

`AlgorithmGhost.SendCommand(...)` currently packages this information:

- `ImgFileName`
- `FileType`
- `DeviceCode`
- `DeviceType`
- `TemplateParam`
- `Color`

Then constructs `MsgSend` and publishes the `Ghost` event.

This also indicates that the actual execution side of Ghost computation is not within this UI class, but on the other side of the message chain.

## How Results Are Currently Handled

`ViewHandleGhost` is the most critical entry point in the current result display chain. It handles:

- Loading result details via `AlgResultGhostDao.Instance.GetAllByPid(...)`
- Connecting result list back to `ViewResultAlg`
- Drawing overlay points on the image based on `GhostPixel` and `LedPixel`
- Displaying `LEDCenters`, `LEDBlobGray`, `GhostAverageGray` in the left list
- Exporting CSV

Unlike the old draft's "return a unified JSON structure," current Ghost results are primarily presented through database result models, image overlays, and list views.

## Most Common Mistakes to Avoid

### It Is Not an Independent Public API

Current Ghost detection clearly belongs to the ARVR template family, with entry point at `Templates/ARVR/Ghost`, not a generic `ghost-detection` library.

### The Algorithm Class Is Not a Local Computation Kernel

`AlgorithmGhost` currently primarily handles windows, input, templates, and message assembly. Writing it as a local algorithm implementation that directly processes `Mat` would not match the real code.

### The Parameter Surface Is Much Narrower Than the Old Draft

Current `GhostParam` exposes lattice radius, row/column counts, and grayscale ratio bounds, without the complete threshold/area/morphology table from old documentation.

### Result Display Depends on UI and Result Handlers

The real output chain is `ViewHandleGhost` + result DAO + image overlay, not a single call returning sample JSON.

## Recommended Reading Order

1. `Engine/ColorVision.Engine/Templates/ARVR/Ghost/TemplateGhost.cs`
2. `Engine/ColorVision.Engine/Templates/ARVR/Ghost/GhostParam.cs`
3. `Engine/ColorVision.Engine/Templates/ARVR/Ghost/AlgorithmGhost.cs`
4. `Engine/ColorVision.Engine/Templates/ARVR/Ghost/DisplayGhost.xaml.cs`
5. `Engine/ColorVision.Engine/Templates/ARVR/Ghost/ViewHandleGhost.cs`

## Continue Reading

- [ARVR Templates](../templates/arvr-template.md)
- [Algorithm System Overview](../overview.md)
- [ColorVision.Engine](../../engine-components/ColorVision.Engine.md)