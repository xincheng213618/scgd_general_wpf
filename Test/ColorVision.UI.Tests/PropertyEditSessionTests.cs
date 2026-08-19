using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using ColorVision.UI.Sorts;

namespace ColorVision.UI.Tests;

public class PropertyEditSessionTests
{
    private sealed class NestedConfig
    {
        public string Name { get; set; } = "Nested";
        public ObservableCollection<int> Values { get; set; } = new() { 1, 2 };
    }

    private sealed class RootConfig
    {
        public string Name { get; set; } = "Original";
        public NestedConfig Nested { get; set; } = new();
        public List<NestedConfig> Items { get; set; } = new() { new NestedConfig { Name = "Item" } };
        public Dictionary<string, NestedConfig> Map { get; set; } = new()
        {
            ["first"] = new NestedConfig { Name = "Mapped" }
        };
    }

    private sealed class GridColumnConfig
    {
        public ObservableCollection<GridViewColumnVisibility> Columns { get; set; } = new();
    }

    [Fact]
    public void TransactionalSession_DoesNotMutateSourceBeforeCommit()
    {
        var source = new RootConfig();
        PropertyEditSession session = PropertyEditSession.Create(source, PropertyEditorEditMode.Transactional);
        var editable = Assert.IsType<RootConfig>(session.EditableObject);

        editable.Name = "Changed";
        editable.Nested.Name = "Changed nested";
        editable.Nested.Values.Add(3);
        editable.Items[0].Name = "Changed item";
        editable.Map["first"].Name = "Changed map";

        Assert.Equal("Original", source.Name);
        Assert.Equal("Nested", source.Nested.Name);
        Assert.Equal(new[] { 1, 2 }, source.Nested.Values);
        Assert.Equal("Item", source.Items[0].Name);
        Assert.Equal("Mapped", source.Map["first"].Name);
    }

    [Fact]
    public void TransactionalSession_CommitCopiesNestedGraphWithoutSharingReferences()
    {
        var source = new RootConfig();
        PropertyEditSession session = PropertyEditSession.Create(source, PropertyEditorEditMode.Transactional);
        var editable = Assert.IsType<RootConfig>(session.EditableObject);
        editable.Nested.Name = "Committed";
        editable.Items[0].Name = "Committed item";

        session.Commit();

        Assert.Equal("Committed", source.Nested.Name);
        Assert.Equal("Committed item", source.Items[0].Name);
        Assert.NotSame(editable.Nested, source.Nested);
        Assert.NotSame(editable.Items, source.Items);
        Assert.NotSame(editable.Items[0], source.Items[0]);
    }

    [Fact]
    public void TransactionalSession_ResetRestoresInitialWorkingValues()
    {
        var source = new RootConfig();
        PropertyEditSession session = PropertyEditSession.Create(source, PropertyEditorEditMode.Transactional);
        var editable = Assert.IsType<RootConfig>(session.EditableObject);
        editable.Nested.Name = "Changed";
        editable.Items.Clear();

        session.Reset();

        Assert.Equal("Nested", editable.Nested.Name);
        Assert.Single(editable.Items);
        Assert.Equal("Item", editable.Items[0].Name);
        Assert.Equal("Original", source.Name);
    }

    [Fact]
    public void ImmediateSession_PreservesLegacyWriteThroughBehavior()
    {
        var source = new RootConfig();
        PropertyEditSession session = PropertyEditSession.Create(source, PropertyEditorEditMode.Immediate);

        Assert.Same(source, session.EditableObject);
        Assert.False(session.IsTransactional);
    }

    [Fact]
    public void ImmediateSession_ResetPreservesWpfRuntimeReferences()
    {
        WpfTestHost.Invoke(() =>
        {
            GridViewColumn column = CreateTemplatedGridViewColumn();
            var source = new GridColumnConfig
            {
                Columns = new ObservableCollection<GridViewColumnVisibility>
                {
                    new() { ColumnName = "Value", GridViewColumn = column, IsVisible = true }
                }
            };

            PropertyEditSession session = PropertyEditSession.Create(source, PropertyEditorEditMode.Immediate);
            source.Columns[0].IsVisible = false;

            session.Reset();

            Assert.True(source.Columns[0].IsVisible);
            Assert.Same(column, source.Columns[0].GridViewColumn);
        });
    }

    [Fact]
    public void TransactionalSession_ClonesConfigDataButPreservesWpfRuntimeReferences()
    {
        WpfTestHost.Invoke(() =>
        {
            GridViewColumn column = CreateTemplatedGridViewColumn();
            var source = new GridColumnConfig
            {
                Columns = new ObservableCollection<GridViewColumnVisibility>
                {
                    new() { ColumnName = "Value", GridViewColumn = column, IsVisible = true }
                }
            };

            PropertyEditSession session = PropertyEditSession.Create(source, PropertyEditorEditMode.Transactional);
            var editable = Assert.IsType<GridColumnConfig>(session.EditableObject);

            Assert.NotSame(source.Columns, editable.Columns);
            Assert.NotSame(source.Columns[0], editable.Columns[0]);
            Assert.Same(column, editable.Columns[0].GridViewColumn);

            editable.Columns[0].IsVisible = false;
            Assert.True(source.Columns[0].IsVisible);

            session.Commit();

            Assert.False(source.Columns[0].IsVisible);
            Assert.Same(column, source.Columns[0].GridViewColumn);
        });
    }

    private static GridViewColumn CreateTemplatedGridViewColumn()
    {
        const string xaml = """
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <TextBlock Text="{Binding}" />
            </DataTemplate>
            """;
        return new GridViewColumn { CellTemplate = (DataTemplate)XamlReader.Parse(xaml) };
    }
}
