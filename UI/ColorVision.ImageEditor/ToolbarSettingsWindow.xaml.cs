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

        public ToolbarSettingsWindow(ImageView imageView)
        {
            InitializeComponent();
            ImageView = imageView;
            DataContext = imageView.ImageViewModel;

            // Initialize editor tools visibility list
            EditorTools = new ObservableCollection<EditorToolViewModel>();
            foreach (var tool in imageView.ImageViewModel.IEditorToolFactory.IEditorTools.OrderBy(t => t.ToolBarLocal).ThenBy(t => t.Order))
            {
                var toolViewModel = new EditorToolViewModel
                {
                    Tool = tool,
                    DisplayName = tool.GuidId ?? tool.GetType().Name,
                    Location = tool.ToolBarLocal.ToString(),
                    IsVisible = true // Default to visible, you can enhance this with persistence
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

        private void UpdateToolVisibility()
        {
            // This would require enhancing the EditorToolFactory to support dynamic visibility
            // For now, this is a placeholder for future enhancement
        }
    }
}
