using ColorVision.Themes;
using ColorVision.UI;
using System.ComponentModel;
using System.Windows;

namespace WindowsServicePlugin.ServiceManager
{
    public partial class ServiceManagerWindow : Window
    {
        public ServiceManagerWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            DataContext = ServiceManagerViewModel.Instance;

            // 日志自动滚动
            ServiceManagerViewModel.Instance.PropertyChanged += ViewModel_PropertyChanged;
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ServiceManagerViewModel.LogText))
            {
                Dispatcher.InvokeAsync(() =>
                {
                    LogTextBox.ScrollToEnd();
                });
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            ServiceManagerViewModel.Instance.PropertyChanged -= ViewModel_PropertyChanged;
            base.OnClosed(e);
        }
    }
}
