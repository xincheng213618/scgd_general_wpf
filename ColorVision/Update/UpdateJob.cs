#pragma warning disable CS8602
using Quartz;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Update
{
    public class UpdateJob : IJob
    {
        public Task Execute(IJobExecutionContext context)
        {
            // 定时任务逻辑
            Application.Current.Dispatcher.Invoke(() =>
            {
                AutoUpdater.DeleteAllCachedUpdateFiles();
                AutoUpdater autoUpdater = AutoUpdater.GetInstance();
                autoUpdater.CheckAndUpdate(true);
            });
            return Task.CompletedTask;
        }
    }

    public class ExcutCMDJob : IJob
    {
        public Task Execute(IJobExecutionContext context)
        {
            // 从作业上下文获取脚本路径
            string scriptPath = context.JobDetail.JobDataMap.GetString("scriptPath");

            // 执行 CMD 脚本
            ExecuteCmdScript(scriptPath);

            return Task.CompletedTask;
        }

        private static void ExecuteCmdScript(string scriptPath)
        {
            try
            {
                ProcessStartInfo processInfo = new ProcessStartInfo("cmd.exe", $"/c \"{scriptPath}\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (Process process = Process.Start(processInfo))
                {
                    process.OutputDataReceived += (sender, args) => Console.WriteLine(args.Data);
                    process.ErrorDataReceived += (sender, args) => Console.WriteLine($"ERROR: {args.Data}");

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    process.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception occurred while executing CMD script: {ex.Message}");
            }
        }
    }
}
