﻿using ColorVision.Common.Utilities;
using ColorVision.Properties;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Update.Export
{
    public class MenuChangeLog : MenuItemBase
    {
        public override string OwnerGuid => "Update";
        public override string GuidId => "ChangeLog";
        public override string Header => Resources.ChangeLog;
        public override int Order => 1;

        [RequiresPermission(PermissionMode.Administrator)]
        public override void Execute()
        {
            new ChangelogWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
    }
}