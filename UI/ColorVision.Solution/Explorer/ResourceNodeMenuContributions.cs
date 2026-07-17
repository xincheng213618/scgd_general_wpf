using ColorVision.UI.Menus;
using ColorVision.Solution.FileMeta;

namespace ColorVision.Solution.Explorer
{
    [SolutionMenuContribution(priority: 250)]
    public sealed class ScriptFileMenuContribution : ISolutionMenuContribution
    {
        public string Id => "colorvision.solution.script-file-actions";
        public SolutionMenuSelectionPolicy SelectionPolicy => SolutionMenuSelectionPolicy.SingleOnly;

        public bool IsApplicable(SolutionMenuContext context)
        {
            return context.PrimaryNode is FileNode
            {
                FileMeta: IScriptFileMeta,
                FileInfo.Exists: true,
            };
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
                new MenuItemMetadata
                {
                    GuidId = "OpenContainingFolder",
                    Order = 200,
                    Header = Properties.Resources.MenuOpenContainingFolder,
                    Command = fileNode.OpenContainingFolderCommand,
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
                new MenuItemMetadata
                {
                    GuidId = "Fusion",
                    Order = 50,
                    Header = "景深融合(_F)",
                    Command = folderNode.OpenFusionCommand,
                },
                new MenuItemMetadata
                {
                    GuidId = "MenuOpenFileInExplorer",
                    Order = 200,
                    Header = Properties.Resources.MenuOpenFileInExplorer,
                    Command = folderNode.OpenFileInExplorerCommand,
                },
                new MenuItemMetadata
                {
                    GuidId = "OpenInCmdCommad",
                    Order = 200,
                    Header = "在终端中打开",
                    Command = folderNode.OpenInCmdCommand,
                },
            ];
        }
    }
}
