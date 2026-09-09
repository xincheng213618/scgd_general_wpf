using ColorVision.Engine.Media;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Services.Types;
using ColorVision.Engine;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.Services.PhyCameras.Calibration;
using ColorVision.Engine.Services.POI;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.POI;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

[Collection(AssemblyDiscoveryCollection.CollectionName)]
public sealed class LumFourColorWorkflowSafetyTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(29.9, true)]
    [InlineData(30, false)]
    [InlineData(60, false)]
    [InlineData(95, false)]
    [InlineData(95.1, true)]
    [InlineData(100, true)]
    public void IpUsesTheDocumentedInclusiveThirtyToNinetyFivePercentRange(double percent, bool warning)
    {
        Assert.Equal(warning, LumFourColorDataChecks.SpectrumWarning(percent * 65535 / 100) != null);
    }

    [Fact]
    public void MissingIpIsUnverifiedAndImpossiblePeakAdIsRejected()
    {
        Assert.NotNull(LumFourColorDataChecks.SpectrumWarning(null));
        foreach (double invalid in new[] { -1d, 65536d, double.NaN, double.PositiveInfinity })
            Assert.Throws<InvalidOperationException>(() => LumFourColorDataChecks.SpectrumWarning(invalid));
    }

    [Fact]
    public void SpectrumCapturePreservesRawValuesQualityAndSourceMetadata()
    {
        var source = new SpectrumColorMeasurement(71, DateTimeOffset.UnixEpoch, -3, -0.2, -0.4,
            new[] { new SpectrumValuePoint(380, -0.1), new SpectrumValuePoint(381, 0.4) })
        { PeakAd = 32767.5, IntegrationTime = 100, NdPort = 2, DeviceCode = "SP-Test" };
        var sample = new LumFourColorCalibrationSample(LumFourColorCorrectionTarget.Red);
        sample.SetSpectrumMeasurement(LumFourColorSpectrumCapture.FromMeasurement(source));
        Assert.Equal(-3, sample.ReferenceY);
        Assert.Equal(-0.1, sample.Spectrum[0].Value);
        Assert.Equal(50, sample.SpectrumIpPercent);
        Assert.Equal(100, sample.SpectrumIntegrationTime);
        Assert.Equal(2, sample.SpectrumNdPort);
        Assert.Equal("SP-Test", sample.SpectrumSource);
        Assert.Equal(DateTimeOffset.UnixEpoch, sample.SpectrumCapturedAt);
        sample.SetSpectrumMeasurement(LumFourColorSpectrumCapture.FromMeasurement(source with { CapturedAt = default }));
        Assert.Null(sample.SpectrumCapturedAt);
        Assert.Contains(sample.GetWarnings("source"), warning => warning.Contains("采集时间", StringComparison.Ordinal));
    }

    [Fact]
    public void InvalidReplacementNeverLeavesThePreviousSpectrumUsable()
    {
        var sample = new LumFourColorCalibrationSample(LumFourColorCorrectionTarget.Red);
        sample.SetSpectrumMeasurement(Spectrum(1));
        Assert.Throws<InvalidOperationException>(() => sample.SetSpectrumMeasurement(Spectrum(2) with
        { Measurement = new ColorCorrectionYxy(1, 0.2, 0) }));
        Assert.False(sample.HasSpectrumMeasurement);
        Assert.Null(sample.SpectrumResultId);
        Assert.Empty(sample.Spectrum);

        sample.SetSpectrumMeasurement(Spectrum(1));
        Assert.Throws<InvalidOperationException>(() => sample.SetSpectrumMeasurement(Spectrum(2) with
        { Spectrum = new[] { new ColorCorrectionSpectrumPoint(380, 1), new ColorCorrectionSpectrumPoint(380, 2) } }));
        Assert.False(sample.HasSpectrumMeasurement);
    }

    [Fact]
    public void SpectrumPointsAreCopiedAndFiniteNegativeCameraDataSurvives()
    {
        var points = new[] { new ColorCorrectionSpectrumPoint(380, -0.01) };
        var sample = new LumFourColorCalibrationSample(LumFourColorCorrectionTarget.SinglePoint);
        sample.SetSpectrumMeasurement(Spectrum(1) with { Spectrum = points });
        points[0] = new ColorCorrectionSpectrumPoint(380, double.NaN);
        sample.SetCameraMeasurement(new(0, 0, 1, 1, PoiMeasurementShape.Rect), new(-1, -2, -3, -0.25f, -0.5f, 0, 0, 0, 0));
        Assert.Equal(-0.01, sample.Spectrum[0].Value);
        Assert.Equal(-2, sample.CreateMeasurement().Camera.Y);
        Assert.Throws<InvalidOperationException>(() => sample.SetCameraMeasurement(new(0, 0, 1, 1, PoiMeasurementShape.Rect), new(1, 2, 3, 0.2f, 0, 0, 0, 0, 0)));
        Assert.False(sample.HasCameraMeasurement);
    }

    [Fact]
    public void ImportedImagesAndMismatchedCalibrationAreNeverReportedAsVerified()
    {
        var sample = new LumFourColorCalibrationSample(LumFourColorCorrectionTarget.SinglePoint);
        sample.SetSpectrumMeasurement(Spectrum(1));
        sample.SetFrame(Frame(null), null!);
        Assert.Single(sample.GetWarnings("current"));
        sample.SetFrame(Frame("different"), null!);
        Assert.Single(sample.GetWarnings("current"));
        sample.SetFrame(Frame("current"), null!);
        Assert.Empty(sample.GetWarnings("current"));
    }

    [Fact]
    public void DifferentColorBlocksCannotReuseOneSpectrumResult()
    {
        var session = new LumFourColorCalibrationSession();
        session.SetMode(LumFourColorCorrectionMode.MatlabRgbw);
        foreach (var sample in session.Samples)
            FillSample(sample, "source", 123);
        Assert.Contains("同一条光谱", Assert.Throws<InvalidOperationException>(() => session.Calculate(Identity())).Message);
    }

    [Fact]
    public void SavingProtectsSourceAndDetectsEditsEvenWhenLengthAndTimestampAreUnchanged()
    {
        string original = Path.GetTempFileName(), destination = Path.GetTempFileName();
        try
        {
            WriteSource(original);
            var snapshot = LumFourColorSourceSnapshot.Load(original);
            string originalText = File.ReadAllText(original);
            Assert.Throws<InvalidOperationException>(() => snapshot.SaveCopy(original, Identity()));
            Assert.Equal(originalText, File.ReadAllText(original));
            snapshot.SaveCopy(destination, Identity());
            Assert.True(CVRawManualCieCalculator.TryLoadLumFourColorCalibrationDefaults(destination, out _, out _));
            string saved = File.ReadAllText(destination);
            var stamp = File.GetLastWriteTimeUtc(original);
            File.WriteAllText(original, originalText.Replace("\"a\": 1.0", "\"a\": 2.0"));
            Assert.NotEqual(originalText, File.ReadAllText(original));
            File.SetLastWriteTimeUtc(original, stamp);
            Assert.Throws<InvalidOperationException>(() => snapshot.SaveCopy(destination, Identity()));
            Assert.Equal(saved, File.ReadAllText(destination));
        }
        finally { File.Delete(original); File.Delete(destination); }
    }

    [Fact]
    public void ManualInputEditsImmediatelyInvalidateResultAndConfirmation()
    {
        string path = Path.GetTempFileName();
        WriteSource(path);
        try
        {
            WithTheme(() =>
            {
                var window = new LumFourColorCorrectionWindow(path);
                try
                {
                    Control<RadioButton>(window, "SinglePointMode").IsChecked = true;
                    var grid = Control<DataGrid>(window, "MeasurementsGrid");
                    var row = Assert.IsType<CorrectionMeasurementRow>(grid.Items[0]);
                    row.CameraY = row.ReferenceY = "2";
                    row.CameraX = row.ReferenceX = "0.2";
                    row.CameraYChromaticity = row.ReferenceYChromaticity = "0.3";
                    Control<CheckBox>(window, "ManualDataConfirmed").IsChecked = true;
                    Invoke(window, "Calculate_Click", window, new RoutedEventArgs());
                    Assert.True(Control<Button>(window, "SaveButton").IsEnabled);
                    row.CameraY = "3";
                    Assert.False(Control<Button>(window, "SaveButton").IsEnabled);
                    Assert.False(Control<CheckBox>(window, "ManualDataConfirmed").IsChecked);
                    Assert.Empty(Control<TextBox>(window, "ResultPreview").Text);
                    Render(window, 1076, 576, "manual-input");
                }
                finally { window.Close(); }
            });
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void WorkflowInvalidatesFailedCalculationsAndNewSourceClearsOnlyCameraData()
    {
        string path = Path.GetTempFileName(), otherPath = Path.GetTempFileName();
        WriteSource(path); WriteSource(otherPath);
        try
        {
            WithTheme(() =>
            {
                var window = new LumFourColorCalibrationWorkflowWindow(Array.Empty<DeviceCamera>(), Array.Empty<DeviceSpectrum>(), path);
                try
                {
                    Control<RadioButton>(window, "SinglePointMode").IsChecked = true;
                    var sample = Assert.IsType<LumFourColorCalibrationSample>(Control<ListBox>(window, "SampleList").Items[0]);
                    FillSample(sample, LumFourColorSourceSnapshot.ComputeHash(path), 1);
                    Click(window, "CalculateButton");
                    Assert.True(Control<Button>(window, "SaveButton").IsEnabled);
                    File.WriteAllText(path, "{}");
                    Click(window, "CalculateButton");
                    Assert.False(Control<Button>(window, "SaveButton").IsEnabled);
                    Assert.Contains("修改", Control<TextBlock>(window, "StatusText").Text);
                    Control<TextBox>(window, "SourcePathBox").Text = otherPath;
                    Assert.False(sample.HasCameraMeasurement);
                    Assert.False(sample.HasImage);
                    Assert.True(sample.HasSpectrumMeasurement);
                    Assert.False(Control<Button>(window, "CalculateButton").IsEnabled);
                }
                finally { window.Close(); }
            });
        }
        finally { File.Delete(path); File.Delete(otherPath); }
    }

    [Fact]
    public void SelectionWindowRequiresExplicitSelectionAndWorkflowRendersAtSupportedSizes()
    {
        WithTheme(() =>
        {
            var picker = new LumFourColorSpectrumSelectionWindow("测试光谱仪", "R", new[]
            { new SpectrumColorMeasurementSummary { ResultId = 1, CapturedAt = DateTime.Today, Y = 2, CieX = 0.2f, CieY = 0.3f, PeakAd = 32767.5f } });
            var window = new LumFourColorCalibrationWorkflowWindow(Array.Empty<DeviceCamera>(), Array.Empty<DeviceSpectrum>());
            try
            {
                Assert.Null(picker.SelectedResult);
                Assert.False(Control<Button>(picker, "UseButton").IsEnabled);
                Control<DataGrid>(picker, "ResultsGrid").SelectedIndex = 0;
                Assert.True(Control<Button>(picker, "UseButton").IsEnabled);
                Render(window, 1176, 736, "workflow-empty");
                var sample = Assert.IsType<LumFourColorCalibrationSample>(Control<ListBox>(window, "SampleList").Items[0]);
                FillSample(sample, null, 1);
                sample.SetSpectrumMeasurement(Spectrum(1) with { PeakAd = 12000 });
                Invoke(window, "RefreshSelectedSample");
                Render(window, 976, 636, "workflow-minimum-warning");
                Render(picker, 816, 436, "spectrum-picker");
            }
            finally { picker.Close(); window.Close(); }
        });
        WithTheme(() =>
        {
            var window = new LumFourColorCalibrationWorkflowWindow(Array.Empty<DeviceCamera>(), Array.Empty<DeviceSpectrum>());
            try
            {
                var sample = Assert.IsType<LumFourColorCalibrationSample>(Control<ListBox>(window, "SampleList").Items[0]);
                FillSample(sample, null, 1);
                sample.SetSpectrumMeasurement(Spectrum(1) with { PeakAd = 12000 });
                Invoke(window, "RefreshSelectedSample");
                Render(window, 1176, 736, "workflow-dark-warning");
            }
            finally { window.Close(); }
        }, true);
    }

    [Fact]
    public void ManualReferenceWithoutInstrumentOrWaveformCanCalculate()
    {
        var session = new LumFourColorCalibrationSession();
        session.SetMode(LumFourColorCorrectionMode.SinglePoint);
        var sample = session.Samples[0];
        sample.SetCameraMeasurement(new(0, 0, 1, 1, PoiMeasurementShape.Rect), new(1, 2, 3, 0.2f, 0.3f, 0, 0, 0, 0));
        sample.ReferenceYInput = "4";
        sample.ReferenceCieXInput = sample.CameraCieX!.Value.ToString("G17", System.Globalization.CultureInfo.CurrentCulture);
        Assert.False(sample.HasSpectrumMeasurement);
        sample.ReferenceCieYInput = sample.CameraCieY!.Value.ToString("G17", System.Globalization.CultureInfo.CurrentCulture);
        Assert.True(session.IsComplete);
        Assert.True(sample.IsReferenceEdited);
        Assert.Null(sample.SpectrumResultId);
        Assert.Null(sample.SpectrumPeakAd);
        Assert.False(sample.CanRestoreReference);
        Assert.Empty(sample.Spectrum);
        Assert.Null(sample.CreateMeasurement().Spectrum);
        Assert.Contains(sample.GetWarnings("source"), warning => warning.Contains("手动"));
        var corrected = session.Calculate(Identity());
        Assert.Equal(2, corrected.A);
        Assert.Equal(2, corrected.E);
        Assert.Equal(2, corrected.I);
    }

    [Fact]
    public void EditingReferenceRetainsOriginalRecordAndCanRestoreOrReacquire()
    {
        var sample = new LumFourColorCalibrationSample(LumFourColorCorrectionTarget.Red);
        FillSample(sample, "source", 42);
        var original = sample.Spectrum;
        sample.ReferenceYInput = "-2";
        Assert.True(sample.HasSpectrumMeasurement);
        Assert.True(sample.IsReferenceEdited);
        Assert.True(sample.CanRestoreReference);
        Assert.Equal(-2, sample.ReferenceY);
        Assert.Equal(42, sample.SpectrumResultId);
        Assert.Same(original, sample.Spectrum);
        Assert.Equal(32767.5, sample.SpectrumPeakAd);
        Assert.Contains("手动", sample.SpectrumQuality);
        Assert.DoesNotContain("合格", sample.SpectrumState);
        Assert.Null(sample.CreateMeasurement().Spectrum);
        sample.ReferenceCieYInput = "0";
        Assert.False(sample.HasSpectrumMeasurement);
        Assert.Null(sample.ReferenceY);
        Assert.Contains("不能为 0", sample.ReferenceInputError);
        sample.RestoreReference();
        Assert.False(sample.IsReferenceEdited);
        Assert.False(sample.CanRestoreReference);
        Assert.True(sample.HasSpectrumMeasurement);
        Assert.Equal(2, sample.ReferenceY);
        Assert.Equal(0.3, sample.ReferenceCieY);
        Assert.Same(original, sample.CreateMeasurement().Spectrum);
        Assert.Empty(sample.GetWarnings("source"));
        sample.ReferenceYInput = "7";
        sample.SetSpectrumMeasurement(Spectrum(43));
        Assert.False(sample.IsReferenceEdited);
        Assert.Equal("2", sample.ReferenceYInput);
        Assert.Equal(43, sample.SpectrumResultId);
        sample.ReferenceYInput = "9";
        sample.ClearSpectrum();
        Assert.False(sample.IsReferenceEdited);
        Assert.False(sample.CanRestoreReference);
        Assert.Equal("", sample.ReferenceYInput);
        sample.RestoreReference();
        Assert.False(sample.HasSpectrumMeasurement);
    }

    [Fact]
    public void ManuallyOverriddenReferencesDoNotReuseOriginalWaveformsOrBlockOnOriginalIds()
    {
        var session = new LumFourColorCalibrationSession();
        session.SetMode(LumFourColorCorrectionMode.MatlabRgbw);
        var colors = new[] { (2f, 0.6f, 0.3f), (3f, 0.25f, 0.6f), (1f, 0.15f, 0.06f), (4f, 0.3f, 0.3f) };
        for (int i = 0; i < session.Samples.Count; i++)
        {
            var sample = session.Samples[i];
            FillSample(sample, "source", 42);
            var (y, x, cy) = colors[i];
            sample.SetCameraMeasurement(new(0, 0, 1, 1, PoiMeasurementShape.Rect), new(1, y, 3, x, cy, 0, 0, 0, 0));
            sample.ReferenceYInput = ((double)y).ToString("R");
            sample.ReferenceCieXInput = ((double)x).ToString("R");
            sample.ReferenceCieYInput = ((double)cy).ToString("R");
            Assert.True(sample.IsReferenceEdited);
            Assert.Null(sample.CreateMeasurement().Spectrum);
        }
        Assert.True(session.IsComplete);
        var result = session.Calculate(Identity());
        Assert.Equal(1, result.A, 10);
        Assert.Equal(1, result.E, 10);
        Assert.Equal(1, result.I, 10);
    }

    [Theory]
    [InlineData("NaN", "0.2", "0.3")]
    [InlineData("Infinity", "0.2", "0.3")]
    [InlineData("2", "bad", "0.3")]
    [InlineData("2", "0.2", "")]
    [InlineData("2", "0.2", "0")]
    [InlineData("1e308", "1e308", "0.3")]
    public void InvalidManualReferenceCannotReuseAcquiredValues(string y, string x, string chromaY)
    {
        var sample = new LumFourColorCalibrationSample(LumFourColorCorrectionTarget.Red);
        FillSample(sample, "source", 42);
        sample.ReferenceYInput = y;
        sample.ReferenceCieXInput = x;
        sample.ReferenceCieYInput = chromaY;
        Assert.True(sample.IsReferenceEdited);
        Assert.False(sample.IsComplete);
        Assert.False(sample.HasSpectrumMeasurement);
        Assert.False(string.IsNullOrWhiteSpace(sample.ReferenceInputError));
        Assert.Throws<InvalidOperationException>(() => sample.CreateMeasurement());
    }

    [Fact]
    public void InlineReferenceEditsInvalidateSavedResultAndStayWithTheirColor()
    {
        string path = Path.Combine(Path.GetTempPath(), "lum-reference-" + Guid.NewGuid() + ".dat");
        WriteSource(path);
        try
        {
            WithTheme(() =>
            {
                var window = new LumFourColorCalibrationWorkflowWindow(Array.Empty<DeviceCamera>(), Array.Empty<DeviceSpectrum>(), path);
                try
                {
                    Control<RadioButton>(window, "SinglePointMode").IsChecked = true;
                    var list = Control<ListBox>(window, "SampleList");
                    var sample = Assert.IsType<LumFourColorCalibrationSample>(list.Items[0]);
                    FillSample(sample, LumFourColorSourceSnapshot.ComputeHash(path), 42);
                    Invoke(window, "RefreshSelectedSample");
                    Render(window, 1176, 736, "reference-before-edit");
                    Assert.False(sample.IsReferenceEdited);
                    Click(window, "CalculateButton");
                    Assert.True(Control<Button>(window, "SaveButton").IsEnabled);
                    Control<TextBox>(window, "CameraYBox").Text = "4";
                    Assert.True(sample.IsCameraEdited);
                    Assert.False(Control<Button>(window, "SaveButton").IsEnabled);
                    Click(window, "RestoreCameraButton");
                    Assert.False(sample.IsCameraEdited);
                    Click(window, "CalculateButton");
                    Assert.True(Control<Button>(window, "SaveButton").IsEnabled);
                    Control<TextBox>(window, "ReferenceYBox").Text = "4";
                    Assert.True(sample.IsReferenceEdited);
                    Assert.Equal(4, sample.ReferenceY);
                    Assert.False(Control<Button>(window, "SaveButton").IsEnabled);
                    Assert.True(Control<Button>(window, "RestoreReferenceButton").IsEnabled);
                    Render(window, 1176, 736, "reference-inline-edited");
                    Click(window, "RestoreReferenceButton");
                    Assert.False(sample.IsReferenceEdited);
                    Assert.Equal("2", Control<TextBox>(window, "ReferenceYBox").Text);
                    Assert.False(Control<Button>(window, "SaveButton").IsEnabled);
                    Control<RadioButton>(window, "FourColorMode").IsChecked = true;
                    Control<TextBox>(window, "ReferenceYBox").Text = "-1";
                    Control<TextBox>(window, "ReferenceCieXBox").Text = "0.2";
                    Control<TextBox>(window, "ReferenceCieYBox").Text = "0.3";
                    list.SelectedIndex = 1;
                    Assert.Equal("", Control<TextBox>(window, "ReferenceYBox").Text);
                    Control<TextBox>(window, "ReferenceYBox").Text = "6";
                    list.SelectedIndex = 0;
                    Assert.Equal("-1", Control<TextBox>(window, "ReferenceYBox").Text);
                    Assert.True(((LumFourColorCalibrationSample)list.Items[0]).HasSpectrumMeasurement);
                    Assert.False(((LumFourColorCalibrationSample)list.Items[1]).HasSpectrumMeasurement);
                    Invoke(window, "SetBusy", true, "测试采集中");
                    Assert.False(Control<TextBox>(window, "ReferenceYBox").IsEnabled);
                    Assert.False(Control<TextBox>(window, "CameraYBox").IsEnabled);
                    Invoke(window, "SetBusy", false, "");
                }
                finally { window.Close(); }
            });
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ExcelPasteIsAtomicSupportsPartialRectanglesAndRoundTripsHeaders()
    {
        var rows = new[] { new CorrectionMeasurementRow { Target = "R", CameraY = "old" }, new CorrectionMeasurementRow { Target = "G" } };
        Assert.Throws<InvalidOperationException>(() => LumFourColorMeasurementClipboard.Paste(rows, "1\t0.2\t0.3\n2\tNaN\t0.3", 0, 1));
        Assert.Equal("old", rows[0].CameraY);
        LumFourColorMeasurementClipboard.Paste(rows, "-2\t2e-1\t0.3\n3\t0.25\t0.35", 0, 1);
        Assert.Equal("-2", rows[0].CameraY);
        Assert.Equal("0.35", rows[1].CameraYChromaticity);
        LumFourColorMeasurementClipboard.Paste(rows, "4\t0.2\t0.3\n5\t0.25\t0.35", 0, 4);
        string copied = LumFourColorMeasurementClipboard.CopyAll(rows);
        var restored = new[] { new CorrectionMeasurementRow { Target = "R" }, new CorrectionMeasurementRow { Target = "G" } };
        LumFourColorMeasurementClipboard.Paste(restored, copied + "\r\n", 0, 0);
        Assert.Equal(copied, LumFourColorMeasurementClipboard.CopyAll(restored));
        Assert.Throws<InvalidOperationException>(() => LumFourColorMeasurementClipboard.Paste(rows, "G\t1\t2\t3\t4\t5\t6", 0, 0));
        Assert.Throws<InvalidOperationException>(() => LumFourColorMeasurementClipboard.Paste(rows, "1\t2\n3", 0, 1));
        Assert.Throws<InvalidOperationException>(() => LumFourColorMeasurementClipboard.Paste(rows, "1\t2", 0, 6));
        Assert.Throws<InvalidOperationException>(() => LumFourColorMeasurementClipboard.Paste(rows, "1\n2\n3", 0, 1));
        Assert.Equal(copied, LumFourColorMeasurementClipboard.CopyAll(rows));
    }

    [Fact]
    public void ExcelPasteUsesCurrentCellAndInvalidatesConfirmation()
    {
        WithTheme(() =>
        {
            var window = new LumFourColorCorrectionWindow();
            try
            {
                var grid = Control<DataGrid>(window, "MeasurementsGrid");
                Assert.Equal(DataGridSelectionUnit.Cell, grid.SelectionUnit);
                Render(window, 1076, 576, "manual-excel-before");
                grid.CurrentCell = new DataGridCellInfo(grid.Items[1], grid.Columns[4]);
                Control<CheckBox>(window, "ManualDataConfirmed").IsChecked = true;
                window.PasteMeasurements("10\t0.2\t0.3\n20\t0.4\t0.5");
                Assert.Equal("10", ((CorrectionMeasurementRow)grid.Items[1]).ReferenceY);
                Assert.Equal("20", ((CorrectionMeasurementRow)grid.Items[2]).ReferenceY);
                Assert.Equal("", ((CorrectionMeasurementRow)grid.Items[0]).ReferenceY);
                Assert.False(Control<CheckBox>(window, "ManualDataConfirmed").IsChecked);
                Assert.False(Control<Button>(window, "SaveButton").IsEnabled);
                grid.SelectedCells.Add(new DataGridCellInfo(grid.Items[1], grid.Columns[4]));
                grid.SelectedCells.Add(new DataGridCellInfo(grid.Items[2], grid.Columns[4]));
                Assert.True(System.Windows.Input.ApplicationCommands.Copy.CanExecute(null, grid));
                Render(window, 1076, 576, "manual-excel");
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void TemplateReferencesSelectTheConfiguredFileAndNeverFallBackToGroupDefault()
    {
        WpfTestHost.Invoke(() =>
        {
            var slot = CalibrationSlotDefinitions.ByKey["LumFourColor"];
            var template = new CalibrationParam();
            template.Color.LumFourColor.FilePath = "custom.dat";
            var groupDefault = new CalibrationResource(new SysResourceModel { Id = 900001, Name = "group-default.dat", Type = (int)ServiceTypes.LumFourColor });
            var wrongType = new CalibrationResource(new SysResourceModel { Id = 900002, Name = "custom.dat", Type = (int)ServiceTypes.DarkNoise });
            var configured = new CalibrationResource(new SysResourceModel { Id = 900003, Name = "custom.dat", Type = (int)ServiceTypes.LumFourColor });
            var resources = new[] { groupDefault, wrongType, configured };
            try
            {
                Assert.Same(configured, slot.FindTemplateResource(template, resources));
                template.Color.LumFourColor.FilePath = "missing.dat";
                Assert.Null(slot.FindTemplateResource(template, resources));
                template.Color.LumFourColor.FilePath = "";
                Assert.Null(slot.FindTemplateResource(template, resources));
            }
            finally { foreach (var resource in resources) CalibrationResource.CalibrationResources.Remove(resource); }
        });
    }

    [Fact]
    public void RoiUsesImageCoordinatesAndRawValuesAndRejectsOutOfBounds()
    {
        WpfTestHost.Invoke(() =>
        {
            var rect = new RectangleTextProperties { Rect = new Rect(6, 4, 4, 6) };
            var point = LumFourColorPoiEditor.GetMeasurementPoint(rect, 20, 16);
            Assert.Equal(new PoiMeasurementPoint(8, 7, 4, 6, PoiMeasurementShape.Rect), point);
            var frame = SyntheticFrame(20, 16);
            var floats = new float[20 * 16 * 3];
            for (int i = 0; i < 320; i++) { floats[i] = -2; floats[320 + i] = 1; floats[640 + i] = 3; }
            // Native POI includes pixel centers on both rectangle borders.
            for (int y = 4; y <= 10; y++) for (int x = 6; x <= 10; x++) floats[320 + y * 20 + x] = 10;
            Buffer.BlockCopy(floats, 0, frame.Data, 0, frame.Data.Length);
            var result = LumFourColorCieService.Measure(frame, point);
            Assert.Equal(10, result.Y);
            Assert.Equal(-2, result.X);
            var circle = new CircleTextProperties { Center = new Point(8, 7), Radius = 2 };
            var round = LumFourColorPoiEditor.GetMeasurementPoint(circle, 20, 16);
            Assert.Equal(new PoiMeasurementPoint(8, 7, 4, 4, PoiMeasurementShape.Circle), round);
            Assert.Equal(-2, LumFourColorCieService.Measure(frame, round).X);
            circle.Center = new Point(0, 0);
            Assert.Throws<InvalidOperationException>(() => LumFourColorPoiEditor.GetMeasurementPoint(circle, 20, 16));
            Assert.Throws<InvalidOperationException>(() => LumFourColorPoiEditor.GetMeasurementPoint(new RectangleTextProperties { Rect = new Rect(19, 15, 2, 2) }, 20, 16));
        });
    }

    [Fact]
    public void ImageViewRemeasuresGeometryClearsDeletedPoiAndRestoresPerColor()
    {
        WithTheme(() =>
        {
            var options = new LumFourColorPoiOptions();
            var window = new LumFourColorCalibrationWorkflowWindow(Array.Empty<DeviceCamera>(), Array.Empty<DeviceSpectrum>(), options: options);
            try
            {
                Assert.Equal(0, Control<ComboBox>(window, "PoiShapeCombo").SelectedIndex);
                Render(window, 1556, 976, "workflow-landscape-empty");
                var view = Control<ImageView>(window, "CieImageView");
                var list = Control<ListBox>(window, "SampleList");
                var first = Assert.IsType<LumFourColorCalibrationSample>(list.Items[0]);
                var frame = SyntheticFrame(956, 654);
                first.SetFrame(frame, LumFourColorCieService.Render(frame));
                Invoke(window, "RefreshSelectedSample");
                Click(window, "DrawPoiButton");
                Assert.IsType<CircleManager>(view.EditorContext.DrawEditorManager.Current);
                Assert.True(((FrameworkElement)view.FindName("CompactInspectorOverlay")).IsVisible);
                var circle = new DVCircleText(new CircleTextProperties { Center = new Point(478, 327), Radius = 80, Text = "POI" });
                view.ImageShow.AddVisual(circle);
                Drain();
                Assert.True(first.HasCameraMeasurement);
                double originalY = first.CameraY!.Value;
                circle.Attribute.Center = new Point(200, 200);
                Assert.False(first.HasCameraMeasurement);
                Drain();
                Assert.True(first.HasCameraMeasurement);
                Assert.NotEqual(originalY, first.CameraY);
                Control<ComboBox>(window, "PoiShapeCombo").SelectedIndex = 1;
                Assert.IsType<RectangleManager>(view.EditorContext.DrawEditorManager.Current);
                var rectangle = new DVRectangleText(new RectangleTextProperties { Rect = new Rect(320, 220, 160, 120), Text = "POI" });
                view.ImageShow.AddVisual(rectangle);
                Drain();
                Assert.Single(view.EditorContext.DrawingVisualLists);
                Assert.Equal(PoiMeasurementShape.Rect, first.Poi!.Value.Shape);
                list.SelectedIndex = 1;
                Assert.Empty(view.EditorContext.DrawingVisualLists);
                list.SelectedIndex = 0;
                Assert.IsType<DVRectangleText>(Assert.Single(view.EditorContext.DrawingVisualLists));
                view.ImageShow.RemoveVisual((Visual)view.EditorContext.DrawingVisualLists[0]);
                Assert.False(first.HasCameraMeasurement);
                Control<ComboBox>(window, "PoiShapeCombo").SelectedIndex = 0;
                var previewCircle = new DVCircleText(new CircleTextProperties { Center = new Point(478, 327), Radius = 90, Text = "POI" });
                view.ImageShow.AddVisual(previewCircle);
                Drain();
                view.EditorContext.DrawEditorManager.SetCurrentDrawEditor(null);
                view.EditorContext.SelectionVisual.SetRender(previewCircle);
                first.SetSpectrumMeasurement(Spectrum(42));
                Invoke(window, "RefreshSelectedSample");
                Assert.True(((FrameworkElement)view.FindName("CompactInspectorOverlay")).IsVisible);
                Render(window, 1556, 976, "workflow-landscape-circle");
                Assert.True(view.ImageShow.ActualWidth > 0);
            }
            finally { window.Close(); }
        });
    }

    private static void Drain() => Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle, System.Threading.CancellationToken.None, TimeSpan.FromSeconds(5));

    [Fact]
    public void SixManualValuesCanCalculateWithoutImageAndRestoreExactPoiMeasurement()
    {
        var sample = new LumFourColorCalibrationSample(LumFourColorCorrectionTarget.SinglePoint);
        sample.CameraYInput = "2"; sample.CameraCieXInput = "0.2"; sample.CameraCieYInput = "0.3";
        sample.ReferenceYInput = "4"; sample.ReferenceCieXInput = "0.2"; sample.ReferenceCieYInput = "0.3";
        Assert.True(sample.IsComplete);
        Assert.Null(sample.Frame);
        Assert.Equal(2, LumFourColorCorrectionCalculator.CorrectSinglePoint(Identity(), sample.CreateMeasurement()).A, 10);
        Assert.Contains(sample.GetWarnings("hash"), message => message.Contains("相机 Y / x / y"));
        sample.CameraCieYInput = "0";
        Assert.False(sample.HasCameraMeasurement);
        Assert.Null(sample.CameraX);
        Assert.True(sample.HasSpectrumMeasurement);
        Assert.Throws<InvalidOperationException>(() => sample.CreateMeasurement());
        sample.CameraCieYInput = "-0.3";
        Assert.True(sample.HasCameraMeasurement);
        Assert.True(sample.CameraX < 0);

        var poi = new PoiMeasurementPoint(4, 4, 2, 2, PoiMeasurementShape.Circle);
        var raw = new PoiMeasurementResult(-1, 2, -3, -.25f, -.5f, 0, 0, 0, 0);
        sample.SetCameraMeasurement(poi, raw);
        sample.CameraYInput = "7";
        Assert.Equal(poi, sample.Poi);
        Assert.True(sample.CanRestoreCamera);
        sample.RestoreCamera();
        Assert.Equal(raw.X, sample.CameraX); Assert.Equal(raw.Z, sample.CameraZ);
        Assert.False(sample.IsCameraEdited);
        sample.CameraYInput = "NaN";
        Assert.False(sample.HasCameraMeasurement);
        sample.ClearCameraMeasurement();
        Assert.False(sample.CanRestoreCamera);
        Assert.Equal("", sample.CameraYInput);
    }

    [Fact]
    public void PoiTemplateUsesFirstPointAndRejectsWrongSizeOrUnsupportedFirstShape()
    {
        var template = new PoiParam { Width = 20, Height = 16 };
        template.PoiPoints.Add(new PoiPoint { PointType = PoiShape.Circle, PixX = 10, PixY = 8, PixWidth = 4 });
        template.PoiPoints.Add(new PoiPoint { PointType = PoiShape.Rect, PixX = 5, PixY = 5, PixWidth = 2, PixHeight = 2 });
        Assert.Equal(new PoiMeasurementPoint(10, 8, 4, 4, PoiMeasurementShape.Circle), LumFourColorPoiEditor.GetTemplatePoint(template, 20, 16));
        Assert.Throws<InvalidOperationException>(() => LumFourColorPoiEditor.GetTemplatePoint(template, 40, 32));
        template.PoiPoints[0].PointType = PoiShape.LeftTopRect;
        template.PoiPoints[0].PixHeight = 2;
        Assert.Equal(new PoiMeasurementPoint(12, 9, 4, 2, PoiMeasurementShape.Rect), LumFourColorPoiEditor.GetTemplatePoint(template, 20, 16));
        template.PoiPoints[0].PixX = 19;
        Assert.Throws<InvalidOperationException>(() => LumFourColorPoiEditor.GetTemplatePoint(template, 20, 16));
        template.PoiPoints[0].PointType = PoiShape.Point;
        Assert.Throws<InvalidOperationException>(() => LumFourColorPoiEditor.GetTemplatePoint(template, 20, 16));
    }

    [Fact]
    public void WorkflowAppliesPoiTemplateOpensDrawingMenuAndKeepsSixEditableValues()
    {
        WithTheme(() =>
        {
            var template = new PoiParam { Id = -1, Name = "校正 POI", Width = 956, Height = 654 };
            template.PoiPoints.Add(new PoiPoint { PointType = PoiShape.Circle, PixX = 478, PixY = 327, PixWidth = 120 });
            template.PoiPoints.Add(new PoiPoint { PointType = PoiShape.Rect, PixX = 100, PixY = 100, PixWidth = 20, PixHeight = 20 });
            var model = new TemplateModel<PoiParam>(template.Name, template);
            var window = new LumFourColorCalibrationWorkflowWindow(Array.Empty<DeviceCamera>(), Array.Empty<DeviceSpectrum>());
            try
            {
                Control<ComboBox>(window, "PoiTemplateCombo").ItemsSource = new[] { model };
                Render(window, 1556, 976, "workflow-six-empty");
                var sample = (LumFourColorCalibrationSample)Control<ListBox>(window, "SampleList").Items[0];
                var frame = SyntheticFrame(956, 654);
                sample.SetFrame(frame, LumFourColorCieService.Render(frame));
                Invoke(window, "RefreshSelectedSample");
                Control<ComboBox>(window, "PoiTemplateCombo").SelectedIndex = 0;
                Click(window, "ApplyPoiTemplateButton");
                Drain();
                Assert.Equal(new PoiMeasurementPoint(478, 327, 120, 120, PoiMeasurementShape.Circle), sample.Poi);
                var view = Control<ImageView>(window, "CieImageView");
                // External app menu providers require the main application's config startup.
                // This window replaces those entries with its own POI menu.
                view.IEditorToolFactory.IIEditorToolContextMenus.Clear();
                var circle = Assert.IsType<DVCircleText>(Assert.Single(view.EditorContext.DrawingVisualLists));
                double measuredY = sample.CameraY!.Value;
                Control<TextBox>(window, "CameraYBox").Text = "30";
                Assert.True(sample.IsCameraEdited);
                Assert.Equal(30, sample.CameraY);
                Assert.True(Control<Button>(window, "RestoreCameraButton").IsEnabled);
                Click(window, "RestoreCameraButton");
                Assert.Equal(measuredY, sample.CameraY);
                circle.Attribute.Center = new Point(350, 300);
                Assert.False(sample.HasCameraMeasurement);
                Drain();
                Assert.True(sample.HasCameraMeasurement);
                Assert.Equal(478, template.PoiPoints[0].PixX);
                Control<TextBox>(window, "ReferenceYBox").Text = "40";
                Control<TextBox>(window, "ReferenceCieXBox").Text = "0.2";
                Control<TextBox>(window, "ReferenceCieYBox").Text = "0.3";
                foreach (string name in new[] { "CameraYBox", "CameraCieXBox", "CameraCieYBox", "ReferenceYBox", "ReferenceCieXBox", "ReferenceCieYBox" })
                    Assert.False(Control<TextBox>(window, name).IsReadOnly);
                Assert.False(Control<Expander>(window, "MeasurementDetailsExpander").IsExpanded);
                Render(window, 1556, 976, "workflow-six-poi");
                Render(window, 1076, 656, "workflow-six-compact");
                foreach (UIElement surface in new UIElement[] { view.ImageShow, view.Zoombox1 })
                {
                    var opening = (ContextMenuEventArgs)Activator.CreateInstance(typeof(ContextMenuEventArgs), BindingFlags.Instance | BindingFlags.NonPublic,
                        binder: null, args: new object[] { surface, true }, culture: null)!;
                    surface.RaiseEvent(opening);
                    Assert.False(opening.Handled);
                    Assert.Contains(view.EditorContext.ContextMenu.Items.OfType<MenuItem>(), item => Equals(item.Header, ColorVision.ImageEditor.Properties.Resources.Draw_Edit));
                    Assert.DoesNotContain(view.EditorContext.ContextMenu.Items.OfType<MenuItem>(), item => ReferenceEquals(item.Command, ApplicationCommands.Open));
                }
                var items = view.EditorContext.ContextMenu.Items.OfType<MenuItem>().ToArray();
                Assert.Contains(items, item => Equals(item.Header, ColorVision.ImageEditor.Properties.Resources.Draw_Edit));
                Assert.Contains(items, item => Equals(item.Header, "选择 POI 模板…"));
                items.Single(item => Equals(item.Header, ColorVision.ImageEditor.Properties.Resources.Draw_Delete)).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Assert.False(sample.HasCameraMeasurement);
                Assert.Empty(view.EditorContext.DrawingVisualLists);
                items.Single(item => Equals(item.Header, "绘制矩形")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Assert.Equal(1, Control<ComboBox>(window, "PoiShapeCombo").SelectedIndex);
                Assert.IsType<RectangleManager>(view.EditorContext.DrawEditorManager.Current);
                items.Single(item => Equals(item.Header, "绘制圆形")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Assert.Equal(0, Control<ComboBox>(window, "PoiShapeCombo").SelectedIndex);
                Click(window, "DrawPoiButton");
                Assert.IsType<CircleManager>(view.EditorContext.DrawEditorManager.Current);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void PythonRgbModeUsesThreeRowsAndInvalidatesResultsWhenSwitchingModes()
    {
        string path = Path.GetTempFileName();
        WriteSource(path);
        try
        {
            WithTheme(() =>
            {
                var window = new LumFourColorCalibrationWorkflowWindow(Array.Empty<DeviceCamera>(), Array.Empty<DeviceSpectrum>(), path);
                var manual = new LumFourColorCorrectionWindow(path, LumFourColorCorrectionMode.PythonRgb);
                try
                {
                    Control<RadioButton>(window, "PythonRgbMode").IsChecked = true;
                    var list = Control<ListBox>(window, "SampleList");
                    Assert.Equal(new[] { "R", "G", "B" }, list.Items.Cast<LumFourColorCalibrationSample>().Select(sample => sample.Name));
                    Assert.Equal("导出 XYZ 矩阵", Control<Button>(window, "SaveButton").Content);
                    Assert.False(Control<Button>(window, "CalculateButton").IsEnabled);
                    int id = 0;
                    foreach (var sample in list.Items.Cast<LumFourColorCalibrationSample>())
                    {
                        FillSample(sample, LumFourColorSourceSnapshot.ComputeHash(path), ++id);
                        float x = id == 1 ? .64f : id == 2 ? .3f : .15f;
                        float y = id == 1 ? .33f : id == 2 ? .6f : .06f;
                        sample.SetCameraMeasurement(new(0, 0, 1, 1, PoiMeasurementShape.Rect), new(2 * x / y, 2, 2 * (1 - x - y) / y, x, y, 0, 0, 0, 0));
                        sample.SetSpectrumMeasurement(new(new(2, x, y), [new(400, 1), new(500, 2)], id, DateTimeOffset.Now) { PeakAd = 32767, Source = "test" });
                    }
                    Invoke(window, "RefreshSelectedSample");
                    Assert.True(Control<Button>(window, "CalculateButton").IsEnabled);
                    Click(window, "CalculateButton");
                    Assert.True(Control<Button>(window, "SaveButton").IsEnabled);
                    Render(window, 1076, 656, "workflow-python-rgb");
                    Invoke(window, "SetBusy", true, "");
                    Assert.False(Control<RadioButton>(window, "PythonRgbMode").IsEnabled);
                    Invoke(window, "SetBusy", false, "");
                    Control<RadioButton>(window, "FourColorMode").IsChecked = true;
                    Assert.Equal(4, list.Items.Count);
                    Assert.False(Control<Button>(window, "SaveButton").IsEnabled);
                    Assert.All(list.Items.Cast<LumFourColorCalibrationSample>(), sample => Assert.False(sample.IsComplete));

                    Assert.True(Control<RadioButton>(manual, "PythonRgbMode").IsChecked);
                    var grid = Control<DataGrid>(manual, "MeasurementsGrid");
                    Assert.Equal(3, grid.Items.Count);
                    manual.PasteMeasurements("2\t0.64\t0.33\t2\t0.64\t0.33\n2\t0.3\t0.6\t2\t0.3\t0.6\n2\t0.15\t0.06\t2\t0.15\t0.06");
                    Control<CheckBox>(manual, "ManualDataConfirmed").IsChecked = true;
                    Invoke(manual, "Calculate_Click", manual, new RoutedEventArgs());
                    Assert.True(Control<Button>(manual, "SaveButton").IsEnabled);
                    Assert.Equal("XYZ 修正矩阵", Control<TextBlock>(manual, "ResultTitle").Text);
                    Render(manual, 1076, 576, "manual-python-rgb");
                    ((CorrectionMeasurementRow)grid.Items[0]).CameraYChromaticity = "0";
                    Assert.False(Control<Button>(manual, "SaveButton").IsEnabled);
                    Control<CheckBox>(manual, "ManualDataConfirmed").IsChecked = true;
                    Invoke(manual, "Calculate_Click", manual, new RoutedEventArgs());
                    Assert.False(Control<Button>(manual, "SaveButton").IsEnabled);
                    Assert.Empty(Control<TextBox>(manual, "ResultPreview").Text);
                    Control<RadioButton>(manual, "FourColorMode").IsChecked = true;
                    Assert.Equal(4, grid.Items.Count);
                    Assert.False(Control<CheckBox>(manual, "ManualDataConfirmed").IsChecked);
                }
                finally { manual.Close(); window.Close(); }
            });
        }
        finally { File.Delete(path); }
    }

    private static LumFourColorCieCapture SyntheticFrame(int width, int height)
    {
        int count = width * height;
        var values = new float[count * 3];
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            int i = y * width + x;
            double dx = (x - width / 2d) / width, dy = (y - height / 2d) / height;
            float luminance = (float)(2 + 80 * Math.Exp(-12 * (dx * dx + dy * dy)));
            values[i] = luminance * 0.18f;
            values[count + i] = luminance;
            values[2 * count + i] = luminance * 0.03f;
        }
        byte[] bytes = new byte[values.Length * sizeof(float)];
        Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
        return new(bytes, width, height, 32, 3, 1, new[] { 10f, 10f, 10f }) { Source = "合成 CIE · 仅用于界面验证" };
    }

    private static void Render(Window window, int width, int height, string name)
    {
        var root = Assert.IsType<Grid>(window.Content);
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.ShowActivated = false;
        window.ShowInTaskbar = false;
        window.Left = -20000;
        window.Top = -20000;
        window.Width = width + 44;
        window.Height = height + 84;
        if (!window.IsVisible) window.Show();
        window.Dispatcher.Invoke(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
        root.Background = window.Background;
        Assert.True(root.ActualWidth > 0 && root.ActualHeight > 0);
        string? folder = Environment.GetEnvironmentVariable("LUM_FOUR_PREVIEW_DIR");
        if (string.IsNullOrEmpty(folder)) return;
        Directory.CreateDirectory(folder);
        int pixelWidth = (int)Math.Ceiling(root.ActualWidth), pixelHeight = (int)Math.Ceiling(root.ActualHeight);
        var bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(window.Background, null, new Rect(0, 0, pixelWidth, pixelHeight));
            context.DrawRectangle(new VisualBrush(root), null, new Rect(0, 0, pixelWidth, pixelHeight));
        }
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(folder, name + ".png")); encoder.Save(stream);
    }

    private static void WithTheme(Action action, bool dark = false) => WpfTestHost.Invoke(() =>
    {
        var dictionaries = new List<ResourceDictionary>();
        try
        {
            foreach (string source in new[] { dark ? "/HandyControl;component/Themes/basic/colors/colorsdark.xaml" : "/HandyControl;component/Themes/basic/colors/colors.xaml", "/HandyControl;component/Themes/Theme.xaml", dark ? "/ColorVision.Themes;component/Themes/Dark.xaml" : "/ColorVision.Themes;component/Themes/White.xaml", "/ColorVision.Themes;component/Themes/Base.xaml", "/ColorVision.Themes;component/Themes/Icons/Images.xaml" })
            {
                var dictionary = new ResourceDictionary { Source = new Uri(source, UriKind.Relative) };
                Application.Current.Resources.MergedDictionaries.Add(dictionary); dictionaries.Add(dictionary);
            }
            action();
        }
        finally { foreach (var dictionary in dictionaries) Application.Current.Resources.MergedDictionaries.Remove(dictionary); }
    });

    private static T Control<T>(Window window, string name) => Assert.IsType<T>(window.FindName(name));
    private static void Click(Window window, string name) => Control<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, args);
    private static CVRawManualCieConfig Identity() => new() { Gain_x = 1, Gain_y = 1, Gain_z = 1, A = 1, E = 1, I = 1, B = 0, C = 0, D = 0, F = 0, G = 0, H = 0 };
    private static void WriteSource(string path) => File.WriteAllText(path, LumFourColorCorrectionCalculator.SerializeCalibrationFile(Identity()));
    private static LumFourColorCieCapture Frame(string? hash) => new(new byte[12], 1, 1, 32, 3, 1, new[] { 1f, 1f, 1f }) { CalibrationHash = hash, Source = "测试 CIE" };
    private static LumFourColorSpectrumCapture Spectrum(int id) => new(new ColorCorrectionYxy(2, 0.2, 0.3), new[] { new ColorCorrectionSpectrumPoint(380, 1) }, id, DateTimeOffset.UnixEpoch) { PeakAd = 32767.5, Source = "SP-Test" };
    private static void FillSample(LumFourColorCalibrationSample sample, string? hash, int id)
    {
        sample.SetFrame(Frame(hash), null!);
        sample.SetCameraMeasurement(new(0, 0, 1, 1, PoiMeasurementShape.Rect), new(1, 2, 3, 0.2f, 0.3f, 0, 0, 0, 0));
        sample.SetSpectrumMeasurement(Spectrum(id));
    }
}
