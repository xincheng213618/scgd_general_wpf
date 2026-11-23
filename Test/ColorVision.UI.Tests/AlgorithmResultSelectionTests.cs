using ColorVision.Engine;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.Engine.Services.Results;
using ColorVision.Engine.Templates.Jsons.KB;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Abstractions;
using ColorVision.Themes;
using ColorVision.UI;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class AlgorithmResultSelectionTests
{
    [Fact]
    public void CorruptResultImageClearsPresentationAndAllowsNextSelection()
    {
        WpfTestHost.Invoke(() =>
        {
            using var fixture = new ResultViewFixture();
            string broken = fixture.WriteInvalidImage("corrupt.png");
            ViewResultAlg first = fixture.AddResult(1, fixture.ValidImage, fixture.ValidImage);
            ViewResultAlg corrupt = fixture.AddResult(2, fixture.ValidImage, broken);
            ViewResultAlg next = fixture.AddResult(3, fixture.ValidImage, fixture.ValidImage);
            fixture.List.SelectedItem = first;
            BitmapSource firstImage = Assert.IsAssignableFrom<BitmapSource>(fixture.View.ImageView.ViewBitmapSource);

            fixture.List.SelectedItem = corrupt;

            fixture.AssertFailure(2);
            fixture.List.SelectedItem = next;
            Assert.NotSame(firstImage, Assert.IsAssignableFrom<BitmapSource>(fixture.View.ImageView.ViewBitmapSource));
            Assert.Same(next.ViewResults, fixture.View.ListView.ItemsSource);
            Assert.Equal(Visibility.Collapsed, fixture.View.SideTextBox.Visibility);
        });
    }

    [Theory]
    [InlineData("unchanged")]
    [InlineData("same-file")]
    [InlineData("new-image")]
    public async Task DelayedImageFailureActsOnlyOnTheSelectionThatOwnsTheRequest(string nextSelection)
    {
        await WpfTestHost.Invoke(async () =>
        {
            using var fixture = new ResultViewFixture();
            string pendingPath = fixture.WriteInvalidImage("pending.cv-history-test");
            var pendingOpener = new FaultableFileOpen();
            fixture.View.ImageView.IEditorToolFactory.IImageOpens[".cv-history-test"] = pendingOpener;

            ViewResultAlg first = fixture.AddResult(1, fixture.ValidImage, fixture.ValidImage);
            ViewResultAlg pending = fixture.AddResult(2, pendingPath);
            fixture.List.SelectedItem = first;
            fixture.List.SelectedItem = pending;
            Task open = fixture.View.ImageView.ImageOpenCompletion;
            Assert.False(open.IsCompleted);
            ViewResultAlg? replacement = null;
            if (nextSelection == "same-file")
            {
                fixture.List.SelectedItem = fixture.AddResult(3, pendingPath);
                Assert.Same(open, fixture.View.ImageView.ImageOpenCompletion);
            }
            else if (nextSelection == "new-image")
            {
                replacement = fixture.AddResult(3, fixture.ValidImage, fixture.ValidImage);
                fixture.List.SelectedItem = replacement;
            }

            pendingOpener.Fail();
            await Assert.ThrowsAsync<InvalidDataException>(() => open);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);

            if (replacement != null)
            {
                Assert.IsAssignableFrom<BitmapSource>(fixture.View.ImageView.ViewBitmapSource);
                Assert.Same(replacement.ViewResults, fixture.View.ListView.ItemsSource);
                Assert.Equal(Visibility.Collapsed, fixture.View.SideTextBox.Visibility);
            }
            else
            {
                fixture.AssertFailure(nextSelection == "unchanged" ? 2 : 3);
            }
        });
    }

    [Theory]
    [InlineData(ViewResultAlgType.POI_Y)]
    [InlineData(ViewResultAlgType.POI_XYZ)]
    [InlineData(ViewResultAlgType.RealPOI)]
    [InlineData(ViewResultAlgType.KB)]
    public async Task UnavailablePointDetailsKeepImageAndRetryBeforeExport(ViewResultAlgType type)
    {
        await WpfTestHost.Invoke(async () =>
        {
            using var fixture = new ResultViewFixture();
            string directory = Path.GetDirectoryName(fixture.ValidImage)!;
            string csvName = type switch
            {
                ViewResultAlgType.POI_Y or ViewResultAlgType.POI_XYZ => $"{type}_batch.csv",
                ViewResultAlgType.KB => "batch_result-KB-15.csv",
                _ => "batch.csv",
            };
            string sideFile = Path.Combine(directory, csvName);
            var first = new PoiPointResultModel { PoiName = "first", PoiWidth = 1, PoiHeight = 1, Value = "{\"X\":1,\"Y\":2,\"Z\":3}" };
            var second = new PoiPointResultModel { PoiName = "second", PoiWidth = 1, PoiHeight = 1, Value = "malformed" };
            IReadOnlyList<PoiPointResultModel>? rows = null;
            int queryCount = 0;
            IReadOnlyList<PoiPointResultModel>? Read(int pid)
            {
                queryCount++;
                return rows;
            }

            IResultHandleBase handler = type switch
            {
                ViewResultAlgType.POI_Y => new ViewHanlePOIY(Read),
                ViewResultAlgType.POI_XYZ => new ViewHanlePOIXZY(Read),
                ViewResultAlgType.RealPOI => new ViewHandleRealPOI(Read),
                _ => new ViewHandleKB(Read),
            };
            var handlers = ResultHandleRegistry.GetInstance().ResultHandles;
            handlers.Insert(0, handler);
            try
            {
                ViewResultAlg result = fixture.AddResult(15, fixture.ValidImage, type == ViewResultAlgType.KB ? fixture.ValidImage : null);
                result.ResultType = type;
                result.Batch = "batch";
                result.ViewResults = null!;

                async Task Select()
                {
                    fixture.List.SelectedItem = null;
                    fixture.List.SelectedItem = result;
                    await fixture.View.ImageView.ImageOpenCompletion;
                    Assert.IsAssignableFrom<BitmapSource>(fixture.View.ImageView.ViewBitmapSource);
                }

                async Task AssertUnavailable()
                {
                    await Select();
                    Assert.Null(result.ViewResults);
                    Assert.Null(fixture.View.ListView.ItemsSource);
                    Assert.Equal(Visibility.Visible, fixture.View.SideTextBox.Visibility);
                    Assert.Contains("15", fixture.View.SideTextBox.Text);
                    Assert.False(AlgorithmResultDataSaver.TrySave(fixture.View.ViewResultContext, result, directory));
                    Assert.False(File.Exists(sideFile));
                    Assert.Equal(new[] { fixture.ValidImage }, Directory.GetFiles(directory));
                }

                await AssertUnavailable();
                Assert.Equal(2, queryCount);

                rows = [first, second];
                if (type is ViewResultAlgType.POI_Y or ViewResultAlgType.POI_XYZ)
                {
                    await AssertUnavailable();
                    Assert.Equal(4, queryCount);
                }

                second.Value = "{\"X\":4,\"Y\":5,\"Z\":6}";
                await Select();
                Assert.Equal(2, result.ViewResults.Count);
                Assert.Same(result.ViewResults, fixture.View.ListView.ItemsSource);
                Assert.Equal(Visibility.Collapsed, fixture.View.SideTextBox.Visibility);
                int afterLoad = queryCount;
                Assert.True(AlgorithmResultDataSaver.TrySave(fixture.View.ViewResultContext, result, directory));
                Assert.Equal(afterLoad, queryCount);
                string csv = File.ReadAllText(sideFile);
                Assert.Contains("first", csv);
                Assert.Contains("second", csv);
                if (type == ViewResultAlgType.KB)
                    Assert.Equal(File.ReadAllBytes(fixture.ValidImage), File.ReadAllBytes(Path.Combine(directory, "valid_result-KB-15.png")));
            }
            finally
            {
                handlers.Remove(handler);
            }
        });
    }

    [Fact]
    public void SuccessfulEmptyPointQueryCanPublishHeaderAndDoesNotRetry()
    {
        WpfTestHost.Invoke(() =>
        {
            using var fixture = new ResultViewFixture();
            string directory = Path.GetDirectoryName(fixture.ValidImage)!;
            int queryCount = 0;
            var handler = new ViewHandleRealPOI(_ => { queryCount++; return []; });
            var handlers = ResultHandleRegistry.GetInstance().ResultHandles;
            handlers.Insert(0, handler);
            try
            {
                ViewResultAlg result = fixture.AddResult(16, fixture.ValidImage);
                result.ResultType = ViewResultAlgType.RealPOI;
                result.Batch = "empty";
                result.ViewResults = null!;
                Assert.True(AlgorithmResultDataSaver.TrySave(fixture.View.ViewResultContext, result, directory));
                Assert.True(AlgorithmResultDataSaver.TrySave(fixture.View.ViewResultContext, result, directory));
                Assert.Empty(result.ViewResults);
                Assert.Equal(1, queryCount);
                Assert.Equal("PoiName,Value", Assert.Single(File.ReadAllLines(Path.Combine(directory, "empty.csv"))));
            }
            finally
            {
                handlers.Remove(handler);
            }
        });
    }

    private sealed class FaultableFileOpen : IAsyncImageOpen
    {
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void OpenImage(EditorContext context, string? filePath) => throw new NotSupportedException();

        public Task OpenImageAsync(EditorContext context, string? filePath, CancellationToken cancellationToken = default)
            => _completion.Task;

        internal void Fail() => _completion.SetException(new InvalidDataException("Synthetic image decode failure"));
    }

    private sealed class ResultViewFixture : IDisposable
    {
        private readonly IConfigService _previousConfig = ConfigService.Instance;
        private readonly List<ResourceDictionary> _dictionaries = [];
        private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("CV-algorithm-result-selection-");

        internal AlgorithmView View { get; }
        internal ListView List { get; }
        internal string ValidImage { get; }

        internal ResultViewFixture()
        {
            try
            {
                TestEnvironment.SetConfig(new ConfigHandler { IsAutoSave = false });
                foreach (string uri in ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase))
                {
                    var dictionary = new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) };
                    _dictionaries.Add(dictionary);
                    Application.Current.Resources.MergedDictionaries.Add(dictionary);
                }

                ValidImage = Path.Combine(_directory.FullName, "valid.png");
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(2, 2, 96, 96, PixelFormats.Gray8, null, new byte[4], 2)));
                using (FileStream file = File.Create(ValidImage))
                    encoder.Save(file);

                View = new AlgorithmView(null, deferInitialization: true);
                View.EnsureInitialized();
                List = (ListView)View.FindName("listView1");
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal string WriteInvalidImage(string fileName)
        {
            string path = Path.Combine(_directory.FullName, fileName);
            File.WriteAllText(path, "This file has no readable pixels.");
            return path;
        }

        internal ViewResultAlg AddResult(int id, string source, string? resultImage = null)
        {
            var result = new ViewResultAlg
            {
                Id = id,
                ResultType = ViewResultAlgType.KB,
                FilePath = source,
                ResultImagFile = resultImage ?? string.Empty,
                ViewResults = [],
            };
            View.ViewResults.Add(result);
            return result;
        }

        internal void AssertFailure(int id)
        {
            Assert.Null(View.ImageView.ViewBitmapSource);
            Assert.Null(View.ImageView.ImageShow.Source);
            Assert.Null(View.ListView.ItemsSource);
            Assert.Empty(Assert.IsType<GridView>(View.ListView.View).Columns);
            Assert.Equal(Visibility.Visible, View.SideTextBox.Visibility);
            Assert.Contains($"ID {id}", View.SideTextBox.Text);
        }

        public void Dispose()
        {
            View?.Dispose();
            foreach (var dictionary in _dictionaries)
                Application.Current.Resources.MergedDictionaries.Remove(dictionary);
            TestEnvironment.SetConfig(_previousConfig);
            foreach (FileInfo file in _directory.EnumerateFiles())
                file.Delete();
            _directory.Delete();
        }
    }
}
