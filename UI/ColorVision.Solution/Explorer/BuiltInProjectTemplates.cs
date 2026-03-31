using System.IO;
using System.Windows.Media;

namespace ColorVision.Solution.Explorer
{
    [ProjectTemplate(0)]
    public class EmptyProjectTemplate : IProjectTemplate
    {
        public string Name => "空项目";
        public string Category => "通用";
        public string Description => "创建一个空的项目，仅包含 .cvproj 文件";
        public int Order => 1;
        public ImageSource? Icon => null;

        public void CreateProject(string projectDir, string projectName)
        {
            string cvprojPath = Path.Combine(projectDir, projectName + ".cvproj");
            File.WriteAllText(cvprojPath, "{\n  \"Name\": \"" + projectName + "\",\n  \"Version\": \"1.0\"\n}\n");
        }
    }

    [ProjectTemplate(0)]
    public class ScriptProjectTemplate : IProjectTemplate
    {
        public string Name => "脚本项目";
        public string Category => "通用";
        public string Description => "创建一个包含脚本目录和示例脚本的项目";
        public int Order => 2;
        public ImageSource? Icon => null;

        public void CreateProject(string projectDir, string projectName)
        {
            string cvprojPath = Path.Combine(projectDir, projectName + ".cvproj");
            File.WriteAllText(cvprojPath, "{\n  \"Name\": \"" + projectName + "\",\n  \"Version\": \"1.0\"\n}\n");

            string scriptsDir = Path.Combine(projectDir, "Scripts");
            Directory.CreateDirectory(scriptsDir);

            string mainScript = Path.Combine(scriptsDir, "main.py");
            File.WriteAllText(mainScript,
                "#!/usr/bin/env python3\n" +
                "# -*- coding: utf-8 -*-\n" +
                $"\"\"\"\n{projectName} - Main Script\n\"\"\"\n\n\n" +
                "def main():\n" +
                "    pass\n\n\n" +
                "if __name__ == \"__main__\":\n" +
                "    main()\n");
        }
    }

    [ProjectTemplate(0)]
    public class DataProjectTemplate : IProjectTemplate
    {
        public string Name => "数据项目";
        public string Category => "通用";
        public string Description => "创建一个包含数据、配置和输出目录的项目";
        public int Order => 3;
        public ImageSource? Icon => null;

        public void CreateProject(string projectDir, string projectName)
        {
            string cvprojPath = Path.Combine(projectDir, projectName + ".cvproj");
            File.WriteAllText(cvprojPath, "{\n  \"Name\": \"" + projectName + "\",\n  \"Version\": \"1.0\"\n}\n");

            Directory.CreateDirectory(Path.Combine(projectDir, "Data"));
            Directory.CreateDirectory(Path.Combine(projectDir, "Config"));
            Directory.CreateDirectory(Path.Combine(projectDir, "Output"));

            string configFile = Path.Combine(projectDir, "Config", "settings.json");
            File.WriteAllText(configFile, "{\n\n}\n");
        }
    }
}
