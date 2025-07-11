﻿// // Copyright (c) Microsoft. All rights reserved.
// // Licensed under the MIT license. See LICENSE file in the project root for full license information.

using log4net;
using Microsoft.Win32;
using System.Diagnostics;

namespace ColorVision
{
    public class StartupRegistryChecker
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(StartupRegistryChecker));

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

    public static class ProgramTimer
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(App));
        private static Stopwatch _stopwatch;

        public static void Start()
       {
            _stopwatch = Stopwatch.StartNew();
        }

        public static void StopAndReport()
        {
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