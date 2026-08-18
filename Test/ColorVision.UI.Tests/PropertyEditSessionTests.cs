using System.Collections.ObjectModel;

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
}
