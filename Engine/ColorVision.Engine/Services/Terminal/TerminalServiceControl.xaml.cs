using System.Windows.Controls;


namespace ColorVision.Engine.Services.Terminal
{
    /// <summary>
    /// TerminalServiceControl.xaml 的交互逻辑
    /// </summary>
    public partial class TerminalServiceControl : UserControl
    {
        public TerminalService ServiceTerminal { get; set; }

        public TerminalServiceControl(TerminalService mQTTService)
        {
            ServiceTerminal = mQTTService;
            InitializeComponent();
            DataContext = ServiceTerminal;
        }
    }
}
