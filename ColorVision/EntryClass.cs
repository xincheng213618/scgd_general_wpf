// // Copyright (c) Microsoft. All rights reserved.
// // Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ColorVision.UI.Shell;
using ColorVision.UI.Desktop.Operations;
using log4net;
using log4net.Config;
using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Linq;
using System.Threading;

[assembly: XmlConfigurator(ConfigFile = "log4net.config", Watch = true)]
namespace ColorVision
{
    /// <summary>
    /// Main�����Ľ������ڳ���֮�У�Ϊ�˲�Ӱ��APP������������һ����
    /// </summary>
    public partial class App
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(App));

        private static Mutex mutex;

        [STAThread]
        [DebuggerNonUserCode]
        [GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
        public static void Main(string[] args)
        {
            args = WindowsApplicationRestartRegistration.CaptureAndRemoveRecoveryArguments(args);
            args = OperationsApplicationRestartController.WaitForEarlierProcessAndRemoveHandoffArguments(args);
            bool automaticRecoveryRegistered = WindowsApplicationRestartRegistration.TryRegister();
            ProgramTimer.Start();
            ArgumentParser.GetInstance().CommandLineArgs = args;
            log.Debug("args" + string.Join(", ", args));
            if (!automaticRecoveryRegistered)
                log.Warn("Windows application failure restart could not be registered.");
            else if (WindowsApplicationRestartRegistration.RestartedAfterFailure)
                log.Warn("ColorVision was restarted by Windows after an earlier application failure.");

            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            if (Environment.CurrentDirectory.Contains("C:\\Program Files"))
            {
                var fileAppender = (log4net.Appender.FileAppender)LogManager.GetRepository().GetAppenders().FirstOrDefault(a => a is log4net.Appender.FileAppender);
                if (fileAppender != null)
                {
                    fileAppender.File = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\ColorVision\\Log\\";
                    fileAppender.ActivateOptions();
                }
            }

            App app;
            app = new App();
            app.InitializeComponent();
            app.Run();
        }

    }
}
