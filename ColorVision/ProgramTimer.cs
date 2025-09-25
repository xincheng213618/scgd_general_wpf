// // Copyright (c) Microsoft. All rights reserved.
// // Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ColorVision.UI;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using Microsoft.Win32;
using System.Diagnostics;
using System.Text;
using System.Windows.Controls;

namespace ColorVision
{
    public class StartupRegistryChecker
    {
        public static StartupRegistryChecker Instance => new StartupRegistryChecker();

        private const string RegistryPath = @"Software\ColorVision\ColorVision";
        private const string StartupFlagKey = "Running";

        public static bool CheckAndSet()
        {
            using (var regKey = Registry.CurrentUser.CreateSubKey(RegistryPath))
            {
                var flag = regKey.GetValue(StartupFlagKey, 0);
                if ((int)flag == 1)
                {
                    // 上次没清理，说明崩溃
                    return false;
                }
                regKey.SetValue(StartupFlagKey, 1, RegistryValueKind.DWord);
                return true;
            }
        }

        /// <summary>
        /// 启动成功后清理标志
        /// </summary>
        public static void Clear()
        {
            using (var regKey = Registry.CurrentUser.CreateSubKey(RegistryPath))
            {
                regKey.DeleteValue(StartupFlagKey, false);
            }
        }
    }

    public class InitAppender : AppenderSkeleton
    {
        public StringBuilder Buffer { get; set; } = new StringBuilder();

        protected override void Append(LoggingEvent loggingEvent)
        {
            var renderedMessage = RenderLoggingEvent(loggingEvent);
            Buffer.Append(renderedMessage);
        }

        protected override void OnClose()
        {
            base.OnClose();
            Buffer.Clear();
        }
    }


    public static class ProgramTimer
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(App));
        private static Stopwatch _stopwatch;
        public static InitAppender InitAppender { get; set; }
        private static Hierarchy Hierarchy { get; set; }

        public static void Start()
        {
            _stopwatch = Stopwatch.StartNew();
            Hierarchy = (Hierarchy)LogManager.GetRepository();
            InitAppender = new InitAppender();
            InitAppender.Layout = new PatternLayout("%date{HH:mm:ss;fff} %-5level %message%newline");
            Hierarchy.Root.AddAppender(InitAppender);
            log4net.Config.BasicConfigurator.Configure(Hierarchy);
        }

        public static void StopAndReport()
        {
            Hierarchy.Root.RemoveAppender(InitAppender);
            log4net.Config.BasicConfigurator.Configure(Hierarchy);
            if (_stopwatch != null)
            {
                _stopwatch.Stop();
                log.Info($"程序运行时间: {_stopwatch.Elapsed.TotalSeconds} 秒");
            }
            else
            {
                log.Info("计时器未启动。");
            }
        }
    }
}