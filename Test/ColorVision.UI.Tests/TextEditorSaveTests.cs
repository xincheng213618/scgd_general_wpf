using ColorVision.Solution.Editor.AvalonEditor;
using ICSharpCode.AvalonEdit;
using System.IO;
using System.Text;
using System.Windows;

namespace ColorVision.UI.Tests;

public sealed class TextEditorSaveTests
{
    [Fact]
    public void SavePreservesAnExternalEditWithTheSameLengthAndWriteTime()
    {
        WpfTestHost.Invoke(() =>
        {
            DirectoryInfo directory = Directory.CreateTempSubdirectory("ColorVision-TextConflict-");
            string path = Path.Combine(directory.FullName, "source.txt");
            const string original = "original";
            const string external = "external";
            const string draft = "my draft";
            var theme = new ResourceDictionary { Source = new Uri("/ColorVision.Themes;component/Themes/Theme.xaml", UriKind.Relative) };
            Application.Current.Resources.MergedDictionaries.Add(theme);
            try
            {
                File.WriteAllText(path, original);
                DateTime stamp = File.GetLastWriteTimeUtc(path);
                using AvalonEditControll editor = new(path);
                TextEditor text = Assert.IsType<TextEditor>(editor.FindName("textEditor"));
                text.Document.Replace(0, text.Document.TextLength, draft);
                File.WriteAllText(path, external);
                File.SetLastWriteTimeUtc(path, stamp);
                Assert.Equal(stamp, File.GetLastWriteTimeUtc(path));

                Assert.Throws<IOException>(() => editor.Save());
                Assert.Equal(external, File.ReadAllText(path));
                Assert.Equal(draft, text.Text);
                Assert.True(editor.IsDirty);
                Assert.Single(directory.EnumerateFiles());

                Assert.True(editor.ReloadFromDisk());
                Assert.Equal(external, text.Text);
                text.Document.Insert(text.Document.TextLength, " with revision");
                Assert.True(editor.Save());
                text.Document.Insert(text.Document.TextLength, " and follow-up");
                Assert.True(editor.Save());
                Assert.Equal("external with revision and follow-up", File.ReadAllText(path));
                Assert.False(editor.IsDirty);
                Assert.Single(directory.EnumerateFiles());
            }
            finally
            {
                Application.Current.Resources.MergedDictionaries.Remove(theme);
                foreach (FileInfo file in directory.EnumerateFiles()) file.Delete();
                directory.Delete();
            }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedEncodingKeepsTheFileAndDirtyDocumentForRetry(bool utf16)
    {
        WpfTestHost.Invoke(() =>
        {
            DirectoryInfo directory = Directory.CreateTempSubdirectory("ColorVision-TextSave-");
            string path = Path.Combine(directory.FullName, "source.txt");
            Encoding encoding = utf16 ? new UnicodeEncoding(false, true, true) : new UTF8Encoding(false, true);
            const string originalText = "original 文档\r\n";
            byte[] original = Encode(originalText);
            var theme = new ResourceDictionary { Source = new Uri("/ColorVision.Themes;component/Themes/Theme.xaml", UriKind.Relative) };
            Application.Current.Resources.MergedDictionaries.Add(theme);
            try
            {
                File.WriteAllBytes(path, original);
                using AvalonEditControll editor = new(path);
                TextEditor text = Assert.IsType<TextEditor>(editor.FindName("textEditor"));
                Assert.Equal(originalText, text.Text);
                text.Encoding = encoding;
                text.Document.Insert(text.Document.TextLength, new string('a', 16_384) + "\ud800");

                Assert.True(editor.IsDirty);
                Assert.Throws<EncoderFallbackException>(() => editor.Save());
                Assert.Equal(original, File.ReadAllBytes(path));
                Assert.True(editor.IsDirty);
                Assert.Single(directory.EnumerateFiles());

                const string repairedText = "repaired 文档\r\n";
                text.Document.Replace(0, text.Document.TextLength, repairedText);
                using (File.OpenRead(path))
                {
                    Assert.Null(File.ResolveLinkTarget(path, returnFinalTarget: true));
                    Assert.True(Record.Exception(() => editor.Save()) is IOException or UnauthorizedAccessException);
                    Assert.Equal(original, File.ReadAllBytes(path));
                    Assert.True(editor.IsDirty);
                    Assert.Single(directory.EnumerateFiles());
                }
                Assert.True(editor.Save());
                Assert.Equal(Encode(repairedText), File.ReadAllBytes(path));
                Assert.False(editor.IsDirty);
                Assert.False(text.IsModified);
                Assert.Single(directory.EnumerateFiles());
            }
            finally
            {
                Application.Current.Resources.MergedDictionaries.Remove(theme);
                foreach (FileInfo file in directory.EnumerateFiles()) file.Delete();
                directory.Delete();
            }

            byte[] Encode(string value) => [.. encoding.GetPreamble(), .. encoding.GetBytes(value)];
        });
    }
}
