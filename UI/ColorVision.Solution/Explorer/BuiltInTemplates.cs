using System.Windows.Media;

namespace ColorVision.Solution.Explorer
{
    [NewItemTemplate(0)]
    public class PythonScriptTemplate : INewItemTemplate
    {
        public string Name => "Python 脚本";
        public string Category => "Script";
        public string? Extension => ".py";
        public int Order => 1;
        public ImageSource? Icon => null;

        public string? GetDefaultContent(string fileName)
        {
            string moduleName = System.IO.Path.GetFileNameWithoutExtension(fileName);
            return "#!/usr/bin/env python3\n" +
                   "# -*- coding: utf-8 -*-\n" +
                   $"\"\"\"\n{moduleName}\n\"\"\"\n\n\n" +
                   "def main():\n" +
                   "    pass\n\n\n" +
                   "if __name__ == \"__main__\":\n" +
                   "    main()\n";
        }

        public string? GetDefaultFileName() => "script";
    }

    [NewItemTemplate(0)]
    public class PowerShellScriptTemplate : INewItemTemplate
    {
        public string Name => "PowerShell 脚本";
        public string Category => "Script";
        public string? Extension => ".ps1";
        public int Order => 2;
        public ImageSource? Icon => null;

        public string? GetDefaultContent(string fileName)
        {
            return "#Requires -Version 5.1\n" +
                   "[CmdletBinding()]\n" +
                   "param()\n\n" +
                   "# Script entry point\n";
        }

        public string? GetDefaultFileName() => "script";
    }

    [NewItemTemplate(0)]
    public class BatchScriptTemplate : INewItemTemplate
    {
        public string Name => "批处理脚本";
        public string Category => "Script";
        public string? Extension => ".bat";
        public int Order => 3;
        public ImageSource? Icon => null;

        public string? GetDefaultContent(string fileName)
        {
            return "@echo off\n" +
                   "chcp 65001 >nul\n" +
                   "setlocal enabledelayedexpansion\n\n" +
                   ":: Script entry point\n\n" +
                   "endlocal\n";
        }

        public string? GetDefaultFileName() => "script";
    }

    [NewItemTemplate(0)]
    public class JsonConfigTemplate : INewItemTemplate
    {
        public string Name => "JSON 配置";
        public string Category => "Config";
        public string? Extension => ".json";
        public int Order => 1;
        public ImageSource? Icon => null;

        public string? GetDefaultContent(string fileName)
        {
            return "{\n\n}\n";
        }

        public string? GetDefaultFileName() => "config";
    }

    [NewItemTemplate(0)]
    public class TextFileTemplate : INewItemTemplate
    {
        public string Name => "文本文件";
        public string Category => "General";
        public string? Extension => ".txt";
        public int Order => 1;
        public ImageSource? Icon => null;

        public string? GetDefaultContent(string fileName) => "";

        public string? GetDefaultFileName() => "new_file";
    }
}
