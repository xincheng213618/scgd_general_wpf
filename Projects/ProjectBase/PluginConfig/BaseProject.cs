﻿using ColorVision.UI;
using System.Windows;

namespace ProjectBase.PluginConfig
{
    public class BaseProject : IProjectBase
    {
        public override string? Header => "基础项目";

        public override string? UpdateUrl => "http://xc213618.ddns.me:9999/D%3A/ColorVision/Projects/ProjectBlackMura";

        public override void Execute()
        {
            if (ProjectWindowInstance.WindowInstance == null)
            {
                ProjectWindowInstance.WindowInstance = new MainWindow
                {
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                ProjectWindowInstance.WindowInstance.Closed += (s, e) => ProjectWindowInstance.WindowInstance = null;
                ProjectWindowInstance.WindowInstance.Show();
            }
            else
            {
                ProjectWindowInstance.WindowInstance.Activate();
            }
        }
    }
}
