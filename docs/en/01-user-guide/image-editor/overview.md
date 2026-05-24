# Image Editor Overview

The image editor is one of the most commonly used result viewing and interaction areas in ColorVision. Here you can view images, zoom and pan, draw ROIs, create annotations, play videos, and switch to 3D or pseudo-color views in specific scenarios.

## What You Can Do Here

- View common image results
- Zoom, pan, and fit the view
- Draw rectangles, circles, polygons, curves, and text annotations
- View the property panel and adjust selected objects
- Use pseudo-color, histogram, or 3D views to observe data
- In video file scenarios, directly play, pause, scrub, and switch preview zoom

## Common Usage Order

1. First open an image or result window.
2. Use zoom and pan to confirm key areas.
3. Switch to drawing tools when you need to measure or mark.
4. Open the property panel when you need to view object parameters.
5. If the input is video, prioritize confirming smooth playback before further analysis.

## What Happens in Video Mode

When a supported video file is opened, the image editor switches to video work mode. At this point you typically see:

- Play and pause controls
- Progress bar
- Speed switching
- Preview zoom switching
- Mute or auto-hide toolbar related options

For high-resolution video, it is usually more practical to lower preview zoom first rather than pursuing original resolution from the start.

## Which Scenarios Are Especially Common

### Viewing Detection Results

- First confirm whether the original image and result overlay correspond
- Then check whether ROIs, annotations, and properties are correct

### Adjusting Image Display

- Prioritize using zoom, pseudo-color, and comparative observation
- Do not immediately modify underlying configuration; first confirm whether it's a display issue

### Checking Video or Dynamic Data

- First verify whether playback is normal
- Then decide whether to lower preview ratio or disable some additional displays

## Common Issues

### Image Display Normal but Operation Feels Laggy

- First shrink the view or reduce simultaneously displayed content
- In video scenarios, first lower preview zoom
- In 3D view scenarios, first lower target resolution or switch back to normal view to confirm the problem scope

### Annotations or ROIs Difficult to Select

- First confirm whether you are still in drawing mode
- Try returning to selection state before clicking objects
- If necessary, use [Property Editor](../interface/property-editor.md) to view the currently selected object

### Video Unsynced or Not Smooth

- First pause and resume playback
- Then try dragging the progress bar to jump
- For high-resolution video, prioritize lowering preview zoom and recheck

### 3D View Effects Abnormal After Opening

- First confirm whether the input data itself is suitable for height or intensity visualization
- Then check whether target resolution needs to be lowered or color mapping switched

## Continue Reading

- [Property Editor](../interface/property-editor.md)
- [Log Viewer](../interface/log-viewer.md)
- [Workflow Execution & Debugging](../workflow/execution.md)

## Notes

- This page only retains the usage perspective of the image editor and no longer maintains source code breakdown and performance appendices.
- Related implementations are primarily located in `UI/ColorVision.ImageEditor/`.