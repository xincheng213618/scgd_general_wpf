using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Cie;
using System.Windows.Controls;

namespace ColorVision.UI.Tests;

public sealed class CieWindowCompositionTests
{
    [Fact]
    public void CieWindowHostsDiagramAndColorGamutCalculationTabs()
    {
        WpfTestHost.Invoke(() =>
        {
            WindowCIE window = new();
            try
            {
                TabControl tabs = Assert.IsType<TabControl>(window.FindName("CieTabs"));
                Assert.Equal(2, tabs.Items.Count);
                Assert.IsType<TabItem>(tabs.Items[0]);
                TabItem calculationTab = Assert.IsType<TabItem>(tabs.Items[1]);
                Assert.IsType<ManualColorGamutView>(calculationTab.Content);
            }
            finally
            {
                window.Close();
            }
        });
    }
}
