using ColorVision.ImageEditor;
using ColorVision.ImageEditor.EditorTools.AppCommand;
using ColorVision.ImageEditor.Draw;
using System.IO;
using System.Windows.Input;

namespace ColorVision.UI.Tests;

public sealed class ImageFileContextMenuTests
{
    [Fact]
    public void FileLocationFollowsCurrentPathAndIsUnavailableForMemoryOnlyOrRemovedFiles()
    {
        string path = Path.GetTempFileName();
        try
        {
            StaTest.Run(() =>
            {
                ImageViewConfig config = new();
                DrawEditorContext drawing = new(new DrawCanvas(), new Zoombox());
                var menu = new ZoomEditorToolContextMenu(config, drawing).GetContextMenuItems();
                ICommand select = Assert.Single(menu, item => item.GuidId == "SelectImageFileInFolder").Command!;
                Assert.False(select.CanExecute(null));
                config.FilePath = path;
                Assert.True(select.CanExecute(null));
                config.FilePath = path + ".missing";
                Assert.False(select.CanExecute(null));
                Assert.Same(ApplicationCommands.Open, Assert.Single(menu, item => item.GuidId == "OpenImage").Command);
            });
        }
        finally { File.Delete(path); }
    }
}
