using ColorVision.UI;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.ImageEditor.Settings
{
    public partial class ImageViewWorkspaceSettingsView : UserControl
    {
        private readonly EditorToolVisibilityConfig _visibilityConfig;

        public ImageViewWorkspaceSettingsView(ImageView imageView)
        {
            ImageView = imageView;
            _visibilityConfig = ConfigService.Instance.GetRequiredService<EditorToolVisibilityConfig>();
            EditorTools = BuildEditorTools(imageView, _visibilityConfig);
            ImageOpens = BuildImageOpens(imageView);
            InitializeComponent();
            DataContext = this;
        }

        public ImageView ImageView { get; }

        public ImageViewConfig Config => ImageView.Config;

        public ObservableCollection<EditorToolViewModel> EditorTools { get; }

        public ObservableCollection<ImageOpenSupportViewModel> ImageOpens { get; }

        private static ObservableCollection<EditorToolViewModel> BuildEditorTools(ImageView imageView, EditorToolVisibilityConfig visibilityConfig)
        {
            ObservableCollection<EditorToolViewModel> editorTools = new();
            foreach (IEditorTool tool in imageView.ImageViewModel.IEditorToolFactory.IEditorTools.OrderBy(t => t.ToolBarLocal).ThenBy(t => t.Order))
            {
                string guidId = tool.GuidId ?? tool.GetType().Name;
                EditorToolViewModel toolViewModel = new(imageView, tool, visibilityConfig)
                {
                    DisplayName = guidId,
                    Location = tool.ToolBarLocal.ToString(),
                    TypeName = tool.GetType().FullName ?? tool.GetType().Name,
                    IsVisible = visibilityConfig.GetToolVisibility(guidId)
                };
                editorTools.Add(toolViewModel);
            }

            return editorTools;
        }

        private static ObservableCollection<ImageOpenSupportViewModel> BuildImageOpens(ImageView imageView)
        {
            ObservableCollection<ImageOpenSupportViewModel> imageOpens = new();
            IEnumerable<ImageOpenSupportViewModel> groupedOpeners = imageView.ImageViewModel.IEditorToolFactory.IImageOpens
                .GroupBy(item => item.Value.GetType())
                .OrderBy(group => group.Key.Name)
                .Select(group => new ImageOpenSupportViewModel
                {
                    HandlerName = group.Key.Name,
                    TypeName = group.Key.FullName ?? group.Key.Name,
                    Extensions = string.Join(", ", group.Select(item => item.Key).Distinct().OrderBy(item => item)),
                });

            foreach (ImageOpenSupportViewModel imageOpen in groupedOpeners)
            {
                imageOpens.Add(imageOpen);
            }

            return imageOpens;
        }

        private void ShowAll_Click(object sender, RoutedEventArgs e)
        {
            Config.IsToolBarAlVisible = true;
            Config.IsToolBarDrawVisible = true;
            Config.IsToolBarTopVisible = true;
            Config.IsToolBarLeftVisible = true;
            Config.IsToolBarRightVisible = true;

            foreach (EditorToolViewModel tool in EditorTools)
            {
                tool.IsVisible = true;
            }
        }

        private void HideAll_Click(object sender, RoutedEventArgs e)
        {
            Config.IsToolBarAlVisible = false;
            Config.IsToolBarDrawVisible = false;
            Config.IsToolBarTopVisible = false;
            Config.IsToolBarLeftVisible = false;
            Config.IsToolBarRightVisible = false;

            foreach (EditorToolViewModel tool in EditorTools)
            {
                tool.IsVisible = false;
            }
        }
    }

    public class EditorToolViewModel : Common.MVVM.ViewModelBase
    {
        private readonly ImageView _imageView;
        private readonly EditorToolVisibilityConfig _visibilityConfig;

        public EditorToolViewModel(ImageView imageView, IEditorTool tool, EditorToolVisibilityConfig visibilityConfig)
        {
            _imageView = imageView;
            Tool = tool;
            _visibilityConfig = visibilityConfig;
        }

        public IEditorTool Tool { get; }

        public string DisplayName { get; set; } = string.Empty;

        public string Location { get; set; } = string.Empty;

        public string TypeName { get; set; } = string.Empty;

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                _isVisible = value;
                OnPropertyChanged();
                UpdateToolVisibility();
            }
        }
        private bool _isVisible = true;

        private void UpdateToolVisibility()
        {
            string guidId = Tool.GuidId ?? Tool.GetType().Name;
            _visibilityConfig.SetToolVisibility(guidId, _isVisible);

            if (_imageView.ImageViewModel.IEditorToolFactory.ToolUIElements.TryGetValue(guidId, out FrameworkElement? uiElement))
            {
                uiElement.Visibility = _isVisible ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }

    public class ImageOpenSupportViewModel
    {
        public string HandlerName { get; set; } = string.Empty;

        public string TypeName { get; set; } = string.Empty;

        public string Extensions { get; set; } = string.Empty;
    }
}