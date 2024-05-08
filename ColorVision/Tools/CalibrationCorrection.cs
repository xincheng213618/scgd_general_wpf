using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Settings;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Tools
{
    public class CalibrationCorrection : IMenuItem
    {
        public string? OwnerGuid => "Tool";

        public string? GuidId => "CalibrationCorrection";

        public int Order => 6;

        public string? Header => ColorVision.Properties.Resource.MenuCalibrationTool;

        public string? InputGestureText => null;

        public object? Icon => null;

        public RelayCommand Command => new RelayCommand(A => Execute());

        private static void Execute()
        {
            if (!File.Exists(ConfigHandler.GetInstance().SoftwareConfig.CalibToolsPath))
            {
                if (MessageBox.Show(Application.Current.GetActiveWindow(), "I can't find CalibTools (CalibTools.exe). Would you like to help me find it?", "Open in CalibTools", MessageBoxButton.YesNo) == MessageBoxResult.No) return;
                using (System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog())
                {
                    openFileDialog.Title = "Select CalibTools.exe";
                    openFileDialog.Filter = "CalibTools.exe|CalibTools.exe";
                    openFileDialog.RestoreDirectory = true;

                    if (openFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                    ConfigHandler.GetInstance().SoftwareConfig.CalibToolsPath = openFileDialog.FileName;
                }
            }
            try
            {
                Process.Start(ConfigHandler.GetInstance().SoftwareConfig.CalibToolsPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message);
            }
        }
    }
}
