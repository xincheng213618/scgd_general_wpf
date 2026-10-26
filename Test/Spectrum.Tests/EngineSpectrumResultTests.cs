using ColorVision.Engine.Services.Devices.Spectrum.Dao;
using ColorVision.Engine.Services.Devices.Spectrum.Views;
using ColorVision.Engine.Services.Devices.Spectrum.Local;
using ColorVision.UI;
using cvColorVision;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SqlSugar;
using System.Globalization;
using System.IO;

namespace Spectrum.Tests;

[Collection("LocalSpectrumDriver")]
public sealed class EngineSpectrumResultTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EqeHistoryStoresMilliampsAndTheActualSourceAssociation(bool upstream)
    {
        var parameters = new LocalSpectrumParameters { Eqe = true, Voltage = 5, Current = .025f };
        if (upstream) parameters.ApplySmuData(new FlowEngineLib.SMUResultData(FlowEngineLib.SMUChannelType.A, 4.5, 25, 123, 200));
        var capture = new LocalSpectrumCapture
        {
            DeviceCode = "EQE.TEST", IntegralTime = 100,
            Data = new COLOR_PARA { fPL = [], fRi = [] },
            EqeData = new COLOR_PARA_EQE { dVoltage = parameters.Voltage, dCurrent = parameters.Current, dEqe = .2 }
        };
        var model = LocalSpectrumResultService.CreateModel(capture, parameters);
        Assert.Equal(upstream ? 4.5f : 5f, model.VResult);
        Assert.Equal(25f, model.IResult);
        var view = ColorVision.Engine.Services.Devices.Spectrum.DeviceSpectrum.CreateLocalSpectrumView(capture);
        Assert.Equal(model.VResult, view.V);
        Assert.Equal(model.IResult, view.I);
        Assert.Equal(upstream ? 123 : (int?)null, model.SmuDataId);
        Assert.Equal(.025f, JObject.Parse(model.Params!)["Current"]!.Value<float>());
        if (upstream) Assert.Equal(25, JObject.Parse(model.Params!)["SMUData"]!["I"]!.Value<double>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DatabaseRoundTripPreservesSpectraAndReadsLegacyQuotedJson(bool legacyQuoted)
    {
        var previous = ConfigService.Instance;
        ConfigService.SetInstance(new ConfigHandler { IsAutoSave = false });
        try
        {
            using var db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = "Data Source=:memory:", DbType = DbType.Sqlite,
                IsAutoCloseConnection = false, InitKeyType = InitKeyType.Attribute
            });
            db.CodeFirst.InitTables<SpectumResultEntity>();
            float[] samples = [1, -.25f, .5f];
            var capture = new LocalSpectrumCapture
            {
                DeviceCode = "LOCAL.SPECTRUM", IntegralTime = 100,
                Data = new COLOR_PARA
                {
                    fPL = samples, fRi = new float[15], fSpect1 = 500, fSpect2 = 502,
                    fInterval = 1, fPlambda = 2, fCIEx = 1.25f, fCIEy = 2.5f, fCIEz = 3.75f
                }
            };
            var model = LocalSpectrumResultService.CreateModel(capture, new());
            if (legacyQuoted)
            {
                model.fPL = JsonConvert.SerializeObject(model.fPL);
                model.fRi = JsonConvert.SerializeObject(model.fRi);
                model.CieDataEx = JsonConvert.SerializeObject(model.CieDataEx);
            }
            int id = db.Insertable(model).ExecuteReturnIdentity();
            Assert.True(id > 0);
            var stored = db.Queryable<SpectumResultEntity>().Single(row => row.Id == id);
            Assert.Equal(legacyQuoted ? JTokenType.String : JTokenType.Array, JToken.Parse(stored.fPL!).Type);
            Assert.Equal(legacyQuoted ? JTokenType.String : JTokenType.Object, JToken.Parse(stored.CieDataEx!).Type);
            var result = new ViewResultSpectrum(stored);
            Assert.Equal(id, result.Id);
            Assert.Equal(samples, result.fPL);
            Assert.Equal(1.25, result.fCIEx);
            Assert.Equal(samples, result.CreateRawExportRecord(false).Samples);
        }
        finally { ConfigService.SetInstance(previous); }
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(10f)]
    public void FluxResultPreservesMeasuredValuesInDisplayAndExport(float aFactor)
    {
        var previousConfig = ConfigService.Instance;
        ConfigService.SetInstance(new ConfigHandler { IsAutoSave = false });
        string path = Path.Combine(Path.GetTempPath(), $"engine-spectrum-flux-{Guid.NewGuid():N}.csv");
        try
        {
            SpectumResultEntity source = CreateResult(35f * aFactor);
            source.AFactor = aFactor;
            source.RadiantFlux = 0.5d * aFactor;

            ViewResultSpectrum result = new(source);
            Assert.Equal(35f, float.Parse(result.Lv, CultureInfo.CurrentCulture));
            Assert.Equal(source.LuminousFlux, result.LuminousFlux);
            Assert.Equal(source.RadiantFlux, result.RadiantFlux);

            SpectrumCsvExportHelper.ExportLuminousFluxMode(path, [result]);
            string[] lines = File.ReadAllLines(path);
            string[] headers = lines[0].Split(',');
            string[] values = lines[1].Split(',');
            Assert.Equal(source.LuminousFlux?.ToString(), values[Array.IndexOf(headers, "LuminousFlux(lm)")]);
            Assert.Equal(source.RadiantFlux?.ToString(), values[Array.IndexOf(headers, "RadiantFlux(W)")]);
        }
        finally
        {
            ConfigService.SetInstance(previousConfig);
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0f)]
    public void MissingOrZeroFluxIsNotReplacedWithLuminance(float? flux)
    {
        var previousConfig = ConfigService.Instance;
        ConfigService.SetInstance(new ConfigHandler { IsAutoSave = false });
        try
        {
            ViewResultSpectrum result = new(CreateResult(flux));
            Assert.Equal(flux, result.LuminousFlux);
            Assert.Equal(35f, float.Parse(result.Lv, CultureInfo.CurrentCulture));
        }
        finally { ConfigService.SetInstance(previousConfig); }
    }

    [Fact]
    public void LuminanceGuardDoesNotClampMeasuredFlux()
    {
        var previousConfig = ConfigService.Instance;
        ConfigService.SetInstance(new ConfigHandler { IsAutoSave = false });
        try
        {
            ViewSpectrumConfig.Instance.EnableNegativeLuminanceGuard = true;
            ViewSpectrumConfig.Instance.MinLuminanceValue = 0.01;
            SpectumResultEntity source = CreateResult(-0.5f);
            source.fPh = -1f;

            ViewResultSpectrum result = new(source);
            Assert.Equal(0.01f, float.Parse(result.Lv, CultureInfo.CurrentCulture));
            Assert.Equal(-0.5f, result.LuminousFlux);
            Assert.Equal(-1f, result.fPh);
        }
        finally { ConfigService.SetInstance(previousConfig); }
    }

    private static SpectumResultEntity CreateResult(float? flux) => new()
    {
        DataType = true,
        fPh = 35f,
        LuminousFlux = flux,
        fPL = "[1,1]",
        fSpect1 = 380f,
        fSpect2 = 381f,
        fInterval = 1f,
        fPlambda = 0.001f,
    };
}
