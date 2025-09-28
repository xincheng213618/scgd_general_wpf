using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text;

namespace ColorVision.Scripts
{
    /// <summary>
    /// 基于 _inventory.json 生成 _sidebar.md 的工具
    /// 使用方法: dotnet run [--sort] [--output path]
    /// </summary>
    class SidebarGenerator
    {
        private static readonly Dictionary<string, string> SectionOrder = new()
        {
            { "introduction", "🚀 Introduction" },
            { "getting-started", "📚 Getting Started" },
            { "architecture", "🏗️ Architecture" },
            { "ui-components", "💻 UI组件" },
            { "engine-components", "⚙️ Engine组件" },
            { "solution", "📊 Solution管理" },
            { "image-editor", "🖼️ ImageEditor" },
            { "scheduler", "⏰ Scheduler" },
            { "database", "💾 Database" },
            { "plugins", "🔌 Plugins" },
            { "flow-engine", "⚙️ Flow Engine" },
            { "templates", "📋 Templates" },
            { "troubleshooting", "🛠️ Troubleshooting" },
            { "performance", "📈 Performance" },
            { "extensibility", "🔧 Extensibility" },
            { "security", "🔒 Security/RBAC" },
            { "api", "📡 API" },
            { "changelog", "📄 Changelog" }
        };

        static void Main(string[] args)
        {
            bool sortEnabled = args.Contains("--sort");
            string outputPath = GetArgumentValue(args, "--output") ?? "docs/_sidebar.md";
            string inventoryPath = "docs/_inventory.json";
            
            Console.WriteLine("ColorVision Sidebar Generator");
            Console.WriteLine($"Inventory: {inventoryPath}");
            Console.WriteLine($"Output: {outputPath}");
            Console.WriteLine($"Sort enabled: {sortEnabled}");
            
            try
            {
                if (File.Exists(inventoryPath))
                {
                    GenerateFromInventory(inventoryPath, outputPath, sortEnabled);
                }
                else
                {
                    Console.WriteLine("inventory.json not found, generating from directory structure...");
                    GenerateFromDirectory("docs", outputPath, sortEnabled);
                }
                
                Console.WriteLine($"✅ Sidebar generated successfully: {outputPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                Environment.Exit(1);
            }
        }

        static string? GetArgumentValue(string[] args, string argument)
        {
            var index = Array.IndexOf(args, argument);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        static void GenerateFromInventory(string inventoryPath, string outputPath, bool sort)
        {
            var jsonContent = File.ReadAllText(inventoryPath);
            var inventory = JsonSerializer.Deserialize<InventoryData>(jsonContent);
            
            var sidebar = new StringBuilder();
            sidebar.AppendLine("<!-- _sidebar.md -->");
            sidebar.AppendLine();
            sidebar.AppendLine("- [项目首页](/)");
            sidebar.AppendLine();
            
            if (inventory?.structure != null)
            {
                ProcessInventoryStructure(inventory.structure, sidebar, sort);
            }
            
            File.WriteAllText(outputPath, sidebar.ToString());
        }

        static void GenerateFromDirectory(string docsPath, string outputPath, bool sort)
        {
            var sidebar = new StringBuilder();
            sidebar.AppendLine("<!-- _sidebar.md -->");
            sidebar.AppendLine();
            sidebar.AppendLine("- [项目首页](/)");
            sidebar.AppendLine();
            
            var directories = Directory.GetDirectories(docsPath)
                .Where(d => !Path.GetFileName(d).StartsWith("_") && !Path.GetFileName(d).StartsWith("."))
                .ToList();
                
            if (sort)
            {
                directories = directories.OrderBy(d => GetSectionPriority(Path.GetFileName(d))).ToList();
            }
            
            foreach (var dir in directories)
            {
                ProcessDirectory(dir, sidebar, docsPath);
            }
            
            File.WriteAllText(outputPath, sidebar.ToString());
        }

        static void ProcessInventoryStructure(object structure, StringBuilder sidebar, bool sort)
        {
            // 简化实现 - 实际需要根据 inventory.json 结构进行解析
            // 这里提供一个占位符实现
            sidebar.AppendLine("## 📚 从 Inventory 生成");
            sidebar.AppendLine("- [TODO: 实现从 inventory.json 生成逻辑](/)");
            sidebar.AppendLine();
        }

        static void ProcessDirectory(string dirPath, StringBuilder sidebar, string basePath)
        {
            var dirName = Path.GetFileName(dirPath);
            var sectionTitle = GetSectionTitle(dirName);
            
            sidebar.AppendLine($"## {sectionTitle}");
            sidebar.AppendLine();
            
            // 查找 README.md
            var readmePath = Path.Combine(dirPath, "README.md");
            if (File.Exists(readmePath))
            {
                var relativePath = Path.GetRelativePath(basePath, readmePath).Replace('\\', '/').Replace(".md", "");
                sidebar.AppendLine($"- [概览]({relativePath})");
            }
            
            // 处理 Markdown 文件
            var mdFiles = Directory.GetFiles(dirPath, "*.md", SearchOption.AllDirectories)
                .Where(f => Path.GetFileName(f) != "README.md")
                .OrderBy(f => f);
                
            foreach (var file in mdFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                var relativePath = Path.GetRelativePath(basePath, file).Replace('\\', '/').Replace(".md", "");
                sidebar.AppendLine($"- [{fileName}]({relativePath})");
            }
            
            sidebar.AppendLine();
        }

        static string GetSectionTitle(string dirName)
        {
            return SectionOrder.TryGetValue(dirName, out var title) ? title : $"📁 {dirName}";
        }

        static int GetSectionPriority(string dirName)
        {
            var keys = SectionOrder.Keys.ToList();
            var index = keys.IndexOf(dirName);
            return index >= 0 ? index : int.MaxValue;
        }

        // 简化的 Inventory 数据模型
        public class InventoryData
        {
            public object? structure { get; set; }
        }
    }
}

/*
使用说明:

1. 编译并运行:
   dotnet run --project scripts/gen-sidebar.cs

2. 带参数运行:
   dotnet run --project scripts/gen-sidebar.cs -- --sort
   dotnet run --project scripts/gen-sidebar.cs -- --output docs/_sidebar.md --sort

3. 功能特性:
   - 基于预设排序生成侧边栏
   - 支持从 _inventory.json 或目录结构生成
   - 自动处理中文文件名
   - 支持自定义输出路径

注意: 这是一个占位符实现，实际使用时需要根据具体的 _inventory.json 格式进行调整
*/