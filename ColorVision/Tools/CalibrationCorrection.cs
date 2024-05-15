using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Settings;
using ColorVision.UI;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace ColorVision.Tools
{
    public class CalibrationConfig : IConfig
    {
        public static CalibrationConfig Instance => ConfigHandler1.GetInstance().GetRequiredService<CalibrationConfig>();

        public string CalibToolsPath { get => _CalibToolsPath; set => _CalibToolsPath = value; }
        private string _CalibToolsPath = string.Empty;
    }


    public class CalibrationCorrection : IMenuItem
    {
        public string? OwnerGuid => "Tool";

        public string? GuidId => "CalibrationCorrection";

        public int Order => 6;

        public string? Header => ColorVision.Properties.Resource.MenuCalibrationTool;

        public string? InputGestureText => null;

        public object? Icon => null;

        public RelayCommand Command => new(A => Execute());
        public Visibility Visibility => Visibility.Visible;

        private static void Execute()
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
