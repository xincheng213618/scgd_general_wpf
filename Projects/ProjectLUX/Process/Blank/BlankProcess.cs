using ColorVision.Database;
using ColorVision.Engine;

namespace ProjectLUX.Process.Blank
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

        public string GenText(IProcessExecutionContext ctx)
        {
            return string.Empty;
        }
    }
}
