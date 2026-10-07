using System;
using System.Net;
using System.Windows;

namespace ColorVisionSetup
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.SystemDefault;
            SetupFiles.Log("Setup helper started; runtime=" + Environment.Version);
            base.OnStartup(e);
        }
    }
}
