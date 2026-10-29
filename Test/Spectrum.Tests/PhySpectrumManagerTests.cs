using ColorVision.Engine;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using ColorVision.Engine.Services.PhyCameras.Licenses;
using ColorVision.Engine.Services.PhySpectrums;
using ColorVision.Engine.Services.Types;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;

namespace Spectrum.Tests;

public sealed class PhySpectrumManagerTests
{
    [Fact]
    public void CorrectionTargetsFollowSerialWithoutFallingBackToAnotherDevice()
    {
        var first = StubSpectrum("SN-01");
        var second = StubSpectrum("sn-01");
        var other = StubSpectrum("SN-02");
        var unbound = StubSpectrum(string.Empty);
        var devices = new[] { first, second, other, unbound };
        Assert.Equal(new[] { first, second }, PhySpectrumManager.MatchCorrectionDevices(devices, " SN-01 "));
        Assert.Same(other, Assert.Single(PhySpectrumManager.MatchCorrectionDevices(devices, "SN-02")));
        Assert.Empty(PhySpectrumManager.MatchCorrectionDevices(devices, "MISSING"));
        Assert.Empty(PhySpectrumManager.MatchCorrectionDevices(devices, string.Empty));
        first.Config.SN = "REBOUND";
        Assert.Same(second, Assert.Single(PhySpectrumManager.MatchCorrectionDevices(devices, "SN-01")));
    }

    private static DeviceSpectrum StubSpectrum(string serial)
    {
        // Bypass runtime service construction: this test only exercises routing over existing configurations.
        var device = (DeviceSpectrum)RuntimeHelpers.GetUninitializedObject(typeof(DeviceSpectrum));
        device.Config = new ConfigSpectrum { SN = serial };
        return device;
    }

    private static string LicenseValue(string expiry = "2000000000") => Convert.ToBase64String(Encoding.UTF8.GetBytes(
        $$"""{"device_mode":"SP-TEST","expiry_date":"{{expiry}}","licensee":"Test customer"}"""));

    [Fact]
    public void CatalogMergesLegacyLicensesPhysicalAndConfiguredSerialsWithoutCameraRecords()
    {
        var physical = new[]
        {
            new SysResourceModel { Id = 10, Code = " SN-01 ", Type = (int)ServiceTypes.PhySpectrums, Value = null },
            new SysResourceModel { Id = 11, Code = "sn-01", Type = (int)ServiceTypes.PhySpectrums },
            new SysResourceModel { Id = 12, Code = "CAMERA", Type = (int)ServiceTypes.PhyCamera },
            new SysResourceModel { Id = 13, Code = "DELETED", Type = (int)ServiceTypes.PhySpectrums, IsDelete = true }
        };
        var licenses = new[]
        {
            new LicenseModel { Id = 20, MacAddress = "sn-01", LiceType = 1, Model = "SP-A" },
            new LicenseModel { Id = 21, MacAddress = "LEGACY", LiceType = 1 },
            new LicenseModel { Id = 22, MacAddress = "CAMERA-LIC", LiceType = 0 },
            new LicenseModel { Id = 23, MacAddress = " ", LiceType = 1 }
        };
        var result = PhySpectrumStore.Merge(physical, licenses, new[] { "CONFIGURED", "Sn-01", "" });
        Assert.Equal(3, result.Count);
        var registered = Assert.Single(result, r => r.IsRegistered);
        Assert.Equal(10, registered.ResourceId);
        Assert.Equal("SP-A", registered.DisplayModel);
        Assert.Contains(result, r => r.SN == "LEGACY" && r.ResourceId == null);
        Assert.Contains(result, r => r.SN == "CONFIGURED" && r.License == null);
        Assert.All(result, r => Assert.False(r.IsDiscovered));
        Assert.Null(physical[0].Value); // Reading the catalog does not initialize a camera config.
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("../SN")]
    [InlineData("folder\\SN")]
    [InlineData("SN\n01")]
    public void RegistrationRejectsInvalidSerials(string sn) => Assert.Throws<ArgumentException>(() => PhySpectrumStore.NormalizeSerial(sn));

