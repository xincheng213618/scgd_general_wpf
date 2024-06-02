#pragma warning disable CS8602
using ColorVision.Scheduler;
using Quartz;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Update
{
    public class UpdateJob : IJob
    {
        public Task Execute(IJobExecutionContext context)
        {
            var schedulerInfo = QuartzSchedulerManager.GetInstance().TaskInfos.First(x => x.JobName == context.JobDetail.Key.Name && x.GroupName == context.JobDetail.Key.Group);

            Application.Current.Dispatcher.Invoke(() =>
            {
                schedulerInfo.Status = SchedulerStatus.Running;
            });
            // 定时任务逻辑
            Application.Current.Dispatcher.Invoke(async () =>
            {
                AutoUpdater.DeleteAllCachedUpdateFiles();
                AutoUpdater autoUpdater = AutoUpdater.GetInstance();
                await autoUpdater.CheckAndUpdate(false);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    schedulerInfo.Status = SchedulerStatus.Ready;
                });
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
