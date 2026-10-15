using AvalonDock;
using AvalonDock.Core;
using AvalonDock.Layout;
using AvalonDock.Serializer.Xml;
using System.IO;
using System.Windows.Controls;

namespace ColorVision.UI.Tests;

public sealed class DockLayoutSerializationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LegacyLayoutRestoresRegisteredContentAndPreservesUnresolvedContentPolicy(bool hideUnresolvedTools)
    {
        WpfTestHost.Invoke(() =>
        {
            // AvalonDock v4 persisted capitalized booleans and the layout model directly.
            const string legacyLayout = """
                <LayoutRoot>
                  <RootPanel Orientation="Horizontal">
                    <LayoutAnchorablePane DockWidth="250">
                      <LayoutAnchorable Title="Registered tool" ContentId="tool" CanClose="False" CanHide="True" />
                      <LayoutAnchorable Title="Unresolved tool" ContentId="missing-tool" />
                    </LayoutAnchorablePane>
                    <LayoutDocumentPane>
                      <LayoutDocument Title="Registered document" ContentId="document" CanClose="False" />
                      <LayoutDocument Title="Unresolved document" ContentId="missing-document" />
                    </LayoutDocumentPane>
                  </RootPanel>
                </LayoutRoot>
                """;
            var toolContent = new UserControl();
            var documentContent = new UserControl();
            var docking = new DockingManager();
            var serializer = new XmlLayoutSerializer(docking)
            {
                UnresolvedContentHandling = hideUnresolvedTools ? UnresolvedContentHandling.Hide : UnresolvedContentHandling.Remove
            };
            serializer.LayoutSerializationCallback += (_, args) =>
            {
                args.Content = args.Model.ContentId switch
                {
                    "tool" => toolContent,
                    "document" => documentContent,
                    _ => null
                };
                if (args.Content == null && !hideUnresolvedTools)
                    args.Cancel = true;
            };

            serializer.Deserialize(new StringReader(legacyLayout));
            AssertRestoredContent();

            using var serialized = new StringWriter();
            serializer.Serialize(serialized);
            serializer.Deserialize(new StringReader(serialized.ToString()));
            AssertRestoredContent();

            void AssertRestoredContent()
            {
                var tool = Assert.Single(docking.Layout.Descendents().OfType<LayoutAnchorable>(), item => item.ContentId == "tool");
                Assert.Same(toolContent, tool.Content);
                Assert.False(tool.CanClose);
                var document = Assert.Single(docking.Layout.Descendents().OfType<LayoutDocument>());
                Assert.Equal("document", document.ContentId);
                Assert.Same(documentContent, document.Content);
                Assert.False(document.CanClose);
                Assert.Equal(hideUnresolvedTools ? 1 : 0, docking.Layout.Hidden.Count);
                if (hideUnresolvedTools)
                    Assert.Equal("missing-tool", Assert.Single(docking.Layout.Hidden).ContentId);
                else
                    Assert.DoesNotContain(docking.Layout.Descendents().OfType<LayoutContent>(), item => item.ContentId == "missing-tool");
            }
        });
    }
}
