using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.HotKey
{
    /// <summary>
    /// HotKeysSetting.xaml 的交互逻辑
    /// </summary>
    public partial class HotKeysSetting : UserControl
    {
        private List<HotKeys> _editableHotKeys = new();

        public HotKeysSetting()
        {
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            LoadEditableHotKeys(useSavedSettings: false);
        }

        private void LoadEditableHotKeys(bool useSavedSettings)
        {
            _editableHotKeys = HotkeyService.GetInstance().CreateEditableHotKeys(useSavedSettings);
            RenderEditableHotKeys();
        }

        private void RenderEditableHotKeys()
        {
            HotKeyStackPanel.Children.Clear();
            foreach (HotKeys hotKeys in _editableHotKeys)
            {
                HotKeyStackPanel.Children.Add(new HoyKeyControl(hotKeys));
            }
        }

        private void SetDefault_Click(object sender, RoutedEventArgs e)
        {
            _editableHotKeys = HotkeyService.GetInstance().CreateDefaultEditableHotKeys();
            RenderEditableHotKeys();
        }

        private void ButtonLoad_Click(object sender, RoutedEventArgs e)
        {
            LoadEditableHotKeys(useSavedSettings: true);
        }

        private void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            HotkeyService.GetInstance().ApplySettings(_editableHotKeys.Select(HotkeySetting.FromHotKeys));
            HotkeyService.GetInstance().SaveSettings();
            LoadEditableHotKeys(useSavedSettings: false);
        }
    }
}
