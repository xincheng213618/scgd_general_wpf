using ColorVision.UI;
using Newtonsoft.Json;
using System.IO;
using System.ComponentModel;
using System.Reflection;

namespace ProjectARVRPro.Tests;

public sealed class ProjectSettingsSessionTests
{
    [Fact]
    public void DraftAndDefaultsNeverChangeSourcesOrRuntimeState()
    {
        var project = new ProjectARVRProConfig { TryCountMax = 6, SN = "running", StepIndex = 3 };
        var results = new ViewResultManagerConfig { Count = 120, Height = 321, SourceExportFormat = SourceImageFormat.BMP };
        var session = new ProjectSettingsSession(project, results, true);
        session.Project.TryCountMax = 10;
        session.Results.Count = 30;
        session.RestoreDefaults();

        Assert.Equal(6, project.TryCountMax);
        Assert.Equal(120, results.Count);
        Assert.Equal("running", project.SN);
        Assert.Equal(3, project.StepIndex);
        Assert.Equal(321, results.Height);
        Assert.Equal(SourceImageFormat.BMP, results.SourceExportFormat);
        Assert.Equal(2, session.Project.TryCountMax);
        Assert.Equal(50, session.Results.Count);
        Assert.True(session.Results.SourceImageSupportsBmp);
        Assert.Equal(SourceImageFormat.TIFF, session.Results.SourceExportFormat);
    }

    [Fact]
    public void SaveWritesBothExistingObjectsAndPreservesConcurrentRuntimeAndUntouchedSettings()
    {
        var project = new ProjectARVRProConfig { SN = "before", StepIndex = 1 };
        var results = new ViewResultManagerConfig { Count = 50, Height = 300 };
        var session = new ProjectSettingsSession(project, results, true);
        session.Project.TryCountMax = 4;
        session.Results.IsSaveSourceImage = true;
        session.Results.SourceExportFormatWithBmp = SourceImageFormat.BMP;
        project.SN = "after";
        project.StepIndex = 5;
        project.TemplateSelectedIndex = 9;
        results.Height = 420;
        results.Count = 75;
        int saves = 0;
        session.Save(() =>
        {
            saves++;
            Assert.Equal(4, project.TryCountMax);
            Assert.True(results.IsSaveSourceImage);
            Assert.Equal(SourceImageFormat.BMP, results.SourceExportFormat);
        });

        Assert.Equal(1, saves);
        Assert.Equal("after", project.SN);
        Assert.Equal(5, project.StepIndex);
        Assert.Equal(9, project.TemplateSelectedIndex);
        Assert.Equal(420, results.Height);
        Assert.Equal(75, results.Count);
        var restored = JsonConvert.DeserializeObject<ViewResultManagerConfig>(JsonConvert.SerializeObject(results))!;
        Assert.Equal(SourceImageFormat.BMP, restored.SourceExportFormat);
        Assert.True(restored.IsSaveSourceImage);
    }

    [Fact]
    public void PersistenceFailureRestoresPreSaveValuesAndRetainsDraftForRetry()
    {
        var project = new ProjectARVRProConfig();
        var results = new ViewResultManagerConfig();
        var session = new ProjectSettingsSession(project, results, false);
        session.Project.TryCountMax = 8;
        session.Results.IsSaveCsv = false;
        project.TryCountMax = 3;
        Assert.Throws<IOException>(() => session.Save(() => throw new IOException("test failure")));
        Assert.Equal(3, project.TryCountMax);
        Assert.True(results.IsSaveCsv);
        Assert.Equal(8, session.Project.TryCountMax);
        Assert.False(session.Results.IsSaveCsv);
        session.Save(() => { });
        Assert.Equal(8, project.TryCountMax);
        Assert.False(results.IsSaveCsv);
    }

    [Theory]
    [InlineData(true, SourceImageFormat.BMP)]
    [InlineData(false, SourceImageFormat.TIFF)]
    public void SourceFormatCapabilityNormalizationIsDraftOnly(bool supportsBmp, SourceImageFormat expected)
    {
        var results = new ViewResultManagerConfig { IsSaveSourceImage = true, SourceExportFormat = SourceImageFormat.BMP };
        var session = new ProjectSettingsSession(new(), results, supportsBmp);
        Assert.Equal(SourceImageFormat.BMP, results.SourceExportFormat);
        Assert.Equal(expected, session.Results.SourceExportFormat);
        Assert.Equal(supportsBmp, session.Results.ShowSourceFormatWithBmp);
        Assert.Equal(!supportsBmp, session.Results.ShowSourceFormatWithoutBmp);
        session.Results.IsSaveCsv = false; // Force a save even when BMP already matches.
        session.Save(() => { });
        Assert.Equal(expected, results.SourceExportFormat);
    }

