﻿using ColorVision.UI.Authorizations;
using ColorVision.UI.Menus;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Windows;
using ColorVision.Common.Utilities;

namespace WindowsServicePlugin.Menus
{
    public class CVWinSMSConfig : IConfig
    {
        public static CVWinSMSConfig Instance => ConfigHandler.GetInstance().GetRequiredService<CVWinSMSConfig>();


        public string CVWinSMSPath { get => _CVWinSMSPath; set => _CVWinSMSPath = value; }
        private string _CVWinSMSPath = string.Empty;
    }

    public class ExportCVWinSMS : MenuItemBase
    {
        public ExportCVWinSMS()
        {
            if (string.IsNullOrWhiteSpace(CVWinSMSConfig.Instance.CVWinSMSPath))
            {
                if (File.Exists("D:\\CVService\\InstallTool\\CVWinSMS.exe"))
                {
                    CVWinSMSConfig.Instance.CVWinSMSPath = "D:\\CVService\\InstallTool\\CVWinSMS.exe";
                }
            }
        }

        public override string OwnerGuid => "ServiceLog";

        public override string GuidId => "CVWinSMS";

        public override int Order => 99;

        public override string Header => "管理服务";

        [RequiresPermission(PermissionMode.User)]
        public override void Execute()
        {
            if (!File.Exists(CVWinSMSConfig.Instance.CVWinSMSPath))
            {
                if (MessageBox.Show(Application.Current.GetActiveWindow(), "I can't find CVWinSMS (CVWinSMS.exe). Would you like to help me find it?", "Open in CVWinSMS", MessageBoxButton.YesNo) == MessageBoxResult.No) return;
                using (System.Windows.Forms.OpenFileDialog openFileDialog = new())
                {
                    openFileDialog.Title = "Select CVWinSMS.exe";
                    openFileDialog.Filter = "CVWinSMS.exe|CVWinSMS.exe";
                    openFileDialog.RestoreDirectory = true;

                    if (openFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                    CVWinSMSConfig.Instance.CVWinSMSPath = openFileDialog.FileName;
                }
            }
            try
            {
                Process.Start(CVWinSMSConfig.Instance.CVWinSMSPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message);
            }
        }
    }



}