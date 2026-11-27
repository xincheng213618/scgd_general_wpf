using ColorVision.Solution.Editor.AvalonEditor;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;

namespace ColorVision.UI.Tests;

public sealed class TextEditorCodeAnalysisTests
{
    [Fact]
    public void FoldRefreshKeepsCaseSensitiveUnicodeWordsAndMasksCommentsAndStrings()
    {
        string longIdentifier = new('a', 101);
        var document = new TextDocument($$"""
            class Component
            {
                public int Evaluate(int countValue)
                {
                    int countvalue = countValue;
                    int 计数 = countValue; // IgnoreComment
                    int i = {{longIdentifier}};
                    string text = "IgnoreQuoted";
                    return ResultMaker(countvalue);
                }
            }
            """);
        IHighlightingDefinition definition = HighlightingManager.Instance.GetDefinition("C#");
        var analysis = new EditorCodeAnalysis(definition.Name);

        var foldings = EditorFoldingStrategy.CreateFoldings(document, definition, 4, analysis);
        EditorCodeModel model = analysis.Complete(document);

        Assert.Equal(2, foldings.Count);
        Assert.Equal(new[] { "Component", "Evaluate" }, model.Symbols.Select(symbol => symbol.Name));
        Assert.Single(model.Words.Where(word => word == "countValue"));
        Assert.Contains("countvalue", model.Words);
        Assert.Contains("计数", model.Words);
        Assert.DoesNotContain("i", model.Words);
        Assert.DoesNotContain(longIdentifier, model.Words);
        Assert.DoesNotContain("IgnoreComment", model.Words);
        Assert.DoesNotContain("IgnoreQuoted", model.Words);
    }
}
