using System.ComponentModel;
using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing.Editor;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.PropertyEditor;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.PhyCameras.Group;
using FlowEngineLib;
using System.Reflection;

namespace ColorVision.UI.Tests;

public sealed class CameraCalibrationGainTests
{
    public static TheoryData<Type> DirectGainCameraNodeTypes => new()
    {
        typeof(CVCameraNode),
        typeof(LVCameraNode),
        typeof(LocalCameraNode)
    };

    [Fact]
    public void SelectedCalibrationAppliesMatchingGroupGainAndLocksEditor()
    {
        StaTest.Run(() =>
        {
            CalibrationParam calibration = new() { Id = 42, CalibrationMode = "calibration1" };
            GroupResource other = CreateGroup("other", 12);
            GroupResource selected = CreateGroup("calibration1", 53);
            DisplayCameraConfig displayConfig = new() { Gain = 17 };

            CalibrationGroupGainResolver.Synchronize(displayConfig, calibration, [other, selected]);

            Assert.Equal(53f, displayConfig.Gain);
            Assert.True(displayConfig.IsGainControlledByCalibrationGroup);
            Assert.Contains("校正组“calibration1”", displayConfig.GainSourceHint);
        });
    }

    [Fact]
    public void EmptyCalibrationPreservesGainAndUnlocksEditor()
    {
        StaTest.Run(() =>
        {
            CalibrationParam calibration = new() { Id = -1, CalibrationMode = "calibration1" };
            DisplayCameraConfig displayConfig = new()
            {
                Gain = 17,
                IsGainControlledByCalibrationGroup = true,
                GainSourceHint = "old"
            };

            CalibrationGroupGainResolver.Synchronize(displayConfig, calibration, [CreateGroup("calibration1", 53)]);

            Assert.Equal(17f, displayConfig.Gain);
            Assert.False(displayConfig.IsGainControlledByCalibrationGroup);
            Assert.Empty(displayConfig.GainSourceHint);
        });
    }

    [Theory]
    [MemberData(nameof(DirectGainCameraNodeTypes))]
    public void DirectGainCameraNodesUseCalibrationAwareEditor(Type nodeType)
    {
        PropertyInfo? gainProperty = nodeType.GetProperty("Gain");

        Assert.NotNull(gainProperty);
        Assert.Equal(typeof(CameraCalibrationGainPropertiesEditor), FlowNodePropertyMetadataProvider.Instance.GetEditorType(gainProperty));
    }

    [Theory]
    [MemberData(nameof(DirectGainCameraNodeTypes))]
    public void DirectGainCameraNodesApplyCalibrationGroupGain(Type nodeType)
    {
        StaTest.Run(() =>
        {
            object node = Activator.CreateInstance(nodeType)!;
            PropertyInfo? gainProperty = nodeType.GetProperty("Gain");
            Assert.NotNull(gainProperty);
            gainProperty.SetValue(node, 17f);
            CalibrationParam calibration = new() { Id = 42, CalibrationMode = "calibration1" };

            bool isControlled = CameraCalibrationGainPropertiesEditor.Synchronize(
                gainProperty,
                node,
                calibration,
                [CreateGroup("calibration1", 53)],
                out string hint);

            Assert.True(isControlled);
            Assert.Equal(53f, gainProperty.GetValue(node));
            Assert.Contains("校正组“calibration1”", hint);
        });
    }

    [Fact]
    public void ClearedNodeCalibrationPreservesGainAndUnlocksEditor()
    {
        StaTest.Run(() =>
        {
            LVCameraNode node = new() { Gain = 17 };
            PropertyInfo gainProperty = typeof(LVCameraNode).GetProperty(nameof(LVCameraNode.Gain))!;

            bool isControlled = CameraCalibrationGainPropertiesEditor.Synchronize(
                gainProperty,
                node,
                null,
                [CreateGroup("calibration1", 53)],
                out string hint);

            Assert.False(isControlled);
            Assert.Equal(17f, node.Gain);
            Assert.Empty(hint);
        });
    }

    private static GroupResource CreateGroup(string name, int gain) => new(new SysResourceModel
    {
        Name = name,
        Value = $"{{\"Gain\":{gain}}}"
    });
}