    [Fact]
    public void LicenseParserPreservesValueAndUsesSpectrumType()
    {
        string value = LicenseValue();
        var result = SpectrumLicenseUpdateService.ParseLicense(value, "SN-01");
        Assert.Equal(value, result.LicenseValue);
        Assert.Equal("SN-01", result.MacAddress);
        Assert.Equal(1, result.LiceType);
        Assert.Equal("SP-TEST", result.Model);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(2000000000).LocalDateTime, result.ExpiryDate);
    }

    [Theory]
    [InlineData("not base64")]
    [InlineData("e30=")]
    [InlineData("W10=")]
    public void InvalidLicenseCannotBecomeAStoredRecord(string value) => Assert.Throws<InvalidDataException>(() => SpectrumLicenseUpdateService.ParseLicense(value, "SN-01"));

    [Fact]
    public void InvalidExpiryCannotBecomeAStoredRecord() => Assert.Throws<InvalidDataException>(() => SpectrumLicenseUpdateService.ParseLicense(LicenseValue("999999999999999999"), "SN-01"));

    [Fact]
    public void ArchiveSelectsOnlyTheRequestedSerialCaseInsensitively()
    {
        using var stream = Archive(("nested/sn-01.LIC", LicenseValue()), ("OTHER.lic", "invalid"));
        var result = SpectrumLicenseUpdateService.ReadArchive(stream, "SN-01");
        Assert.Equal("SN-01", result.MacAddress);
        Assert.Equal("SP-TEST", result.Model);
    }

    [Fact]
    public void ArchiveRejectsMissingOrAmbiguousSerial()
    {
        using var missing = Archive(("OTHER.lic", LicenseValue()));
        Assert.Throws<InvalidDataException>(() => SpectrumLicenseUpdateService.ReadArchive(missing, "SN-01"));
        using var ambiguous = Archive(("first/SN-01.lic", LicenseValue()), ("second/sn-01.lic", LicenseValue()));
        Assert.Throws<InvalidDataException>(() => SpectrumLicenseUpdateService.ReadArchive(ambiguous, "SN-01"));
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task JsonErrorsIncludeStatusAndServerMessage(HttpStatusCode status)
    {
        using var client = new HttpClient(new StubHandler(_ => new HttpResponseMessage(status) { Content = new StringContent("{\"message\":\"license unavailable\"}", Encoding.UTF8, "application/json") }));
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => new SpectrumLicenseUpdateService(client).DownloadAsync("SN-01"));
        Assert.Contains($"HTTP {(int)status}", error.Message);
        Assert.Contains("license unavailable", error.Message);
        Assert.Contains("SN-01", error.Message);
    }

    [Fact]
    public async Task DownloadPostsSelectedSerialAndParsesArchiveWithoutUsingServerFilename()
    {
        using var archive = Archive(("SN-01.lic", LicenseValue()));
        string? requestBody = null;
        using var client = new HttpClient(new StubHandler(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            requestBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            var content = new ByteArrayContent(archive.ToArray());
            content.Headers.ContentDisposition = new("attachment") { FileName = "../../untrusted.zip" };
            return new(HttpStatusCode.OK) { Content = content };
        }));
        var license = await new SpectrumLicenseUpdateService(client).DownloadAsync(" SN-01 ");
        Assert.Contains("\"macSn\":\"SN-01\"", requestBody);
        Assert.Equal("SN-01", license.MacAddress);
    }

    [Fact]
    public void ToolbarImportReadsAllSerialsWithoutRequiringASelection()
    {
        string file = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".zip");
        try
        {
            using var archive = Archive(("SN-01.lic", LicenseValue()), ("nested/SN-02.lic", LicenseValue()));
            File.WriteAllBytes(file, archive.ToArray());
            var licenses = SpectrumLicenseUpdateService.ReadFiles(new[] { file });
            Assert.Equal(new[] { "SN-01", "SN-02" }, licenses.Select(l => l.MacAddress));
            Assert.All(licenses, license => Assert.Equal(1, license.LiceType));
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void ToolbarImportRejectsDuplicateSerialsAcrossFiles()
    {
        string first = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".zip");
        string second = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".zip");
        try
        {
            using var archive = Archive(("SN-01.lic", LicenseValue()));
            using var duplicate = Archive(("sn-01.lic", LicenseValue()));
            File.WriteAllBytes(first, archive.ToArray());
            File.WriteAllBytes(second, duplicate.ToArray());
            Assert.Throws<InvalidDataException>(() => SpectrumLicenseUpdateService.ReadFiles(new[] { first, second }));
        }
        finally { File.Delete(first); File.Delete(second); }
    }

    private static MemoryStream Archive(params (string Name, string Value)[] files)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
            foreach (var file in files)
            {
                using var writer = new StreamWriter(archive.CreateEntry(file.Name).Open());
                writer.Write(file.Value);
            }
        stream.Position = 0;
        return stream;
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(respond(request));
    }
}
