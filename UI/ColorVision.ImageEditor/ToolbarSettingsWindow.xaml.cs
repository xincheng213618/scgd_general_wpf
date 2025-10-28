using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace ColorVision.ImageEditor
{
    /// <summary>
    /// ToolbarSettingsWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ToolbarSettingsWindow : Window
    {
        public ImageView ImageView { get; set; }
        public ObservableCollection<EditorToolViewModel> EditorTools { get; set; }
        private EditorToolVisibilityConfig _visibilityConfig;

        public ToolbarSettingsWindow(ImageView imageView)
        {
            InitializeComponent();
            ImageView = imageView;
            DataContext = imageView.ImageViewModel;

            // Get or create visibility config
            _visibilityConfig = imageView.Config.GetRequiredService<EditorToolVisibilityConfig>();

            // Initialize editor tools visibility list
            EditorTools = new ObservableCollection<EditorToolViewModel>();
            foreach (var tool in imageView.ImageViewModel.IEditorToolFactory.IEditorTools.OrderBy(t => t.ToolBarLocal).ThenBy(t => t.Order))
            {
                var guidId = tool.GuidId ?? tool.GetType().Name;
                var toolViewModel = new EditorToolViewModel(imageView, tool, _visibilityConfig)
                {
                    DisplayName = guidId,
                    Location = tool.ToolBarLocal.ToString(),
                    IsVisible = _visibilityConfig.GetToolVisibility(guidId)
                };
                EditorTools.Add(toolViewModel);
            }

            EditorToolsItemsControl.ItemsSource = EditorTools;
        }

        private void ShowAll_Click(object sender, RoutedEventArgs e)
        {
            ImageView.Config.IsToolBarAlVisible = true;
            ImageView.Config.IsToolBarDrawVisible = true;
            ImageView.Config.IsToolBarTopVisible = true;
            ImageView.Config.IsToolBarLeftVisible = true;
            ImageView.Config.IsToolBarRightVisible = true;

            foreach (var tool in EditorTools)
            {
                tool.IsVisible = true;
            }
        }

        private void HideAll_Click(object sender, RoutedEventArgs e)
        {
            ImageView.Config.IsToolBarAlVisible = false;
            ImageView.Config.IsToolBarDrawVisible = false;
            ImageView.Config.IsToolBarTopVisible = false;
            ImageView.Config.IsToolBarLeftVisible = false;
            ImageView.Config.IsToolBarRightVisible = false;

            foreach (var tool in EditorTools)
            {
                tool.IsVisible = false;
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    /// <summary>
    /// ViewModel for individual editor tools
    /// </summary>
    public class EditorToolViewModel : Common.MVVM.ViewModelBase
    {
        private readonly ImageView _imageView;
        private readonly EditorToolVisibilityConfig _visibilityConfig;

        public IEditorTool Tool { get; set; }
        
        public string DisplayName { get; set; }
        
        public string Location { get; set; }

        private bool _isVisible = true;
        public bool IsVisible 
        { 
            get => _isVisible; 
            set 
            { 
                _isVisible = value; 
                OnPropertyChanged();
                // Update the actual tool visibility
                UpdateToolVisibility();
            } 
        }

        public EditorToolViewModel(ImageView imageView, IEditorTool tool, EditorToolVisibilityConfig visibilityConfig)
        {
            _imageView = imageView;
            Tool = tool;
            _visibilityConfig = visibilityConfig;
        }

        private void UpdateToolVisibility()
        {
            var guidId = Tool.GuidId ?? Tool.GetType().Name;
            
            // Save visibility state
            _visibilityConfig.SetToolVisibility(guidId, _isVisible);

            // Update UI element visibility
            if (_imageView.ImageViewModel.IEditorToolFactory.ToolUIElements.TryGetValue(guidId, out var uiElement))
            {
                uiElement.Visibility = _isVisible ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }
}
