using System.Windows.Media;
using System.Windows.Controls;
using ColorVision.Themes;
using System.Windows;
using ColorVision.Services.Algorithm;

namespace ColorVision.Solution.V.Folders
{
    public class HistoryFolder : IFolder
    {
        public string Name { get; set; }
        public string ToolTip { get; set; }
        public ContextMenu ContextMenu { get; set; }
        public ImageSource Icon { get; set; }

        public HistoryFolder(string Name)
        {
            this.Name = Name;
            if (Application.Current.TryFindResource("HistoryDrawingImage") is DrawingImage drawingImage)
                Icon = drawingImage;

            ThemeManager.Current.CurrentUIThemeChanged += (s) =>
            {
                if (Application.Current.TryFindResource("HistoryDrawingImage") is DrawingImage drawingImage)
                    Icon = drawingImage;
            };
            ContextMenu= new ContextMenu();
        }



        public void Open()
        {
            WindowSolution windowSolution = new WindowSolution();
            windowSolution.Show();
        }

        public void Copy()
        {
            throw new System.NotImplementedException();
        }

        public void ReName()
        {
            throw new System.NotImplementedException();
        }

        public void Delete()
        {
            throw new System.NotImplementedException();
        }
    }
}
