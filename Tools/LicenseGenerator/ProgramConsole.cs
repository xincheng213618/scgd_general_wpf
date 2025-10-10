using System.Text;

namespace LicenseGenerator
{
    /// <summary>
    /// 许可证生成工具
    /// 用于为指定的机器码生成许可证
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("==============================================");
            Console.WriteLine("       ColorVision 许可证生成工具");
            Console.WriteLine("==============================================");
            Console.WriteLine();

            if (args.Length == 0)
            {
                ShowHelp();
                InteractiveMode();
            }
            else
            {
                ProcessCommandLine(args);
            }
        }

        /// <summary>
        /// 显示帮助信息
        /// </summary>
        static void ShowHelp()
        {
            Console.WriteLine("用法:");
            Console.WriteLine("  LicenseGenerator [选项]");
            Console.WriteLine();
            Console.WriteLine("选项:");
            Console.WriteLine("  -m, --machine-code <code>    指定机器码生成许可证");
            Console.WriteLine("  -f, --file <filepath>        从文件读取机器码列表（每行一个）");
            Console.WriteLine("  -o, --output <filepath>      输出许可证到文件");
            Console.WriteLine("  -b, --batch                  批量模式：输入文件每行格式为 '机器码'");
            Console.WriteLine("                               输出文件每行格式为 '机器码,许可证'");
            Console.WriteLine("  -h, --help                   显示此帮助信息");
            Console.WriteLine();
            Console.WriteLine("示例:");
            Console.WriteLine("  LicenseGenerator -m 74657374");
            Console.WriteLine("  LicenseGenerator -f machinecodes.txt -o licenses.txt");
            Console.WriteLine("  LicenseGenerator -m 74657374 -o license.txt");
            Console.WriteLine();
        }

        /// <summary>
        /// 交互模式
        /// </summary>
        static void InteractiveMode()
        {
            while (true)
            {
                Console.WriteLine("请选择操作:");
                Console.WriteLine("1. 生成当前机器的许可证");
                Console.WriteLine("2. 为指定机器码生成许可证");
                Console.WriteLine("3. 批量生成许可证（从文件）");
                Console.WriteLine("4. 退出");
                Console.Write("请输入选项 (1-4): ");

                string? choice = Console.ReadLine();

                switch (choice)
                {
                    case "1":
                        GenerateForCurrentMachine();
                        break;
                    case "2":
                        GenerateForSpecificMachineCode();
                        break;
                    case "3":
                        BatchGenerateFromFile();
                        break;
                    case "4":
                        Console.WriteLine("再见！");
                        return;
                    default:
                        Console.WriteLine("无效选项，请重新选择。");
                        break;
                }
                Console.WriteLine();
            }
        }

        /// <summary>
        /// 生成当前机器的许可证
        /// </summary>
        static void GenerateForCurrentMachine()
        {
            try
            {
                string machineCode = LicenseHelper.GetMachineCode();
                Console.WriteLine($"当前机器码: {machineCode}");
                Console.WriteLine($"机器名称: {Environment.MachineName}");
                
                string license = LicenseHelper.CreateLicense(machineCode);
                Console.WriteLine($"生成的许可证: {license}");
                
                // 验证生成的许可证
                bool isValid = LicenseHelper.VerifyLicense(license, machineCode);
                Console.WriteLine($"许可证验证: {(isValid ? "通过 ✓" : "失败 ✗")}");
                
                Console.Write("\n是否保存到文件? (y/n): ");
                if (Console.ReadLine()?.ToLower() == "y")
                {
                    Console.Write("请输入文件路径: ");
                    string? filepath = Console.ReadLine();
                    if (!string.IsNullOrWhiteSpace(filepath))
                    {
                        File.WriteAllText(filepath, license);
                        Console.WriteLine($"许可证已保存到: {filepath}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 为指定机器码生成许可证
        /// </summary>
        static void GenerateForSpecificMachineCode()
        {
            Console.Write("请输入机器码: ");
            string? machineCode = Console.ReadLine();
            
            if (string.IsNullOrWhiteSpace(machineCode))
            {
                Console.WriteLine("机器码不能为空！");
                return;
            }

            try
            {
                string license = LicenseHelper.CreateLicense(machineCode);
                Console.WriteLine($"机器码: {machineCode}");
                Console.WriteLine($"生成的许可证: {license}");
                
                Console.Write("\n是否保存到文件? (y/n): ");
                if (Console.ReadLine()?.ToLower() == "y")
                {
                    Console.Write("请输入文件路径: ");
                    string? filepath = Console.ReadLine();
                    if (!string.IsNullOrWhiteSpace(filepath))
                    {
                        File.WriteAllText(filepath, license);
                        Console.WriteLine($"许可证已保存到: {filepath}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 从文件批量生成许可证
        /// </summary>
        static void BatchGenerateFromFile()
        {
            Console.Write("请输入机器码列表文件路径: ");
            string? inputFile = Console.ReadLine();
            
            if (string.IsNullOrWhiteSpace(inputFile) || !File.Exists(inputFile))
            {
                Console.WriteLine("文件不存在或路径无效！");
                return;
            }

            Console.Write("请输入输出文件路径 (留空则输出到控制台): ");
            string? outputFile = Console.ReadLine();

            try
            {
                string[] machineCodes = File.ReadAllLines(inputFile);
                List<string> results = new List<string>();
                
                Console.WriteLine($"\n开始批量生成 {machineCodes.Length} 个许可证...\n");
                
                foreach (string machineCode in machineCodes)
                {
                    if (string.IsNullOrWhiteSpace(machineCode) || machineCode.TrimStart().StartsWith("#"))
                        continue;

                    try
                    {
                        string license = LicenseHelper.CreateLicense(machineCode.Trim());
                        string result = $"{machineCode.Trim()},{license}";
                        results.Add(result);
                        Console.WriteLine($"✓ {machineCode.Trim()} -> {license}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"✗ {machineCode.Trim()} -> 错误: {ex.Message}");
                    }
                }

                if (!string.IsNullOrWhiteSpace(outputFile))
                {
                    File.WriteAllLines(outputFile, results);
                    Console.WriteLine($"\n成功生成 {results.Count} 个许可证，已保存到: {outputFile}");
                }
                else
                {
                    Console.WriteLine($"\n成功生成 {results.Count} 个许可证");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理命令行参数
        /// </summary>
        static void ProcessCommandLine(string[] args)
        {
            string? machineCode = null;
            string? inputFile = null;
            string? outputFile = null;
            bool batchMode = false;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLower())
                {
                    case "-h":
                    case "--help":
                        ShowHelp();
                        return;
                    
                    case "-m":
                    case "--machine-code":
                        if (i + 1 < args.Length)
                            machineCode = args[++i];
                        break;
                    
                    case "-f":
                    case "--file":
                        if (i + 1 < args.Length)
                            inputFile = args[++i];
                        break;
                    
                    case "-o":
                    case "--output":
                        if (i + 1 < args.Length)
                            outputFile = args[++i];
                        break;
                    
                    case "-b":
                    case "--batch":
                        batchMode = true;
                        break;
                }
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(inputFile))
                {
                    // 批量处理
                    ProcessBatchFile(inputFile, outputFile);
                }
                else if (!string.IsNullOrWhiteSpace(machineCode))
                {
                    // 单个机器码
                    string license = LicenseHelper.CreateLicense(machineCode);
                    
                    if (!string.IsNullOrWhiteSpace(outputFile))
                    {
                        File.WriteAllText(outputFile, license);
                        Console.WriteLine($"许可证已生成并保存到: {outputFile}");
                    }
                    else
                    {
                        Console.WriteLine($"机器码: {machineCode}");
                        Console.WriteLine($"许可证: {license}");
                    }
                }
                else
                {
                    Console.WriteLine("错误: 必须指定机器码 (-m) 或输入文件 (-f)");
                    ShowHelp();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误: {ex.Message}");
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// 批量处理文件
        /// </summary>
        static void ProcessBatchFile(string inputFile, string? outputFile)
        {
            if (!File.Exists(inputFile))
            {
                throw new FileNotFoundException($"输入文件不存在: {inputFile}");
            }

            string[] machineCodes = File.ReadAllLines(inputFile);
            List<string> results = new List<string>();

            Console.WriteLine($"开始批量生成 {machineCodes.Length} 个许可证...\n");

            int successCount = 0;
            int failCount = 0;

            foreach (string line in machineCodes)
            {
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                    continue;

                try
                {
                    string machineCode = line.Trim();
                    string license = LicenseHelper.CreateLicense(machineCode);
                    results.Add($"{machineCode},{license}");
                    successCount++;
                    Console.WriteLine($"✓ {machineCode}");
                }
                catch (Exception ex)
                {
                    failCount++;
                    Console.WriteLine($"✗ {line.Trim()} - 错误: {ex.Message}");
                }
            }

            if (!string.IsNullOrWhiteSpace(outputFile))
            {
                File.WriteAllLines(outputFile, results);
                Console.WriteLine($"\n批量生成完成！");
                Console.WriteLine($"成功: {successCount}, 失败: {failCount}");
                Console.WriteLine($"结果已保存到: {outputFile}");
            }
            else
            {
                Console.WriteLine("\n生成的许可证:");
                Console.WriteLine("=========================================");
                foreach (string result in results)
                {
                    Console.WriteLine(result);
                }
                Console.WriteLine("=========================================");
                Console.WriteLine($"成功: {successCount}, 失败: {failCount}");
            }
        }
    }
}
