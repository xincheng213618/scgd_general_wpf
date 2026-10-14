using ColorVision.Engine;
using ColorVision.FileIO;
using Conoscope.ApplicationServices.Capture;
using System.IO;

namespace Conoscope.Tests;

public sealed class FlowCaptureSelectionTests
{
    [Fact]
    public void LatestSuccessfulReadableImageWinsRegardlessOfDatabaseEnumerationOrder()
    {
        string older = WriteCie(), newer = WriteCie();
        try
        {
            MeasureResultImgModel[] rows = [
                new() { Id = 8, FileUrl = older, ResultCode = 0 },
                new() { Id = 13, FileUrl = older, ResultCode = -1 },
                new() { Id = 11, FileUrl = newer, ResultCode = 0 },
                new() { Id = 12, FileUrl = newer + ".missing", ResultCode = 0 }
            ];
            Assert.Equal(newer, ConoscopeCaptureWorkflow.FindCvcieFile(rows));
            Assert.Equal(newer, ConoscopeCaptureWorkflow.FindCvcieFile(rows.Reverse()));
            Assert.Null(ConoscopeCaptureWorkflow.FindCvcieFile([rows[1], rows[3]]));
        }
        finally { File.Delete(older); File.Delete(newer); }
    }

    private static string WriteCie()
    {
        string path = Path.Combine(Path.GetTempPath(), $"conoscope-selection-{Guid.NewGuid():N}.cvcie");
        using CVCIEFile file = new() { Version = 2, Rows = 1, Cols = 1, Bpp = 32, Channels = 3,
            Exp = [1,1,1], Data = new float[] { 10,20,30 }.SelectMany(BitConverter.GetBytes).ToArray() };
        Assert.True(CVFileUtil.WriteCIEFile(path, file));
        return path;
    }
}
