using ColorVision.UI.Menus;
using System.IO;

namespace ColorVision.Solution.Explorer
{
    [SolutionMenuContribution(priority: 250)]
    public sealed class ScriptFileMenuContribution : ISolutionMenuContribution
    {
        public string Id => "colorvision.solution.script-file-actions";
        public SolutionMenuSelectionPolicy SelectionPolicy => SolutionMenuSelectionPolicy.SingleOnly;

        public bool IsApplicable(SolutionMenuContext context)
        {
            return context.PrimaryNode is FileNode fileNode
                && ScriptFileSupport.CanRun(fileNode.FileInfo);
        }

        public IEnumerable<MenuItemMetadata> CreateMenuItems(SolutionMenuContext context)
        {
            return
            [
                new MenuItemMetadata
                {
                    GuidId = SolutionResourceCommands.RunScriptId,
                    Order = 0,
                    Header = "运行脚本",
                    Command = SolutionResourceCommands.RunScript,
                    Icon = MenuItemIcon.TryFindResource("DIRun"),
                },
            ];
        }
    }

    [SolutionMenuContribution(priority: 245)]
    public sealed class FileNodeMenuContribution : ISolutionMenuContribution
    {
        public string Id => "colorvision.solution.file-node-actions";
        public SolutionMenuSelectionPolicy SelectionPolicy => SolutionMenuSelectionPolicy.SingleOnly;

        public bool IsApplicable(SolutionMenuContext context)
        {
            return context.PrimaryNode is FileNode;
        }

        public IEnumerable<MenuItemMetadata> CreateMenuItems(SolutionMenuContext context)
        {
            var fileNode = (FileNode)context.PrimaryNode;
            return
            [
                new MenuItemMetadata
                {
                    GuidId = "AskCopilotExplainFile",
                    Order = 20,
                    Header = "问 AI 解释此文件",
                    Command = fileNode.AskCopilotExplainFileCommand,
                },
                new MenuItemMetadata
                {
                    GuidId = "AskCopilotDiagnoseFile",
                    Order = 21,
                    Header = "问 AI 诊断此文件/日志",
                    Command = fileNode.AskCopilotDiagnoseFileCommand,
                },
            ];
        }
    }

    [SolutionMenuContribution(priority: 245)]
    public sealed class FolderNodeMenuContribution : ISolutionMenuContribution
    {
        public string Id => "colorvision.solution.folder-node-actions";
        public SolutionMenuSelectionPolicy SelectionPolicy => SolutionMenuSelectionPolicy.SingleOnly;

        public bool IsApplicable(SolutionMenuContext context)
        {
            return context.PrimaryNode is FolderNode;
        }

        public IEnumerable<MenuItemMetadata> CreateMenuItems(SolutionMenuContext context)
        {
            var folderNode = (FolderNode)context.PrimaryNode;
            return
            [
                new MenuItemMetadata
                {
                    GuidId = "AskCopilotSummarizeFolder",
                    Order = 20,
                    Header = "问 AI 总结此文件夹",
                    Command = folderNode.AskCopilotSummarizeFolderCommand,
                },
            ];
        }
    }

    [SolutionMenuContribution(priority: 240)]
    public sealed class ResourceShellMenuContribution : ISolutionMenuContribution
    {
        public string Id => "colorvision.solution.resource-shell-actions";
        public SolutionMenuSelectionPolicy SelectionPolicy => SolutionMenuSelectionPolicy.SingleOnly;

        public bool IsApplicable(SolutionMenuContext context)
        {
            return SolutionResourceShellPolicy.CanReveal(context.PrimaryNode)
                || SolutionResourceShellPolicy.CanOpenTerminal(context.PrimaryNode);
        }

        public IEnumerable<MenuItemMetadata> CreateMenuItems(SolutionMenuContext context)
        {
            SolutionNode node = context.PrimaryNode;
            var menuItems = new List<MenuItemMetadata>();
            if (SolutionResourceShellPolicy.CanReveal(node))
            {
                menuItems.Add(new MenuItemMetadata
                {
                    GuidId = SolutionResourceCommands.RevealInFileExplorerId,
                    Order = 200,
                    Header = File.Exists(node.ExplorerResourcePath)
                        ? Properties.Resources.MenuOpenContainingFolder
                        : Properties.Resources.MenuOpenFileInExplorer,
                    Command = SolutionResourceCommands.RevealInFileExplorer,
                });
            }
            if (SolutionResourceShellPolicy.CanOpenTerminal(node))
            {
                menuItems.Add(new MenuItemMetadata
                {
                    GuidId = SolutionResourceCommands.OpenInTerminalId,
                    Order = 201,
                    Header = "在终端中打开",
                    Command = SolutionResourceCommands.OpenInTerminal,
                });
            }
            return menuItems;
        }
    }
}
