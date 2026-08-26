using ColorVision.Engine;
using ColorVision.Engine.Templates.Jsons.FindCross;
using Newtonsoft.Json;

namespace ColorVision.UI.Tests;

public sealed class FindCrossResultOverlayTests
{
    [Fact]
    public void LocalDistortionUsesRawCenterFromPersistedDiagnostics()
    {
        FindCrossItem item = CreateItem();
        AlgResultMasterModel master = new()
        {
            TName = "LocalFindCross",
            Params = JsonConvert.SerializeObject(new
            {
                Diagnostics = new
                {
                    DistortionApplied = true,
                    RawGeometricCenter = new { x = 4701.25, y = 3188.75 }
                }
            })
        };

        System.Windows.Point center = ViewHandleFindCross.ResolveOverlayCenter(item, master, out bool usesRawCenter);

        Assert.True(usesRawCenter);
        Assert.Equal(4701.25, center.X, 8);
        Assert.Equal(3188.75, center.Y, 8);
    }

    [Fact]
    public void LocalDistortionFallsBackToRawJsonDiagnostics()
    {
        string rawJson = JsonConvert.SerializeObject(new
        {
            diagnostics = new
            {
                DistortionApplied = true,
                RawGeometricCenter = new { x = 4699.5, y = 3202.125 }
            }
        });
        AlgResultMasterModel master = new()
        {
            Params = JsonConvert.SerializeObject(new
            {
                Algorithm = "LocalFindCross",
                Diagnostics = new { DistortionApplied = true },
                RawJson = rawJson
            })
        };

        System.Windows.Point center = ViewHandleFindCross.ResolveOverlayCenter(CreateItem(), master, out bool usesRawCenter);

        Assert.True(usesRawCenter);
        Assert.Equal(4699.5, center.X, 8);
        Assert.Equal(3202.125, center.Y, 8);
    }

    [Fact]
    public void LocalCalibrationOffsetWithoutDistortionUsesRawCenter()
    {
        AlgResultMasterModel master = new()
        {
            TName = "LocalFindCross",
            Params = JsonConvert.SerializeObject(new
            {
                Diagnostics = new
                {
                    DistortionApplied = false,
                    RawGeometricCenter = new { x = 4708.5, y = 3196.25 },
                    AppliedOffset = new { x = 3.5, y = 2.75 }
                }
            })
        };

        System.Windows.Point center = ViewHandleFindCross.ResolveOverlayCenter(CreateItem(), master, out bool usesRawCenter);

        Assert.True(usesRawCenter);
        Assert.Equal(4708.5, center.X, 8);
        Assert.Equal(3196.25, center.Y, 8);
    }

    [Fact]
    public void LegacyOrUncorrectedResultKeepsPersistedCenter()
    {
        FindCrossItem item = CreateItem();
        string diagnostics = JsonConvert.SerializeObject(new
        {
            Diagnostics = new
            {
                DistortionApplied = true,
                RawGeometricCenter = new { x = 4701.25, y = 3188.75 }
            }
        });
        AlgResultMasterModel legacy = new() { TName = "FindCross", Params = diagnostics };
        AlgResultMasterModel localWithoutRawCenter = new()
        {
            TName = "LocalFindCross",
            Params = JsonConvert.SerializeObject(new
            {
                Diagnostics = new { DistortionApplied = false }
            })
        };

        System.Windows.Point legacyCenter = ViewHandleFindCross.ResolveOverlayCenter(item, legacy, out bool legacyUsesRaw);
        System.Windows.Point localCenter = ViewHandleFindCross.ResolveOverlayCenter(item, localWithoutRawCenter, out bool localUsesRaw);

        Assert.False(legacyUsesRaw);
        Assert.False(localUsesRaw);
        Assert.Equal(4712, legacyCenter.X);
        Assert.Equal(3199, legacyCenter.Y);
        Assert.Equal(legacyCenter, localCenter);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{\"Diagnostics\":{\"RawGeometricCenter\":{\"x\":{},\"y\":2}}}")]
    public void MalformedLocalParamsKeepPersistedCenter(string parameters)
    {
        FindCrossItem item = CreateItem();
        AlgResultMasterModel master = new() { TName = "LocalFindCross", Params = parameters };

        System.Windows.Point center = ViewHandleFindCross.ResolveOverlayCenter(item, master, out bool usesRawCenter);

        Assert.False(usesRawCenter);
        Assert.Equal(4712, center.X);
        Assert.Equal(3199, center.Y);
    }

    private static FindCrossItem CreateItem() => new()
    {
        center = new Center { x = 4712, y = 3199 },
        tilt = new Tilt(),
        name = "Point_1"
    };
}
