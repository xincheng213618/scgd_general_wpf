using ColorVision.Common.Utilities;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Menus;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace SerialPlugin
{
    public class CalibrationConfig : IConfig
    {
        public static CalibrationConfig Instance => ConfigHandler.GetInstance().GetRequiredService<CalibrationConfig>();

        public string CalibToolsPath { get => _CalibToolsPath; set => _CalibToolsPath = value; }
        private string _CalibToolsPath = string.Empty;
    }


    public class CalibrationCorrection : MenuItemBase
    {
        public override string OwnerGuid => "Tool";

        public override string GuidId => "CalibrationCorrection";

        public override int Order => 6;

        public override string Header => "CalibrationCorrection";

        [RequiresPermission(PermissionMode.User)]
        public override void Execute()
        {
            if (!File.Exists(CalibrationConfig.Instance.CalibToolsPath))
            {
                if (MessageBox.Show(Application.Current.GetActiveWindow(), "I can't find CalibTools (CalibTools.exe). Would you like to help me find it?", "Open in CalibTools", MessageBoxButton.YesNo) == MessageBoxResult.No) return;
                using (System.Windows.Forms.OpenFileDialog openFileDialog = new())
                {
                    openFileDialog.Title = "Select CalibTools.exe";
                    openFileDialog.Filter = "CalibTools.exe|CalibTools.exe";
                    openFileDialog.RestoreDirectory = true;

                    if (openFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                    CalibrationConfig.Instance.CalibToolsPath = openFileDialog.FileName;
                }
            }
            try
            {
                Process.Start(CalibrationConfig.Instance.CalibToolsPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message);
            }
        }
    }
}
