using Spectrum.Configs;
using Spectrum.PropertyEditor;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;

namespace Spectrum.Tests;

public sealed class FilterWheelHoleMappingEditSessionTests
{
    [Fact]
    public void HoleMappingSelectsDedicatedEditor()
    {
        var property = typeof(FilterWheelConfig).GetProperty(nameof(FilterWheelConfig.HoleMapping))!;
        Assert.Equal(typeof(FilterWheelHoleMappingPropertiesEditor), property.GetCustomAttribute<PropertyEditorTypeAttribute>()!.EditorType);
    }

    [Fact]
    public void DiscardingEditedSessionLeavesOriginalCollectionAndRowsUnchanged()
    {
        var config = new FilterWheelConfig();
        var original = config.HoleMapping;
        var first = original[0];
        first.CalibrationGroupName = "ND reference";
        var session = new FilterWheelHoleMappingEditSession(original);

        session.Rows[0].IndexText = "12";
        session.Rows[0].Hole.HoleName = "Changed";
        session.Rows.RemoveAt(1);
        session.AddRow();

        Assert.Same(original, config.HoleMapping);
        Assert.Same(first, original[0]);
        Assert.Equal(5, original.Count);
        Assert.Equal(0, first.HoleIndex);
        Assert.Equal("ND0", first.HoleName);
        Assert.Equal("ND10", original[1].HoleName);
        Assert.Equal("ND reference", first.CalibrationGroupName);
    }

    [Fact]
    public void ValidatedCommitReplacesMappingWithDetachedRowsAndPreservesCalibration()
    {
        var config = new FilterWheelConfig();
        var original = config.HoleMapping;
        original[0].CalibrationGroupName = "  Legacy calibration  ";
        var session = new FilterWheelHoleMappingEditSession(original);
        session.Rows[0].IndexText = "12";
        session.Rows[0].Hole.HoleName = "ND8";
        session.Rows.RemoveAt(1);

        Assert.True(session.TryCreateMapping(out var result, out string error), error);
        Assert.Same(original, config.HoleMapping);
        config.HoleMapping = result!;

        Assert.NotSame(original, config.HoleMapping);
        Assert.Equal(new[] { 12, 2, 3, 4 }, result!.Select(h => h.HoleIndex));
        Assert.Equal("ND8", result[0].HoleName);
        Assert.Equal("  Legacy calibration  ", result[0].CalibrationGroupName);
        Assert.NotSame(session.Rows[0].Hole, result[0]);
        session.Rows[0].Hole.HoleName = "After commit";
        Assert.Equal("ND8", result[0].HoleName);
        Assert.Equal("ND0", original[0].HoleName);
    }

    [Fact]
    public void AddUsesAnUnusedIndexFromCurrentDraftAndCanReuseDeletedPosition()
    {
        var session = new FilterWheelHoleMappingEditSession(new FilterWheelConfig().HoleMapping);
        Assert.Equal("5", session.AddRow().IndexText);
        session.Rows[1].IndexText = "25";
        var added = session.AddRow();
        Assert.Equal("1", added.IndexText);
        Assert.Empty(added.Hole.HoleName);
        Assert.Empty(added.Hole.CalibrationGroupName);
        session.Rows.Remove(added);
        Assert.Equal("1", session.AddRow().IndexText);
    }

    [Fact]
    public void DuplicateNumericIndexIsRejectedUntilCorrected()
    {
        var session = new FilterWheelHoleMappingEditSession(new FilterWheelConfig().HoleMapping);
        session.Rows[1].IndexText = " +0 ";
        Assert.False(session.TryCreateMapping(out var result, out string error));
        Assert.Null(result);
        Assert.Contains("第 2 行", error);
        Assert.Contains("重复", error);
        session.Rows[1].IndexText = "6";
        Assert.True(session.TryCreateMapping(out result, out error), error);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("abc")]
    [InlineData("1.5")]
    [InlineData("2147483648")]
    [InlineData("-2147483649")]
    public void InvalidIndexNeverCommitsThePreviousNumericValue(string input)
    {
        var original = new FilterWheelConfig().HoleMapping;
        var session = new FilterWheelHoleMappingEditSession(original);
        session.Rows[0].IndexText = input;
        Assert.False(session.TryCreateMapping(out var result, out string error));
        Assert.Null(result);
        Assert.Contains("第 1 行", error);
        Assert.Contains("有效整数", error);
        Assert.Equal(0, original[0].HoleIndex);
    }

    [Fact]
    public void ExistingIntRangeAndBlankOrRepeatedNamesAreNotRestricted()
    {
        var original = new ObservableCollection<FilterWheelHoleMap>
        {
            new() { HoleIndex = int.MinValue, HoleName = "", CalibrationGroupName = "legacy" },
            new() { HoleIndex = int.MaxValue, HoleName = "" },
            new() { HoleIndex = 12, HoleName = "same" },
            new() { HoleIndex = 15, HoleName = "same" }
        };
        var session = new FilterWheelHoleMappingEditSession(original);
        Assert.True(session.TryCreateMapping(out var result, out string error), error);
        Assert.Equal(original.Select(h => h.HoleIndex), result!.Select(h => h.HoleIndex));
        Assert.Equal(original.Select(h => h.HoleName), result.Select(h => h.HoleName));
        Assert.Equal("legacy", result[0].CalibrationGroupName);
        Assert.Equal("0", session.AddRow().IndexText);
    }

    [Fact]
    public void EmptyMappingCanBeCommittedAndNullMappingCanBeEdited()
    {
        var session = new FilterWheelHoleMappingEditSession(null);
        Assert.True(session.TryCreateMapping(out var result, out string error), error);
        Assert.Empty(result!);
        Assert.Equal("0", session.AddRow().IndexText);
    }
}
