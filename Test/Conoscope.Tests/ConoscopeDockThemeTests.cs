using AvalonDock;
using AvalonDock.Layout;
using ColorVision.Themes;
using Conoscope.Presentation.Docking;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;

namespace Conoscope.Tests;

public sealed class ConoscopeDockThemeTests
{
    [Theory]
    [InlineData(Theme.Light)]
    [InlineData(Theme.Dark)]
    public void ThemeLoadsAndDisplaysWorkspaceContent(Theme theme)
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                var content = new TextBlock { Text = "Conoscope document" };
                var docking = new DockingManager
                {
                    Theme = ConoscopeDockTheme.Create(theme),
                    Layout = new LayoutRoot
                    {
                        RootPanel = new LayoutPanel
                        {
                            Children = { new LayoutDocumentPane { Children = { new LayoutDocument { Title = "Document", Content = content } } } }
                        }
                    }
                };
                var window = new Window { Content = docking, Width = 640, Height = 480, Opacity = 0, ShowInTaskbar = false, ShowActivated = false };
                try
                {
                    window.Show();
                    docking.UpdateLayout();
                    Assert.True(content.IsLoaded);
                }
                finally
                {
                    window.Close();
                }
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Conoscope docking theme did not finish loading.");
        if (failure != null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
