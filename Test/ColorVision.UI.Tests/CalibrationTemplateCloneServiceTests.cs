using ColorVision.Engine.Services.PhyCameras.Group;

namespace ColorVision.UI.Tests;

public class CalibrationTemplateCloneServiceTests
{
    [Fact]
    public void MapSlot_UsesTargetCameraResource()
    {
        CalibrationTemplateSlotMapping mapping = CalibrationTemplateCloneService.MapSlot(
            isSelected: true,
            targetResourceName: "new-camera-luminance",
            targetResourceId: 42);

        Assert.Equal("new-camera-luminance", mapping.FilePath);
        Assert.Equal(42, mapping.Id);
        Assert.False(mapping.NeedsConfiguration);
    }

    [Fact]
    public void MapSlot_DoesNotRetainSourceReferenceWhenTargetResourceIsMissing()
    {
        CalibrationTemplateSlotMapping mapping = CalibrationTemplateCloneService.MapSlot(
            isSelected: true,
            targetResourceName: null,
            targetResourceId: 0);

        Assert.Equal(string.Empty, mapping.FilePath);
        Assert.Equal(0, mapping.Id);
        Assert.True(mapping.NeedsConfiguration);
    }

    [Fact]
    public void MapSlot_DoesNotFlagUnselectedMissingResource()
    {
        CalibrationTemplateSlotMapping mapping = CalibrationTemplateCloneService.MapSlot(
            isSelected: false,
            targetResourceName: null,
            targetResourceId: 0);

        Assert.Equal(string.Empty, mapping.FilePath);
        Assert.Equal(0, mapping.Id);
        Assert.False(mapping.NeedsConfiguration);
    }

    [Fact]
    public void MapSlot_KeepsTargetReferenceButFlagsMissingTargetFile()
    {
        CalibrationTemplateSlotMapping mapping = CalibrationTemplateCloneService.MapSlot(
            isSelected: true,
            targetResourceName: "target-resource",
            targetResourceId: 55,
            targetResourceIsValid: false);

        Assert.Equal("target-resource", mapping.FilePath);
        Assert.Equal(55, mapping.Id);
        Assert.True(mapping.NeedsConfiguration);
    }
}
