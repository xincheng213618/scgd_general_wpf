using System;
using ColorVision.Solution;

namespace ColorVision.Solution.Examples
{
    /// <summary>
    /// 简单的 ConPTY 终端使用示例
    /// </summary>
    public class ConPtyExample
    {
        public static void BasicUsageExample()
        {
            // 创建终端实例
            using var terminal = new ConPtyTerminal();

            // 订阅输出事件
            terminal.OutputReceived += (sender, output) =>
            {
                Console.Write(output);
            };

            // 启动终端 (80列 x 25行，运行 cmd.exe)
            terminal.Start(80, 25, "cmd.exe");

            // 等待用户输入
            Console.WriteLine("终端已启动。输入命令然后按 Enter，输入 'exit' 退出。");
            
            string? input;
            while ((input = Console.ReadLine()) != null)
            {
                if (input.Equals("exit", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                // 发送命令到终端（需要添加回车换行）
                terminal.SendInput(input + "\r\n");
            }

            // Dispose 会自动清理资源
        }

        public static void PowerShellExample()
        {
            using var terminal = new ConPtyTerminal();
            
            terminal.OutputReceived += (sender, output) =>
            {
                Console.Write(output);
            };

            // 启动 PowerShell 而不是 cmd.exe
            terminal.Start(120, 30, "powershell.exe");

            // 执行一些 PowerShell 命令
            terminal.SendInput("Get-Process | Select-Object -First 5\r\n");
            
            System.Threading.Thread.Sleep(2000);

            terminal.SendInput("exit\r\n");
        }

        public static void ResizeExample()
        {
            using var terminal = new ConPtyTerminal();
            
            terminal.OutputReceived += (sender, output) =>
            {
                Console.Write(output);
            };

            // 启动时使用小尺寸
            terminal.Start(40, 10, "cmd.exe");

            System.Threading.Thread.Sleep(1000);

            // 调整到更大的尺寸
            terminal.Resize(120, 40);

            System.Threading.Thread.Sleep(1000);

            terminal.SendInput("exit\r\n");
        }
    }
}
