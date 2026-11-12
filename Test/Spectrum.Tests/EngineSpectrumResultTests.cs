using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Spectrum;
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
    [InlineData("GetDataAuto", false)]
    [InlineData("EQE.GetDataAuto", true)]
    public void ContinuousServiceResultFollowsTheNotificationWhenAnotherMeasurementHasArrived(string eventName, bool eqe)
    {
        using var db = CreateSpectrumDatabase();
        int notifiedId = db.Insertable(new SpectumResultEntity { DeviceCode = "SPECTRUM.A", IntTime = 12, DataType = eqe }).ExecuteReturnIdentity();
        db.Insertable(new SpectumResultEntity { DeviceCode = "SPECTRUM.A", IntTime = 24, DataType = eqe }).ExecuteReturnIdentity();
        db.Insertable(new SpectumResultEntity { DeviceCode = "SPECTRUM.B", IntTime = 85, DataType = !eqe }).ExecuteReturnIdentity();
        var notification = JsonConvert.DeserializeObject<MsgReturn>($$$"""
            {"EventName":"{{{eventName}}}","Code":102,"DeviceCode":"SPECTRUM.A","data":{"MasterId":{{{notifiedId}}} }}
            """)!;

        var result = MQTTSpectrum.ReadServiceContinuousResult(db, (JObject)notification.Data);

        Assert.NotNull(result);
        Assert.Equal(notifiedId, result.Id);
        Assert.Equal("SPECTRUM.A", result.DeviceCode);
        Assert.Equal(12f, result.IntTime);
        Assert.Equal(eqe, result.DataType);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{}")]
    [InlineData("{\"MasterId\":0}")]
    [InlineData("{\"MasterId\":-1}")]
    [InlineData("{\"MasterId\":123456}")]
    public void ContinuousServiceNotificationWithoutAMatchingResultDoesNotSubstituteHistory(string? data)
    {
        using var db = CreateSpectrumDatabase();
        db.Insertable(new SpectumResultEntity { DeviceCode = "SPECTRUM.A", IntTime = 40 }).ExecuteReturnIdentity();

        Assert.Null(MQTTSpectrum.ReadServiceContinuousResult(db, data == null ? null : JObject.Parse(data)));
    }

    private static SqlSugarClient CreateSpectrumDatabase()
    {
        var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = "Data Source=:memory:", DbType = DbType.Sqlite,
            IsAutoCloseConnection = false, InitKeyType = InitKeyType.Attribute
        });
        db.CodeFirst.InitTables<SpectumResultEntity>();
        return db;
    }

    // Undefined SDK metrics must not become bare NaN/Infinity literals in an ORM INSERT.
    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void NonFiniteMetricsPersistAsNullWithoutChangingRawSamples(float invalid)
    {
        using var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = "Data Source=:memory:", DbType = DbType.Sqlite,
            IsAutoCloseConnection = false, InitKeyType = InitKeyType.Attribute
        });
        db.CodeFirst.InitTables<SpectumResultEntity>();
        object native = new COLOR_PARA { fPL = [1, invalid, -.25f], fRi = [invalid] };
        var mappedFields = typeof(COLOR_PARA).GetFields().Where(field => field.FieldType == typeof(float)
            && typeof(SpectumResultEntity).GetProperty(field.Name)?.PropertyType == typeof(float?)).ToArray();
        foreach (var field in mappedFields) field.SetValue(native, invalid);
        var data = (COLOR_PARA)native;
        data.fy = .25f;
        var capture = new LocalSpectrumCapture
        {
            DeviceCode = "NONFINITE.SPECTRUM", IntegralTime = 100, Data = data,
            EqeData = new COLOR_PARA_EQE { dEqe = invalid, dIm = invalid, dW = invalid }
        };
        var model = LocalSpectrumResultService.CreateModel(capture, new() { Eqe = true, Voltage = 5, Current = 1 });
        int id = db.Insertable(model).ExecuteReturnIdentity();
        var stored = db.Queryable<SpectumResultEntity>().Single(row => row.Id == id);

        foreach (var field in mappedFields.Where(field => field.Name != nameof(COLOR_PARA.fy)))
            Assert.Null(typeof(SpectumResultEntity).GetProperty(field.Name)!.GetValue(stored));
        Assert.Null(stored.Eqe);
        Assert.Null(stored.LuminousFlux);
        Assert.Null(stored.RadiantFlux);
        Assert.Null(stored.fRi);
        Assert.Equal(.25f, stored.fy);
        Assert.Equal(100f, stored.IntTime);
        Assert.Equal(5f, stored.VResult);
        Assert.Equal(1000f, stored.IResult);
        Assert.Equal(invalid, JsonConvert.DeserializeObject<float[]>(stored.fPL!)![1]);
        Assert.Equal(invalid, JObject.Parse(stored.CieDataEx!)[nameof(COLOR_PARA.fx)]!.Value<float>());
        Assert.Equal(invalid, capture.Data.fx);
        Assert.Equal(invalid, capture.Data.fPL[1]);
    }

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
    [InlineData(false, 2f)]
    [InlineData(true, 2f)]
    [InlineData(false, 0.1f)]
    [InlineData(true, 0.1f)]
    public void StandardCsvKeepsSpectralSamplesAtTheirMeasuredWavelength(bool fluxMode, float interval)
    {
        var previousConfig = ConfigService.Instance;
        ConfigService.SetInstance(new ConfigHandler { IsAutoSave = false });
        string path = Path.Combine(Path.GetTempPath(), $"engine-spectrum-grid-{Guid.NewGuid():N}.csv");
        try
        {
            var result = new ViewResultSpectrum(new COLOR_PARA
            {
                fSpect1 = 500f, fSpect2 = 504f, fInterval = interval, fPlambda = 2f,
                fPL = Enumerable.Range(0, (int)Math.Round(4d / interval) + 1)
                    .Select(index => index == 0 ? -0.5f : index * interval).ToArray()
            });
            if (fluxMode) SpectrumCsvExportHelper.ExportLuminousFluxMode(path, [result]);
            else SpectrumCsvExportHelper.ExportLuminanceMode(path, [result]);

            string[] lines = File.ReadAllLines(path);
            string[] headers = lines[0].Split(',');
            string[] values = lines[1].Split(',');
            Assert.Equal(headers.Length, values.Length);
            string Read(string header) => values[Array.IndexOf(headers, header)];

            Assert.Equal(string.Empty, Read("380"));
            Assert.Equal(string.Empty, Read("sp499"));
            Assert.Equal("-1", Read("500"));
            Assert.Equal("0", Read("sp500"));
            Assert.Equal(interval == 2f ? string.Empty : "2", Read("501"));
            Assert.Equal(interval == 2f ? string.Empty : "1", Read("sp501"));
            Assert.Equal("4", Read("502"));
            Assert.Equal("2", Read("sp502"));
            Assert.Equal("8", Read("504"));
            Assert.Equal("4", Read("sp504"));
            Assert.Equal(string.Empty, Read("505"));
        }
        finally
        {
            ConfigService.SetInstance(previousConfig);
            File.Delete(path);
        }
    }

    [Fact]
    public void StandardCsvIgnoresSamplesPastTheDeclaredEndWavelength()
    {
        var previousConfig = ConfigService.Instance;
        ConfigService.SetInstance(new ConfigHandler { IsAutoSave = false });
        string path = Path.Combine(Path.GetTempPath(), $"engine-spectrum-end-{Guid.NewGuid():N}.csv");
        try
        {
            var result = new ViewResultSpectrum(new COLOR_PARA
            {
                fSpect1 = 500f, fSpect2 = 502.75f, fInterval = 1f, fPlambda = 2f,
                fPL = [1f, 2f, 3f, 100f, 200f]
            });
            SpectrumCsvExportHelper.ExportLuminousFluxMode(path, [result]);

            string[] lines = File.ReadAllLines(path);
            string[] headers = lines[0].Split(',');
            string[] values = lines[1].Split(',');
            Assert.Equal("6", values[Array.IndexOf(headers, "502")]);
            Assert.Equal("3", values[Array.IndexOf(headers, "sp502")]);
            Assert.Equal(string.Empty, values[Array.IndexOf(headers, "503")]);
            Assert.Equal(string.Empty, values[Array.IndexOf(headers, "sp503")]);
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
