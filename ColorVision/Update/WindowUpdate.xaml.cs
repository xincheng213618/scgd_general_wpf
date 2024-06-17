using ColorVision.Common.MVVM;
using ColorVision.UI.HotKey;
using System;
using System.Windows;
using System.Windows.Input;
using ColorVision.UI.Menus;
using ColorVision.Themes;
using ColorVision.Common.Utilities;
using ColorVision.UI.Authorizations;

namespace ColorVision.Update
{
    public class ExportUpdate: MenuItemBase, IHotKey
    {
        public HotKeys HotKeys => new(ColorVision.Properties.Resources.Update, new Hotkey(Key.U, ModifierKeys.Control), Execute);

        public override string OwnerGuid => "Help";

        public override string GuidId => "MenuUpdate";

        public override int Order => 10003;
        public override Visibility Visibility => Visibility.Visible;

        public override string Header => Properties.Resources.MenuUpdate;

        public override string InputGestureText => "Ctrl + U";


        [RequiresPermission(PermissionMode.Administrator)]
        public override void Execute() => _ = AutoUpdater.GetInstance().CheckAndUpdate();
    }



    /// <summary>
    /// WindowUpdate.xaml 的交互逻辑
    /// </summary>
    public partial class WindowUpdate : Window
    {
        public WindowUpdate()
        {
            InitializeComponent();
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            DataContext = AutoUpdater.GetInstance(); 
        }
    }
}
