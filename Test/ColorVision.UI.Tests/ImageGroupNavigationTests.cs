using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Navigation;

namespace ColorVision.UI.Tests;

public sealed class ImageGroupNavigationTests
{
    [Fact]
    public void OpenGroupKeepsFirstPathMetadataAndClampsSelectionAfterNormalization()
    {
        List<string> opened = new();
        ImageGroupNavigation navigation = new(opened.Add);
        object tag = new();
        ImageViewImageItem first = new("a.tif", "Original label", tag);
        ImageViewImageItem last = new("b.tif");

        Assert.True(navigation.TryOpenGroup([first, new("A.TIF", "Duplicate"), new(" "), last], 99));

        Assert.Equal(2, navigation.Items.Count);
        Assert.Same(first, navigation.Items[0]);
        Assert.Same(tag, navigation.Items[0].Tag);
        Assert.Equal(1, navigation.SelectedIndex);
        Assert.Equal(new[] { "b.tif" }, opened);
    }

    [Fact]
    public void BrowsingAnOlderImagePausesFollowingUntilTheNewestImageIsSelected()
    {
        List<string> opened = new();
        ImageGroupNavigation navigation = new(opened.Add);
        navigation.TryOpenGroup([new("a.tif"), new("b.tif")], 1);
        navigation.Select(0);
        opened.Clear();

        navigation.Append("c.tif", true);
        Assert.Empty(opened);
        Assert.Equal(0, navigation.SelectedIndex);
        Assert.Equal(3, navigation.Items.Count);

        navigation.Select(2);
        navigation.Append("d.tif", true);
        Assert.Equal(new[] { "c.tif", "d.tif" }, opened);
        Assert.Equal(3, navigation.SelectedIndex);

        navigation.AutoFollow = false;
        navigation.Append("e.tif", true);
        Assert.Equal(3, navigation.SelectedIndex);
        Assert.Equal(2, opened.Count);
    }

    [Fact]
    public void AppendingAnExistingPathCanSelectItEvenWhenAutomaticFollowingIsDisabled()
    {
        List<string> opened = new();
        List<bool> selections = new();
        ImageGroupNavigation navigation = new(opened.Add) { AutoFollow = false };
        navigation.TryOpenGroup([new("a.tif"), new("b.tif")], 1);
        navigation.SelectedImageChanged += (_, change) => selections.Add(change.UserInitiated);
        opened.Clear();

        navigation.Append("A.TIF", true);
        navigation.Append("B.TIF", false);

        Assert.Equal(2, navigation.Items.Count);
        Assert.Equal(0, navigation.SelectedIndex);
        Assert.Equal(new[] { "a.tif" }, opened);
        Assert.Equal(new[] { true }, selections);
    }

    [Fact]
    public void SelectionRefreshesNavigationBeforeOpeningAndRaisesSelectionAfterOpening()
    {
        List<string> trace = new();
        ImageGroupNavigation navigation = new(path => trace.Add($"open:{path}"));
        navigation.Changed += (_, _) => trace.Add($"navigation:{navigation.SelectedIndex}");
        navigation.SelectedImageChanged += (_, change) => trace.Add($"selected:{change.Index}:{change.UserInitiated}");

        navigation.TryOpenGroup([new("a.tif"), new("b.tif")], 0);
        navigation.Select(1);

        Assert.Equal(new[]
        {
            "navigation:0", "open:a.tif", "selected:0:False",
            "navigation:1", "open:b.tif", "selected:1:True",
        }, trace);
    }

    [Fact]
    public void EmptyInputDefersToHostClearAndClearingReleasesTheManualSelection()
    {
        List<string> opened = new();
        ImageGroupNavigation navigation = new(opened.Add);
        navigation.TryOpenGroup([new("a.tif"), new("b.tif")], 1);
        navigation.Select(0);
        opened.Clear();

        Assert.False(navigation.TryOpenGroup([new(" ")], 0));
        Assert.Equal(2, navigation.Items.Count);
        Assert.Empty(opened);

        navigation.Clear();
        Assert.Empty(navigation.Items);
        Assert.Equal(-1, navigation.SelectedIndex);
        navigation.Append("c.tif", true);
        Assert.Equal(new[] { "c.tif" }, opened);
        Assert.Equal(0, navigation.SelectedIndex);
    }

    [Fact]
    public void SettingTheSameSinglePathKeepsItsMetadataWithoutOpeningOrSelecting()
    {
        List<string> opened = new();
        ImageGroupNavigation navigation = new(opened.Add);
        ImageViewImageItem item = new("a.tif", "User label", new object());
        navigation.TryOpenGroup([item], 0);
        opened.Clear();
        int selectionCount = 0;
        navigation.SelectedImageChanged += (_, _) => selectionCount++;

        navigation.SetSingle("A.TIF");
        Assert.Same(item, Assert.Single(navigation.Items));
        navigation.SetSingle("b.tif");

        Assert.Equal("b.tif", Assert.Single(navigation.Items).FilePath);
        Assert.Equal(0, navigation.SelectedIndex);
        Assert.Empty(opened);
        Assert.Equal(0, selectionCount);
    }

    [Fact]
    public void AppendingUsesTheWholePathAndRetiresIndicesAcrossGroupChanges()
    {
        List<string> opened = new();
        ImageGroupNavigation navigation = new(opened.Add);
        ImageViewImageItem first = new(@"C:\Station1\Frame.tif", "First camera");
        ImageViewImageItem second = new(@"C:\Station2\Frame.tif", "Second camera");
        ImageViewImageItem[] additional = Enumerable.Range(1, 70).Select(i => new ImageViewImageItem($@"C:\Archive\Frame-{i}.tif")).ToArray();
        ImageViewImageItem[] group = [first, second, .. additional];
        navigation.TryOpenGroup(group, 0);

        navigation.Append(@"c:\station2\FRAME.TIF", true);
        Assert.Equal(group, navigation.Items);
        Assert.Equal(second.FilePath, opened[^1]);
        Assert.Equal(1, navigation.SelectedIndex);

        navigation.TryOpenGroup([second], 0);
        navigation.Append(first.FilePath, false);
        Assert.Equal(new[] { second.FilePath, first.FilePath }, navigation.Items.Select(item => item.FilePath));
        foreach (ImageViewImageItem image in additional) navigation.Append(image.FilePath, false);
        navigation.Append(@"c:\archive\FRAME-70.TIF", true);
        Assert.Equal(group.Length, navigation.Items.Count);
        Assert.Equal(additional[^1].FilePath, opened[^1]);

        navigation.SetSingle(first.FilePath);
        navigation.Append(second.FilePath, false);
        Assert.Equal(new[] { first.FilePath, second.FilePath }, navigation.Items.Select(item => item.FilePath));

        navigation.Clear();
        navigation.Append(first.FilePath, false);
        Assert.Equal(first.FilePath, Assert.Single(navigation.Items).FilePath);
    }
}
