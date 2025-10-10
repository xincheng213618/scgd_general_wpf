using System;

namespace LicenseGenerator
{
    /// <summary>
    /// 控制台测试程序 - 用于测试增强许可证功能
    /// </summary>
    public class TestProgram
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("ColorVision 许可证生成工具 - 控制台测试");
            Console.WriteLine("=========================================\n");

            if (args.Length > 0 && args[0] == "--test")
            {
                // 运行测试
                EnhancedLicenseTests.RunTests();
            }
            else
            {
                Console.WriteLine("使用方法:");
                Console.WriteLine("  dotnet run --project LicenseGenerator.csproj -- --test");
                Console.WriteLine("\n或者直接运行 GUI 版本：");
                Console.WriteLine("  dotnet run --project LicenseGenerator.csproj");
            }

            Console.WriteLine("\n按任意键退出...");
            Console.ReadKey();
        }
    }
}
