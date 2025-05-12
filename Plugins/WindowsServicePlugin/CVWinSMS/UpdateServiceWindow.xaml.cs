using ColorVision.Common.MVVM;
using HandyControl.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace WindowsServicePlugin.CVWinSMS
{
    public class UpdateServiceControl : ViewModelBase
    {
        public int StepIndex { get => _StepIndex; set { _StepIndex = value; NotifyPropertyChanged(); } }
        private int _StepIndex = 1;

        /// <summary>
        ///     下一步
        /// </summary>
        public RelayCommand<Panel> NextCmd => new RelayCommand(Next);

        /// <summary>
        ///     上一步
        /// </summary>
        public RelayCommand<Panel> PrevCmd => new RelayCommand(Prev);

        private void Next(Panel panel)
        {
            foreach (var stepBar in panel.Children.OfType<StepBar>())
            {
                stepBar.Next();
            }
        }

        private void Prev(Panel panel)
        {
            foreach (var stepBar in panel.Children.OfType<StepBar>())
            {
                stepBar.Prev();
            }
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
