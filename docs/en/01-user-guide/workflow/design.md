# Workflow Design

The focus of the workflow design page is not to abstractly introduce "what is a visual editor," but to help you build a workflow, save it, and quickly return to a runnable state after making changes.

## What You Can Directly Do in the Current Designer

From the current implementation, the workflow designer supports at least these common actions:

- Create new workflow
- Open existing workflow template
- Save workflow
- Delete workflow
- Import workflow
- Export workflow
- Import module
- Auto layout
- Auto fit canvas
- Undo and redo
- Edit node-related content via the property panel

## Common Design Order

1. First create or open a workflow.
2. Place the needed nodes on the canvas.
3. Connect node relationships and fill in key parameters in the property panel.
4. Save once first, then do auto layout or fine-tune positions.
5. After significant changes, save again and go to [Workflow Execution & Debugging](./execution.md) for verification.

## Most Commonly Used Actions During Design

### Create, Open, and Save

The most common workflow design problem is not "can't drag nodes," but having made changes for a long time without saving to the correct template. During design, prioritize confirming which workflow you are currently editing before saving.

### Auto Layout and Auto Fit

When nodes become more numerous and the canvas becomes increasingly messy, first use auto layout and auto fit to pull the structure back to a readable state, then continue adjusting parameters. This is usually much more stable than purely manual dragging.

### Undo and Redo

The current implementation has explicit undo and redo stacks. When making major changes, don't be afraid to experiment first; but if you find the direction is wrong after experimenting, undo quickly rather than continuing to patch on top of an incorrect state.

### Import, Export, and Module Reuse

If you are not building a workflow from scratch, prioritize checking whether there are reusable workflows or modules. Importing existing content and then making small modifications is usually faster than completely rebuilding by hand and less likely to miss parameters.

## Key Points to Note During Design

### Content on Canvas Does Not Equal Saved Content

What you see in the designer, if not yet saved, is not necessarily the version that will actually run during execution. Always save before execution.

### Selected Workflow Template Must Match Editing Target

The current implementation maintains the currently selected workflow template. If the selected object is not what you think it is, subsequent save, delete, and export operations will land on the wrong object.

### Confirm Parameters in Property Panel First

After connecting nodes, do not directly assume parameters are also correct. Many problems are not due to incorrect workflow structure, but because node parameters still use old template values.

## Common Issues

### Workflow Can Be Opened but Canvas is Empty

- First confirm whether the currently selected template is correct
- Then confirm whether that template actually has workflow data
- If the template itself has no content yet, first create the corresponding workflow before continuing

### Changed Workflow but Execution Results Seem Unchanged

- First confirm whether it has been saved
- Then confirm whether the execution page has selected the same workflow template
- If modules or workflows were imported, check whether the wrong object was modified

### Canvas Too Messy, Relationships Unclear

- First use auto layout or auto fit
- Then rename or reorganize key nodes
- Do not enter execution troubleshooting when the structure is unclear

### Unsure Whether Problem is in Design or Execution

- If node relationships and parameters are not yet confirmed, stay on the design page to organize first
- If the structure is already stable, go to [Workflow Execution & Debugging](./execution.md) to locate runtime issues

## Continue Reading

- [Workflow Execution & Debugging](./execution.md)
- [Device Service Overview](../devices/overview.md)
- [Property Editor](../interface/property-editor.md)
- [Log Viewer](../interface/log-viewer.md)

## Notes

- This page only retains the usage entry point for workflow design and no longer maintains generalized workflow editor introductions.
- Related implementations are primarily located in `Engine/ColorVision.Engine/Templates/Flow/ViewFlow.xaml.cs`.