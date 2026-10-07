using AvalonDock;
using AvalonDock.Layout;
using ColorVision.Solution.Themes;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.Tests;

public sealed class DockThemeCompatibilityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ModernThemeLoadsDocumentAndToolContent(bool isDark)
    {
        WpfTestHost.Invoke(() =>
        {
            var toolContent = new TextBlock { Text = "Tool content" };
            var documentContent = new TextBlock { Text = "Document content" };
            var docking = new DockingManager
            {
                Theme = new AvalonDockTheme(isDark),
                Layout = new LayoutRoot
                {
                    RootPanel = new LayoutPanel
                    {
                        Children =
                        {
                            new LayoutAnchorablePane { Children = { new LayoutAnchorable { Title = "Tool", ContentId = "tool", Content = toolContent } } },
                            new LayoutDocumentPane { Children = { new LayoutDocument { Title = "Document", ContentId = "document", Content = documentContent } } }
                        }
                    }
                }
            };
            var window = new Window { Content = docking, Width = 640, Height = 480, Opacity = 0, ShowInTaskbar = false, ShowActivated = false };
            try
            {
                window.Show();
                docking.UpdateLayout();
                Assert.True(toolContent.IsLoaded);
                Assert.True(documentContent.IsLoaded);
            }
            finally
            {
                window.Close();
            }
        });
    }
}
