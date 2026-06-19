using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;

namespace ColorVision.ImageEditor.Settings
{
    public partial class ImageViewWorkspaceSettingsView : UserControl
    {
        public ImageViewWorkspaceSettingsView(ImageView imageView)
        {
            ImageView = imageView;
            ImageOpens = BuildImageOpens(imageView);
            InitializeComponent();
            DataContext = this;
        }

        public ImageView ImageView { get; }

        public ImageViewConfig Config => ImageView.Config;

        public ObservableCollection<ImageOpenSupportViewModel> ImageOpens { get; }

        private static ObservableCollection<ImageOpenSupportViewModel> BuildImageOpens(ImageView imageView)
        {
            ObservableCollection<ImageOpenSupportViewModel> imageOpens = new();
            IEnumerable<ImageOpenSupportViewModel> groupedOpeners = imageView.IEditorToolFactory.IImageOpens
                .GroupBy(item => item.Value.GetType())
                .OrderBy(group => group.Key.Name)
                .Select(group => new ImageOpenSupportViewModel
                {
                    HandlerName = group.Key.Name,
                    Extensions = string.Join(", ", group.Select(item => item.Key).Distinct().OrderBy(item => item)),
                });

            foreach (ImageOpenSupportViewModel imageOpen in groupedOpeners)
            {
                imageOpens.Add(imageOpen);
            }

            return imageOpens;
        }
    }

    public class ImageOpenSupportViewModel
    {
        public string HandlerName { get; set; } = string.Empty;

        public string Extensions { get; set; } = string.Empty;
    }
}
