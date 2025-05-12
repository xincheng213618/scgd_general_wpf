using ColorVision.Common.MVVM;
using System.Windows;

namespace WindowsServicePlugin.CVWinSMS
{
    public class UpdateServiceControl : ViewModelBase
    {
        public int StepIndex { get => _StepIndex; set { _StepIndex = value; NotifyPropertyChanged(); } }
        private int _StepIndex = 1;

        /// <summary>
        ///     下一步
        /// </summary>
        public RelayCommand NextCmd => new RelayCommand(a=> Next());

        /// <summary>
        ///     上一步
        /// </summary>
        public RelayCommand PrevCmd => new RelayCommand(a => Next());

        private void Next()
        {

        }

        private void Prev()
        {
        }


    }


    /// <summary>
    /// UpdateServiceWindow.xaml 的交互逻辑
    /// </summary>
    public partial class UpdateServiceWindow : Window
    {
        public UpdateServiceWindow()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {

        }
    }
}
