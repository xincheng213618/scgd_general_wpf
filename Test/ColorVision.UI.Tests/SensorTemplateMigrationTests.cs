using ColorVision.Database;
using ColorVision.Engine.Services.Devices.Sensor.Templates;

namespace ColorVision.UI.Tests;

public class SensorTemplateMigrationTests
{
    [Theory]
    [InlineData("Sensor.CVFilterWheel1", "Sensor.CVFilterWheel")]
    [InlineData("Sensor.Default12", "Sensor.Default")]
    [InlineData("Sensor.Custom", "Sensor.Custom")]
    [InlineData("Image.ROI1", null)]
    public void InferLegacySensorCodeUsesSensorBusinessName(string templateName, string? expectedCode)
    {
        Assert.Equal(expectedCode, SensorTemplateDictionaryService.InferLegacySensorCode(templateName));
    }

    [Fact]
    public void BuildLegacyIdCollisionInsertsByCodeAndRemapsReferences()
    {
        string sql = SensorTemplateMigrationSqlBuilder.Build(
            [
                new SensorTemplateMigrationMasterRow
                {
                    ModMasterId = 80,
                    SourceDictionaryId = 52,
                    TemplateName = "Sensor.CVFilterWheel1",
                    DictionaryCode = "Image.ROI",
                    DictionaryName = "图像裁剪",
                    DictionaryModType = 7
                }
            ],
            [new SensorTemplateMigrationDetailRow { DetailId = 651, ModMasterId = 80, SourceItemId = 521 }],
            []);

        Assert.Contains("Generic sensor: Sensor.CVFilterWheel", sql);
        Assert.Contains("WHERE `code` = 'Sensor.CVFilterWheel'", sql);
        Assert.Contains("WHERE `id` IN (80)", sql);
        Assert.Contains("'defaultcommand'", sql);
        Assert.Contains("WHERE `id` IN (651)", sql);
        Assert.Contains("AND `cc_pid` = 521", sql);
        Assert.DoesNotContain("SET `mm_id` = 52", sql);
        Assert.DoesNotContain("图像裁剪", sql);
    }

    [Fact]
    public void BuildHealthySensorCopiesCommandsAndStillRemapsIds()
    {
        DateTime created = new(2026, 7, 17, 12, 0, 0, DateTimeKind.Local);
        string sql = SensorTemplateMigrationSqlBuilder.Build(
            [
                new SensorTemplateMigrationMasterRow
                {
                    ModMasterId = 90,
                    SourceDictionaryId = 70,
                    TemplateName = "OpenFilter",
                    DictionaryCode = "Sensor.CustomFilter",
                    DictionaryName = "自定义滤光片",
                    DictionaryModType = 5,
                    DictionaryCreateDate = created
                }
            ],
            [new SensorTemplateMigrationDetailRow { DetailId = 901, ModMasterId = 90, SourceItemId = 900 }],
            [
                new SensorTemplateMigrationItemRow
                {
                    SourceItemId = 900,
                    SourceDictionaryId = 70,
                    Symbol = "open",
                    Name = "打开",
                    DefaultValue = "OPEN,OK,Ascii,1000/0,0",
                    ValueType = 3,
                    CreateDate = created,
                    IsEnable = true
                }
            ]);

        Assert.Contains("WHERE `code` = 'Sensor.CustomFilter'", sql);
        Assert.Contains("'open', '打开', 'OPEN,OK,Ascii,1000/0,0'", sql);
        Assert.Contains("WHERE `id` IN (90)", sql);
        Assert.Contains("WHERE `id` IN (901)", sql);
        Assert.Contains("AND `cc_pid` = 900", sql);
        Assert.Contains("COALESCE(MAX(`id`), 0) + 1", sql);
        Assert.DoesNotContain("SET `mm_id` = 70", sql);
    }
}
