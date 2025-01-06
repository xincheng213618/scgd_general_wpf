// // Copyright (c) Microsoft. All rights reserved.
// // Licensed under the MIT license. See LICENSE file in the project root for full license information.

using log4net;
using System.Diagnostics;

namespace ColorVision
{
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