using ColorVision.Solution.Explorer;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class SolutionBatchDeleteTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void PartialRecycleKeepsOnlyTheItemsThatStillExist(int shellResult)
    {
        string root = Directory.CreateTempSubdirectory("ColorVision-recycle-").FullName;
        string removedPath = Path.Combine(root, "removed.txt");
        string retainedPath = Path.Combine(root, "retained.txt");
        string missingPath = Path.Combine(root, "already-missing.txt");
        string recycledPath = Path.Combine(root, "recycled.txt");
        try
        {
            File.WriteAllText(removedPath, "removed");
            File.WriteAllText(retainedPath, "keep");
            var tree = new SolutionNode();
            var removed = new FileNode(new FileInfo(removedPath));
            var retained = new FileNode(new FileInfo(retainedPath));
            var missing = new FileNode(new FileInfo(missingPath));
            tree.AddChild(removed);
            tree.AddChild(retained);
            tree.AddChild(missing);

            string[] submitted = [];
            IReadOnlyList<SolutionNode> failures = SolutionBatchDeleteService.Delete([removed, retained, missing], paths =>
            {
                submitted = paths;
                File.Move(removedPath, recycledPath);
                return shellResult;
            });

            Assert.Equal([removedPath, retainedPath], submitted);
            Assert.Same(retained, Assert.Single(failures));
            Assert.Same(retained, Assert.Single(tree.VisualChildren));
            Assert.Equal("keep", File.ReadAllText(retainedPath));
            Assert.Equal("removed", File.ReadAllText(recycledPath));
        }
        finally
        {
            File.Delete(removedPath);
            File.Delete(retainedPath);
            File.Delete(recycledPath);
            Directory.Delete(root);
        }
    }
}