    [Fact]
    public void EveryEditablePropertyIsAvailableExactlyOnceAndSearchIncludesHiddenDependentSettings()
    {
        var session = new ProjectSettingsSession(new(), new(), false);
        foreach (var source in new object[] { session.Project, session.Results })
        {
            var expected = PropertyEditorHelper.GetEditableProperties(source.GetType())
                .Where(property => property.GetCustomAttribute<CategoryAttribute>()?.Category != "串口参数")
                .Select(property => property.Name).Order();
            var actual = session.Sections.SelectMany(section => section.Groups).Where(group => group.Source == source)
                .SelectMany(group => group.Properties).Select(property => property.Name).Order();
            Assert.Equal(expected, actual);
        }
        Assert.Equal(ProjectSettingsPage.Images, Assert.Single(session.Sections, section => section.Matches("TIFF")).Id);
        Assert.Equal(ProjectSettingsPage.Results, Assert.Single(session.Sections, section => section.Matches("CodeUseSN")).Id);
        Assert.Equal(ProjectSettingsPage.Images, Assert.Single(session.Sections, section => section.Matches("IsSaveLink")).Id);
        Assert.Equal(ProjectSettingsPage.Results, Assert.Single(session.Sections, section => section.Matches("CsvSavePath")).Id);
        Assert.DoesNotContain(session.Sections, section => section.Matches("TextSavePath") || section.Matches("Thunderbird"));
        Assert.Equal(4, session.Sections.Count);
    }

    [Fact]
    public void DefaultsAndSaveDoNotChangeConnectionOrLegacyTextDirectory()
    {
        var project = new ProjectARVRProConfig { ThunderbirdPortName = "COM19", ThunderbirdBaudRate = 57600,
            ThunderbirdTimeoutMs = -1, ThunderbirdAutoConnect = true };
        var results = new ViewResultManagerConfig { TextSavePath = "legacy-text" };
        var session = new ProjectSettingsSession(project, results, false);
        session.RestoreDefaults();
        session.Project.TryCountMax = 9;
        session.Save(() => { });
        Assert.Equal("COM19", project.ThunderbirdPortName);
        Assert.Equal(57600, project.ThunderbirdBaudRate);
        Assert.Equal(-1, project.ThunderbirdTimeoutMs);
        Assert.True(project.ThunderbirdAutoConnect);
        Assert.Equal("legacy-text", results.TextSavePath);
        Assert.Equal("legacy-text", JsonConvert.DeserializeObject<ViewResultManagerConfig>(JsonConvert.SerializeObject(results))!.TextSavePath);
    }

    [Fact]
    public void NoOpDoesNotPersistAndInvalidFormatCannotReachSources()
    {
        var project = new ProjectARVRProConfig();
        var results = new ViewResultManagerConfig { Count = -1 };
        var session = new ProjectSettingsSession(project, results, false);
        int saves = 0;
        session.Save(() => saves++);
        Assert.Equal(0, saves);
        Assert.Null(session.Validate()); // The existing unlimited-query setting remains valid.
        session.Results.CodeDateFormat = "%";
        Assert.Equal(ProjectSettingsPage.Results, session.Validate()!.Page);
        Assert.Throws<InvalidOperationException>(() => session.Save(() => saves++));
        Assert.Equal(0, saves);
        Assert.NotEqual("%", results.CodeDateFormat);
    }

    [Fact]
    public void PerSettingSavesAdvanceBaselineAndDoNotCommitOtherInvalidDrafts()
    {
        var results = new ViewResultManagerConfig();
        var session = new ProjectSettingsSession(new(), results, true);
        int saves = 0;
        session.Results.CodeDateFormat = "%";
        session.Results.IsSaveCsv = false;
        session.SaveChange(session.Results, nameof(results.IsSaveCsv), () => saves++);
        Assert.False(results.IsSaveCsv);
        Assert.NotEqual("%", results.CodeDateFormat);
        session.SaveChange(session.Results, nameof(results.IsSaveCsv), () => saves++);
        Assert.Equal(1, saves);
        session.Results.IsSaveCsv = true;
        session.SaveChange(session.Results, nameof(results.IsSaveCsv), () => saves++);
        Assert.True(results.IsSaveCsv);
        Assert.Equal(2, saves);
        Assert.Throws<InvalidOperationException>(() => session.SaveChange(session.Results, nameof(results.CodeDateFormat), () => saves++));
        Assert.Equal(2, saves);
        session.Results.CodeDateFormat = "yyyyMMdd";
        session.SaveChange(session.Results, nameof(results.CodeDateFormat), () => saves++);
        Assert.False(session.HasChanges);
    }

    [Fact]
    public void FirstActualAutoSaveIncludesHighBitFormatNormalization()
    {
        var results = new ViewResultManagerConfig { SourceExportFormat = SourceImageFormat.BMP };
        var session = new ProjectSettingsSession(new(), results, false);
        int saves = 0;
        session.SaveChange(session.Results, nameof(results.IsSaveCsv), () => saves++);
        Assert.Equal(0, saves);
        Assert.Equal(SourceImageFormat.BMP, results.SourceExportFormat);
        session.Results.IsSaveCsv = false;
        session.SaveChange(session.Results, nameof(results.IsSaveCsv), () => saves++);
        Assert.Equal(1, saves);
        Assert.Equal(SourceImageFormat.TIFF, results.SourceExportFormat);
        Assert.False(session.HasChanges);
    }
}
