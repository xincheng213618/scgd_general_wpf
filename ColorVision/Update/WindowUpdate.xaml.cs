using ColorVision.Common.MVVM;
using ColorVision.UI.HotKey;
using ColorVision.UI;
using System;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Update
{
    public class ExportUpdate: IHotKey, IMenuItem
    {
        public HotKeys HotKeys => new(Properties.Resource.MenuUpdate, new Hotkey(Key.U, ModifierKeys.Control), Execute);

        public string? OwnerGuid => "Help";

        public string? GuidId => "MenuUpdate";

        public int Order => 10003;
        public Visibility Visibility => Visibility.Visible;

        public string? Header => Properties.Resource.MenuUpdate;

        public string? InputGestureText => "Ctrl + U";

        public object? Icon { get; set; }

        public RelayCommand Command => new(A => Execute());

        private void Execute()
        {
            AutoUpdater.GetInstance().CheckAndUpdate();
        }
    }



    /// <summary>
    /// WindowUpdate.xaml 的交互逻辑
    /// </summary>
    public partial class WindowUpdate : Window
    {
        public WindowUpdate()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            DataContext = AutoUpdater.GetInstance(); 
        }
    }
}
