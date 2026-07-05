using ColorVision.Database;
using ColorVision.Engine;

namespace ProjectARVRPro.Process.Blank
{
    public class BlankProcess : IProcess
    {
        public bool Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null)
            {
                return false;
            }

            var values = MeasureImgResultDao.Instance.GetAllByBatchId(ctx.Batch.Id);
            if (values.Count > 0)
            {
                ctx.Result.FileName = values[0].FileUrl ?? string.Empty;
            }

            ctx.Result.Result = true;
            ctx.Result.ViewResultJson = string.Empty;
            return true;
        }

        public void Render(IProcessExecutionContext ctx)
        {
        }

        public void GenText(IProcessExecutionContext ctx, System.Windows.Documents.Paragraph paragraph, System.Windows.Media.Brush foreground, double fontSize)
        {
        }
    }
}
