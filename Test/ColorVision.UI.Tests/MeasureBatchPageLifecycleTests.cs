using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Results;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.FOV2;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.ImageEditor;
using ColorVision.Themes;
using Microsoft.VisualBasic.FileIO;
using Newtonsoft.Json;
using SqlSugar;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class MeasureBatchPageLifecycleTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(45)]
    public async Task BoundBatchKeepsItsImagesFovDetailsAndExportAfterDatabaseSwitch(int rowCount)
    {
        DirectoryInfo files = Directory.CreateTempSubdirectory("cv-batch-history-");
        string exportDirectory = files.CreateSubdirectory("export").FullName;
        try
        {
            await WpfTestHost.Invoke(async () =>
            {
                using var resources = new PresentationResources();
                MySqlConnectedEndpoint? active = null;
                MySqlConnectedEndpoint Endpoint(string label) => new(endpoint => ReferenceEquals(active, endpoint), () => new SqlSugarClient(new ConnectionConfig
                {
                    ConnectionString = $"Data Source={Path.Combine(files.FullName, label + ".db")};Pooling=False",
                    DbType = SqlSugar.DbType.Sqlite, InitKeyType = InitKeyType.Attribute, IsAutoCloseConnection = true
                }));
                MySqlConnectedEndpoint original = Endpoint("original");
                MySqlConnectedEndpoint replacement = Endpoint("replacement");
                active = original;
                var selected = Seed(original, "original", width: 3, fov: 17);
                var other = Seed(replacement, "replacement", width: 7, fov: 83);
                Assert.Equal((selected.Batch.Id, selected.ImageId, selected.MasterId), (other.Batch.Id, other.ImageId, other.MasterId));
                active = replacement;
                Assert.False(original.IsCurrent);

                using var page = new MeasureBatchPage(new Frame(), selected.Batch);
                page.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent, page));
                Assert.Equal(Enumerable.Range(selected.ImageId, rowCount), page.ViewResultImages.Select(row => row.Id));
                Assert.Equal(Enumerable.Range(selected.MasterId, rowCount), page.ViewResultAlgs.Select(row => row.Id));
                ViewResultImage image = page.ViewResultImages[0];
                ViewResultAlg algorithm = page.ViewResultAlgs[0];
                Assert.Same(image, Assert.IsType<ListView>(page.FindName("listView1")).SelectedItem);
                Assert.Same(algorithm, Assert.IsType<ListView>(page.FindName("listView2")).SelectedItem);
                Assert.Equal(selected.ImageId, image.Id);
                Assert.Equal(selected.ImageFile, image.FileUrl);
                Assert.Equal(selected.MasterId, algorithm.Id);
                Assert.Equal(selected.Batch.Code, algorithm.Batch);
                Assert.Equal(selected.ImageFile, algorithm.FilePath);
                Assert.Same(original, algorithm.AlgResultMasterModel.DatabaseEndpoint);
                var imagePreview = Preview(page, "imagePreview");
                var algorithmPreview = Preview(page, "algorithmImagePreview");
                await Task.WhenAll(imagePreview.ImageOpenCompletion, algorithmPreview.ImageOpenCompletion).WaitAsync(TimeSpan.FromSeconds(15));
                Assert.Equal(3, Assert.IsAssignableFrom<BitmapSource>(imagePreview.ViewBitmapSource).PixelWidth);
                Assert.Equal(3, Assert.IsAssignableFrom<BitmapSource>(algorithmPreview.ViewBitmapSource).PixelWidth);
                Assert.Equal(17, Assert.IsType<DFovView>(Assert.Single(algorithm.ViewResults)).D_Fov);

                // Exercise the export's own lazy database read after B became current.
                algorithm.ViewResults = null;
                AlgorithmResultDataSaver.Save(new ViewResultContext(), algorithm, exportDirectory);
                string output = Assert.Single(Directory.GetFiles(exportDirectory, "*.csv"));
                using var csv = new TextFieldParser(output);
                csv.SetDelimiters(",");
                string[] header = csv.ReadFields()!;
                int fov = Array.IndexOf(header, nameof(DFovView.D_Fov));
                Assert.True(fov >= 0);
                Assert.Equal("17", csv.ReadFields()![fov]);
                Assert.True(csv.EndOfData);
            });
        }
        finally
        {
            foreach (string output in Directory.EnumerateFiles(exportDirectory)) File.Delete(output);
            Directory.Delete(exportDirectory);
            foreach (FileInfo file in files.EnumerateFiles()) file.Delete();
            files.Delete();
        }

        (MeasureBatchModel Batch, int ImageId, int MasterId, string ImageFile) Seed(
            MySqlConnectedEndpoint endpoint, string label, int width, double fov)
        {
            string imageFile = Path.Combine(files.FullName, label + ".png");
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(new WriteableBitmap(width, 2, 96, 96, PixelFormats.Gray8, null)));
            using (FileStream stream = File.Create(imageFile)) encoder.Save(stream);
            string resultFile = Path.Combine(files.FullName, label + ".json");
            File.WriteAllText(resultFile, JsonConvert.SerializeObject(new ResDFov { result = new CameraFOV { D_Fov = fov } }));

            using var db = endpoint.CreateDbClient();
            db.CodeFirst.InitTables(typeof(MeasureBatchModel), typeof(MeasureResultImgModel), typeof(AlgResultMasterModel), typeof(DetailCommonModel));
            var batch = new MeasureBatchModel { Code = "shared-code", Name = label };
            batch.Id = db.Insertable(batch).ExecuteReturnIdentity();
            batch.DatabaseEndpoint = endpoint;
            int imageId = db.Insertable(new MeasureResultImgModel { BatchId = batch.Id, FileUrl = imageFile }).ExecuteReturnIdentity();
            int masterId = db.Insertable(new AlgResultMasterModel
            {
                BatchId = batch.Id, ImgFileType = ViewResultAlgType.FOV, version = "2.0", ImgFile = imageFile, CreateDate = DateTime.Now
            }).ExecuteReturnIdentity();
            db.Insertable(new DetailCommonModel
            {
                PId = masterId, ResultJson = JsonConvert.SerializeObject(new ResultFile { ResultFileName = resultFile })
            }).ExecuteCommand();
            if (rowCount > 1)
            {
                db.Insertable(Enumerable.Range(1, rowCount - 1)
                    .Select(_ => new MeasureResultImgModel { BatchId = batch.Id, FileUrl = imageFile }).ToList()).ExecuteCommand();
                db.Insertable(Enumerable.Range(1, rowCount - 1)
                    .Select(_ => new AlgResultMasterModel { BatchId = batch.Id, ImgFile = imageFile, CreateDate = DateTime.Now }).ToList()).ExecuteCommand();
            }
            return (batch, imageId, masterId, imageFile);
        }
    }

    [Fact]
    public void CorruptAlgorithmResultClearsBatchDetailsAndAllowsAnotherSelection()
    {
        WpfTestHost.Invoke(() =>
        {
            using var resources = new PresentationResources();
            using var page = CreateOfflinePage(new Frame());
            var rows = Assert.IsType<ListView>(page.FindName("listView2"));
            var details = Assert.IsType<ListView>(page.FindName("algorithmDetailListView"));
            var message = Assert.IsType<TextBox>(page.FindName("algorithmDetailTextBox"));
            var image = Preview(page, "algorithmImagePreview");
            DirectoryInfo files = Directory.CreateTempSubdirectory("ColorVision-batch-result-test-");
            string goodFile = Path.Combine(files.FullName, "good.png");
            string corruptFile = Path.Combine(files.FullName, "corrupt.png");
            try
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(new WriteableBitmap(2, 2, 96, 96, PixelFormats.Gray8, null)));
                using (FileStream file = File.Create(goodFile)) encoder.Save(file);
                File.WriteAllText(corruptFile, "truncated image");
                ViewResultAlg valid = new() { Id = 1, ResultType = ViewResultAlgType.KB, FilePath = goodFile, ResultImagFile = goodFile, ViewResults = [] };
                ViewResultAlg corrupt = new() { Id = 2, ResultType = ViewResultAlgType.KB, FilePath = goodFile, ResultImagFile = corruptFile, ViewResults = [] };
                page.ViewResultAlgs.Add(valid);
                page.ViewResultAlgs.Add(corrupt);

                rows.SelectedItem = valid;
                Assert.IsAssignableFrom<BitmapSource>(image.ViewBitmapSource);
                Assert.Same(valid.ViewResults, details.ItemsSource);

                rows.SelectedItem = corrupt;
                Assert.Null(image.ViewBitmapSource);
                Assert.Null(details.ItemsSource);
                Assert.Empty(Assert.IsType<GridView>(details.View).Columns);
                Assert.Equal(Visibility.Visible, message.Visibility);
                Assert.Contains("ID 2", message.Text);

                rows.SelectedItem = valid;
                Assert.IsAssignableFrom<BitmapSource>(image.ViewBitmapSource);
                Assert.Same(valid.ViewResults, details.ItemsSource);
                Assert.Equal(Visibility.Collapsed, message.Visibility);

                rows.SelectedIndex = -1;
                Assert.Null(image.ViewBitmapSource);
                Assert.Null(details.ItemsSource);
            }
            finally
            {
                File.Delete(corruptFile);
                File.Delete(goodFile);
                files.Delete();
            }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClosingHostDisposesCurrentAndJournaledPreviews(bool navigateBeforeClose)
    {
        WpfTestHost.Invoke(() =>
        {
            using var resources = new PresentationResources();
            Window? previousMainWindow = Application.Current.MainWindow;
            var frame = new Frame();
            var window = CreateWindow(frame);
            var page = CreateOfflinePage(frame);
            var image = Preview(page, "imagePreview");
            var algorithm = Preview(page, "algorithmImagePreview");
            List<ImageView> previews = [image, algorithm];
            try
            {
                frame.Navigate(page);
                window.Show();
                PumpDispatcher();
                Assert.Same(page, frame.Content);
                SetPreviewImages(image, algorithm);

                if (navigateBeforeClose)
                {
                    var nextPage = CreateOfflinePage(frame);
                    var nextImage = Preview(nextPage, "imagePreview");
                    var nextAlgorithm = Preview(nextPage, "algorithmImagePreview");
                    previews.AddRange([nextImage, nextAlgorithm]);
                    frame.Navigate(nextPage);
                    PumpDispatcher();
                    Assert.Same(nextPage, frame.Content);
                    SetPreviewImages(nextImage, nextAlgorithm);
                    Assert.Null(image.ViewBitmapSource);
                    Assert.Null(algorithm.ViewBitmapSource);
                    using (image.RegisterSettingsProvider(() => [])) { }
                    using (algorithm.RegisterSettingsProvider(() => [])) { }

                    frame.GoBack();
                    PumpDispatcher();
                    Assert.Same(page, frame.Content);
                    SetPreviewImages(image, algorithm);
                    frame.GoForward();
                    PumpDispatcher();
                    Assert.Same(nextPage, frame.Content);
                    SetPreviewImages(nextImage, nextAlgorithm);
                }

                window.Close();
                PumpDispatcher();
                Assert.All(previews, preview =>
                {
                    Assert.Null(preview.ViewBitmapSource);
                    Assert.Throws<ObjectDisposedException>(() => preview.RegisterSettingsProvider(() => []));
                });
            }
            finally
            {
                window.Close();
                foreach (ImageView preview in previews) preview.Dispose();
                PumpDispatcher();
                Application.Current.MainWindow = previousMainWindow;
            }
        });
    }

    private static MeasureBatchPage CreateOfflinePage(Frame frame)
    {
        var page = new MeasureBatchPage(frame, new MeasureBatchModel { Code = "lifecycle-test" });
        // Exercise real WPF navigation/closure without querying the production MySQL database.
        var databaseLoad = typeof(MeasureBatchPage).GetMethod("Page_Loaded", BindingFlags.Instance | BindingFlags.NonPublic)!;
        page.Loaded -= (RoutedEventHandler)databaseLoad.CreateDelegate(typeof(RoutedEventHandler), page);
        return page;
    }

    private static ImageView Preview(MeasureBatchPage page, string name) => Assert.IsType<ImageView>(page.FindName(name));

    private static void SetPreviewImages(ImageView image, ImageView algorithm)
    {
        image.SetImageSource(new WriteableBitmap(64, 32, 96, 96, PixelFormats.Gray8, null));
        algorithm.SetImageSource(new WriteableBitmap(64, 32, 96, 96, PixelFormats.Gray8, null));
        using var imageFrame = image.AcquireImageFrame();
        using var algorithmFrame = algorithm.AcquireImageFrame();
        Assert.NotNull(imageFrame);
        Assert.NotNull(algorithmFrame);
    }

    private static Window CreateWindow(Frame frame) => new()
    {
        Content = frame,
        Width = 900,
        Height = 600,
        ShowActivated = false,
        ShowInTaskbar = false,
        WindowStartupLocation = WindowStartupLocation.Manual,
        Left = -10000,
        Top = -10000,
    };

    private static void PumpDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private sealed class PresentationResources : IDisposable
    {
        private readonly List<ResourceDictionary> _dictionaries = [];

        public PresentationResources()
        {
            foreach (string uri in ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase))
            {
                var dictionary = new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) };
                _dictionaries.Add(dictionary);
                Application.Current.Resources.MergedDictionaries.Add(dictionary);
            }
        }

        public void Dispose()
        {
            foreach (var dictionary in _dictionaries) Application.Current.Resources.MergedDictionaries.Remove(dictionary);
        }
    }
}
