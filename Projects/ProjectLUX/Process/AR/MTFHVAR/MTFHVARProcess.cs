using ColorVision.Database;
using ColorVision.Engine; // DAOs
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.MTF2; // MTFDetailViewReslut
using ColorVision.ImageEditor.Draw;
using Newtonsoft.Json;
using ProjectLUX.Fix;
using ProjectLUX.Process.Distortion;
using ProjectLUX.Process.MTFHV;
using ProjectLUX.Process.W255;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace ProjectLUX.Process.MTFHVAR
{
    public class MTFHVARProcessConfig : ProcessConfigBase
    {
        public string ShowConfig { get => _ShowConfig; set { _ShowConfig = value; OnPropertyChanged(); } }
        private string _ShowConfig = "F1";

        [Category("解析配置")]
        [DisplayName("Center_0F解析Key")]
        [Description("用于解析Center_0F数据的Key")]
        public string Key_Center_0F { get => _Key_Center_0F; set { _Key_Center_0F = value; OnPropertyChanged(); } }
        private string _Key_Center_0F = "Center_0F";

        [Category("解析配置")]
        [DisplayName("LeftUp_0_4F解析Key")]
        [Description("用于解析LeftUp_0_4F数据的Key")]
        public string Key_LeftUp_0_4F { get => _Key_LeftUp_0_4F; set { _Key_LeftUp_0_4F = value; OnPropertyChanged(); } }
        private string _Key_LeftUp_0_4F = "LeftUp_0.5F";

        [Category("解析配置")]
        [DisplayName("LeftDown_0_4F解析Key")]
        [Description("用于解析LeftDown_0_4F数据的Key")]
        public string Key_LeftDown_0_4F { get => _Key_LeftDown_0_4F; set { _Key_LeftDown_0_4F = value; OnPropertyChanged(); } }
        private string _Key_LeftDown_0_4F = "LeftDown_0.5F";

        [Category("解析配置")]
        [DisplayName("RightDown_0_4F解析Key")]
        [Description("用于解析RightDown_0_4F数据的Key")]
        public string Key_RightDown_0_4F { get => _Key_RightDown_0_4F; set { _Key_RightDown_0_4F = value; OnPropertyChanged(); } }
        private string _Key_RightDown_0_4F = "RightDown_0.5F";

        [Category("解析配置")]
        [DisplayName("RightUp_0_4F解析Key")]
        [Description("用于解析RightUp_0_4F数据的Key")]
        public string Key_RightUp_0_4F { get => _Key_RightUp_0_4F; set { _Key_RightUp_0_4F = value; OnPropertyChanged(); } }
        private string _Key_RightUp_0_4F = "RightUp_0.5F";

        [Category("解析配置")]
        [DisplayName("LeftUp_0_8F解析Key")]
        [Description("用于解析LeftUp_0_8F数据的Key")]
        public string Key_LeftUp_0_8F { get => _Key_LeftUp_0_8F; set { _Key_LeftUp_0_8F = value; OnPropertyChanged(); } }
        private string _Key_LeftUp_0_8F = "LeftUp_0.8F";

        [Category("解析配置")]
        [DisplayName("LeftDown_0_8F解析Key")]
        [Description("用于解析LeftDown_0_8F数据的Key")]
        public string Key_LeftDown_0_8F { get => _Key_LeftDown_0_8F; set { _Key_LeftDown_0_8F = value; OnPropertyChanged(); } }
        private string _Key_LeftDown_0_8F = "LeftDown_0.8F";

        [Category("解析配置")]
        [DisplayName("RightDown_0_8F解析Key")]
        [Description("用于解析RightDown_0_8F数据的Key")]
        public string Key_RightDown_0_8F { get => _Key_RightDown_0_8F; set { _Key_RightDown_0_8F = value; OnPropertyChanged(); } }
        private string _Key_RightDown_0_8F = "RightDown_0.8F";

        [Category("解析配置")]
        [DisplayName("RightUp_0_8F解析Key")]
        [Description("用于解析RightUp_0_8F数据的Key")]
        public string Key_RightUp_0_8F { get => _Key_RightUp_0_8F; set { _Key_RightUp_0_8F = value; OnPropertyChanged(); } }
        private string _Key_RightUp_0_8F = "RightUp_0.8F";

    }
    public class MTFHVARProcess : ProcessBase<MTFHVARProcessConfig>
    {
        public override bool Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null) return false;
            var log = ctx.Logger;
            MTFHVARRecipeConfig recipeConfig = ctx.RecipeConfig.GetRequiredService<MTFHVARRecipeConfig>();
            MTFHVARFixConfig fixConfig = ctx.FixConfig.GetRequiredService<MTFHVARFixConfig>();
            MTFHVARViewTestResult testResult = new MTFHVARViewTestResult();


            try
            {
                var values = MeasureImgResultDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                if (values.Count > 0)
                    ctx.Result.FileName = values[0].FileUrl;

                var masters = AlgResultMasterDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                foreach (var master in masters.Where(m => m.ImgFileType == ViewResultAlgType.MTF && m.version == "2.0"))
                {
                    var details = DeatilCommonDao.Instance.GetAllByPid(master.Id);
                    if (details.Count == 1)
                    {
                        var mtfDetail = new MTFDetailViewReslut(details[0]);
                        foreach (var mtf in mtfDetail.MTFResult.resultChild)
                        {
                            if (mtf.name == Config.Key_Center_0F)
                            {
                                //顺序反了  H1=0, V1=1, V2=2, H2=3,后面调整一下

                                //顺序反了  H1=1, V1=0, V2=3, H2=2,后面调整一下

                                testResult.MTF0F_Center_V1.Value = mtf.childRects[0].mtfValue ?? 0;
                                testResult.MTF0F_Center_V1.Value *= fixConfig.MTF0F_Center_V1;
                                testResult.MTF0F_Center_V1.LowLimit = recipeConfig.MTF0F_Center_V1.Min;
                                testResult.MTF0F_Center_V1.UpLimit = recipeConfig.MTF0F_Center_V1.Max;
                                ctx.Result.Result &= testResult.MTF0F_Center_V1.TestResult;

                                testResult.MTF0F_Center_H1.Value = mtf.childRects[1].mtfValue ?? 0;
                                testResult.MTF0F_Center_H1.Value *= fixConfig.MTF0F_Center_H1;
                                testResult.MTF0F_Center_H1.LowLimit = recipeConfig.MTF0F_Center_H1.Min;
                                testResult.MTF0F_Center_H1.UpLimit = recipeConfig.MTF0F_Center_H1.Max;
                                ctx.Result.Result &= testResult.MTF0F_Center_H1.TestResult;

                                testResult.MTF0F_Center_H2.Value = mtf.childRects[3].mtfValue ?? 0;
                                testResult.MTF0F_Center_H2.Value *= fixConfig.MTF0F_Center_H2;
                                testResult.MTF0F_Center_H2.LowLimit = recipeConfig.MTF0F_Center_H2.Min;
                                testResult.MTF0F_Center_H2.UpLimit = recipeConfig.MTF0F_Center_H2.Max;
                                ctx.Result.Result &= testResult.MTF0F_Center_H2.TestResult;

                                testResult.MTF0F_Center_V2.Value = mtf.childRects[2].mtfValue ?? 0;
                                testResult.MTF0F_Center_V2.Value *= fixConfig.MTF0F_Center_V2;
                                testResult.MTF0F_Center_V2.LowLimit = recipeConfig.MTF0F_Center_V2.Min;
                                testResult.MTF0F_Center_V2.UpLimit = recipeConfig.MTF0F_Center_V2.Max;
                                ctx.Result.Result &= testResult.MTF0F_Center_V2.TestResult;

                                testResult.MTF0F_Center_horizontal.Value = mtf.horizontalAverage;
                                testResult.MTF0F_Center_horizontal.Value *= fixConfig.MTF0F_Center_horizontal;
                                testResult.MTF0F_Center_horizontal.LowLimit = recipeConfig.MTF0F_Center_horizontal.Min;
                                testResult.MTF0F_Center_horizontal.UpLimit = recipeConfig.MTF0F_Center_horizontal.Max;
                                ctx.Result.Result &= testResult.MTF0F_Center_horizontal.TestResult;

                                testResult.MTF0F_Center_Vertical.Value = mtf.verticalAverage;
                                testResult.MTF0F_Center_Vertical.Value *= fixConfig.MTF0F_Center_Vertical;
                                testResult.MTF0F_Center_Vertical.LowLimit = recipeConfig.MTF0F_Center_Vertical.Min;
                                testResult.MTF0F_Center_Vertical.UpLimit = recipeConfig.MTF0F_Center_Vertical.Max;
                                ctx.Result.Result &= testResult.MTF0F_Center_Vertical.TestResult;
                            }


                            if (mtf.name == Config.Key_LeftUp_0_4F)
                            {
                                testResult.MTF0_4F_LeftUp_V1.Value = mtf.childRects[0].mtfValue ?? 0;
                                testResult.MTF0_4F_LeftUp_V1.Value *= fixConfig.MTF0_4F_LeftUp_V1;
                                testResult.MTF0_4F_LeftUp_V1.LowLimit = recipeConfig.MTF0_4F_LeftUp_V1.Min;
                                testResult.MTF0_4F_LeftUp_V1.UpLimit = recipeConfig.MTF0_4F_LeftUp_V1.Max;
                                ctx.Result.Result &= testResult.MTF0_4F_LeftUp_V1.TestResult;

                                testResult.MTF0_4F_LeftUp_H1.Value = mtf.childRects[1].mtfValue ?? 0;
                                testResult.MTF0_4F_LeftUp_H1.Value *= fixConfig.MTF0_4F_LeftUp_H1;
                                testResult.MTF0_4F_LeftUp_H1.LowLimit = recipeConfig.MTF0_4F_LeftUp_H1.Min;
                                testResult.MTF0_4F_LeftUp_H1.UpLimit = recipeConfig.MTF0_4F_LeftUp_H1.Max;
                                ctx.Result.Result &= testResult.MTF0_4F_LeftUp_H1.TestResult;

                                testResult.MTF0_4F_LeftUp_H2.Value = mtf.childRects[3].mtfValue ?? 0;
                                testResult.MTF0_4F_LeftUp_H2.Value *= fixConfig.MTF0_4F_LeftUp_H2;
                                testResult.MTF0_4F_LeftUp_H2.LowLimit = recipeConfig.MTF0_4F_LeftUp_H2.Min;
                                testResult.MTF0_4F_LeftUp_H2.UpLimit = recipeConfig.MTF0_4F_LeftUp_H2.Max;
                                ctx.Result.Result &= testResult.MTF0_4F_LeftUp_H2.TestResult;

                                testResult.MTF0_4F_LeftUp_V2.Value = mtf.childRects[2].mtfValue ?? 0;
                                testResult.MTF0_4F_LeftUp_V2.Value *= fixConfig.MTF0_4F_LeftUp_V2;
                                testResult.MTF0_4F_LeftUp_V2.LowLimit = recipeConfig.MTF0_4F_LeftUp_V2.Min;
                                testResult.MTF0_4F_LeftUp_V2.UpLimit = recipeConfig.MTF0_4F_LeftUp_V2.Max;
                                ctx.Result.Result &= testResult.MTF0_4F_LeftUp_V2.TestResult;

                                testResult.MTF0_4F_LeftUp_horizontal.Value = mtf.horizontalAverage;
                                testResult.MTF0_4F_LeftUp_horizontal.Value *= fixConfig.MTF0_4F_LeftUp_horizontal;
                                testResult.MTF0_4F_LeftUp_horizontal.LowLimit = recipeConfig.MTF0_4F_LeftUp_horizontal.Min;
                                testResult.MTF0_4F_LeftUp_horizontal.UpLimit = recipeConfig.MTF0_4F_LeftUp_horizontal.Max;
                                ctx.Result.Result &= testResult.MTF0_4F_LeftUp_horizontal.TestResult;

                                testResult.MTF0_4F_LeftUp_Vertical.Value = mtf.verticalAverage;
                                testResult.MTF0_4F_LeftUp_Vertical.Value *= fixConfig.MTF0_4F_LeftUp_Vertical;
                                testResult.MTF0_4F_LeftUp_Vertical.LowLimit = recipeConfig.MTF0_4F_LeftUp_Vertical.Min;
                                testResult.MTF0_4F_LeftUp_Vertical.UpLimit = recipeConfig.MTF0_4F_LeftUp_Vertical.Max;
                                ctx.Result.Result &= testResult.MTF0_4F_LeftUp_Vertical.TestResult;
                            }

                            if (mtf.name == Config.Key_RightUp_0_4F)
                            {
                                testResult.MTF0_4F_RightUp_V1.Value = mtf.childRects[0].mtfValue ?? 0;
                                testResult.MTF0_4F_RightUp_V1.Value *= fixConfig.MTF0_4F_RightUp_V1;
                                testResult.MTF0_4F_RightUp_V1.LowLimit = recipeConfig.MTF0_4F_RightUp_V1.Min;
                                testResult.MTF0_4F_RightUp_V1.UpLimit = recipeConfig.MTF0_4F_RightUp_V1.Max;
                                ctx.Result.Result &= testResult.MTF0_4F_RightUp_V1.TestResult;

                                testResult.MTF0_4F_RightUp_H1.Value = mtf.childRects[1].mtfValue ?? 0;
                                testResult.MTF0_4F_RightUp_H1.Value *= fixConfig.MTF0_4F_RightUp_H1;
                                testResult.MTF0_4F_RightUp_H1.LowLimit = recipeConfig.MTF0_4F_RightUp_H1.Min;
                                testResult.MTF0_4F_RightUp_H1.UpLimit = recipeConfig.MTF0_4F_RightUp_H1.Max;
                                ctx.Result.Result &= testResult.MTF0_4F_RightUp_H1.TestResult;

                                testResult.MTF0_4F_RightUp_H2.Value = mtf.childRects[3].mtfValue ?? 0;
                                testResult.MTF0_4F_RightUp_H2.Value *= fixConfig.MTF0_4F_RightUp_H2;
                                testResult.MTF0_4F_RightUp_H2.LowLimit = recipeConfig.MTF0_4F_RightUp_H2.Min;
                                testResult.MTF0_4F_RightUp_H2.UpLimit = recipeConfig.MTF0_4F_RightUp_H2.Max;
                                ctx.Result.Result &= testResult.MTF0_4F_RightUp_H2.TestResult;

                                testResult.MTF0_4F_RightUp_V2.Value = mtf.childRects[2].mtfValue ?? 0;
                                testResult.MTF0_4F_RightUp_V2.Value *= fixConfig.MTF0_4F_RightUp_V2;
                                testResult.MTF0_4F_RightUp_V2.LowLimit = recipeConfig.MTF0_4F_RightUp_V2.Min;
                                testResult.MTF0_4F_RightUp_V2.UpLimit = recipeConfig.MTF0_4F_RightUp_V2.Max;
                                ctx.Result.Result &= testResult.MTF0_4F_RightUp_V2.TestResult;

                                testResult.MTF0_4F_RightUp_horizontal.Value = mtf.horizontalAverage;
                                testResult.MTF0_4F_RightUp_horizontal.Value *= fixConfig.MTF0_4F_RightUp_horizontal;
                                testResult.MTF0_4F_RightUp_horizontal.LowLimit = recipeConfig.MTF0_4F_RightUp_horizontal.Min;
                                testResult.MTF0_4F_RightUp_horizontal.UpLimit = recipeConfig.MTF0_4F_RightUp_horizontal.Max;
                                ctx.Result.Result &= testResult.MTF0_4F_RightUp_horizontal.TestResult;

                                testResult.MTF0_4F_RightUp_Vertical.Value = mtf.verticalAverage;
                                testResult.MTF0_4F_RightUp_Vertical.Value *= fixConfig.MTF0_4F_RightUp_Vertical;
                                testResult.MTF0_4F_RightUp_Vertical.LowLimit = recipeConfig.MTF0_4F_RightUp_Vertical.Min;
                                testResult.MTF0_4F_RightUp_Vertical.UpLimit = recipeConfig.MTF0_4F_RightUp_Vertical.Max;
                                ctx.Result.Result &= testResult.MTF0_4F_RightUp_Vertical.TestResult;
                            }


                            if (mtf.name == Config.Key_LeftDown_0_4F)
                            {
                                testResult.MTF0_4F_LeftDown_V1.Value = mtf.childRects[0].mtfValue ?? 0;
                                testResult.MTF0_4F_LeftDown_V1.Value *= fixConfig.MTF0_4F_LeftDown_V1;
                                testResult.MTF0_4F_LeftDown_V1.LowLimit = recipeConfig.MTF0_4F_LeftDown_V1.Min;
                                testResult.MTF0_4F_LeftDown_V1.UpLimit = recipeConfig.MTF0_4F_LeftDown_V1.Max;
                                ctx.Result.Result &= testResult.MTF0_4F_LeftDown_V1.TestResult;

                                testResult.MTF0_4F_LeftDown_H1.Value = mtf.childRects[1].mtfValue ?? 0;
                                testResult.MTF0_4F_LeftDown_H1.Value *= fixConfig.MTF0_4F_LeftDown_H1;
                                testResult.MTF0_4F_LeftDown_H1.LowLimit = recipeConfig.MTF0_4F_LeftDown_H1.Min;
                                testResult.MTF0_4F_LeftDown_H1.UpLimit = recipeConfig.MTF0_4F_LeftDown_H1.Max;
                                ctx.Result.Result &= testResult.MTF0_4F_LeftDown_H1.TestResult;

                                testResult.MTF0_4F_LeftDown_H2.Value = mtf.childRects[3].mtfValue ?? 0;
                                testResult.MTF0_4F_LeftDown_H2.Value *= fixConfig.MTF0_4F_LeftDown_H2;
                                testResult.MTF0_4F_LeftDown_H2.LowLimit = recipeConfig.MTF0_4F_LeftDown_H2.Min;
                                testResult.MTF0_4F_LeftDown_H2.UpLimit = recipeConfig.MTF0_4F_LeftDown_H2.Max;
                                ctx.Result.Result &= testResult.MTF0_4F_LeftDown_H2.TestResult;

                                testResult.MTF0_4F_LeftDown_V2.Value = mtf.childRects[2].mtfValue ?? 0;
                                testResult.MTF0_4F_LeftDown_V2.Value *= fixConfig.MTF0_4F_LeftDown_V2;
                                testResult.MTF0_4F_LeftDown_V2.LowLimit = recipeConfig.MTF0_4F_LeftDown_V2.Min;
                                testResult.MTF0_4F_LeftDown_V2.UpLimit = recipeConfig.MTF0_4F_LeftDown_V2.Max;
                                ctx.Result.Result &= testResult.MTF0_4F_LeftDown_V2.TestResult;

                                testResult.MTF0_4F_LeftDown_horizontal.Value = mtf.horizontalAverage;
                                testResult.MTF0_4F_LeftDown_horizontal.Value *= fixConfig.MTF0_4F_LeftDown_horizontal;
                                testResult.MTF0_4F_LeftDown_horizontal.LowLimit = recipeConfig.MTF0_4F_LeftDown_horizontal.Min;
                                testResult.MTF0_4F_LeftDown_horizontal.UpLimit = recipeConfig.MTF0_4F_LeftDown_horizontal.Max;
                                ctx.Result.Result &= testResult.MTF0_4F_LeftDown_horizontal.TestResult;

                                testResult.MTF0_4F_LeftDown_Vertical.Value = mtf.verticalAverage;
                                testResult.MTF0_4F_LeftDown_Vertical.Value *= fixConfig.MTF0_4F_LeftDown_Vertical;
                                testResult.MTF0_4F_LeftDown_Vertical.LowLimit = recipeConfig.MTF0_4F_LeftDown_Vertical.Min;
                                testResult.MTF0_4F_LeftDown_Vertical.UpLimit = recipeConfig.MTF0_4F_LeftDown_Vertical.Max;
                                ctx.Result.Result &= testResult.MTF0_4F_LeftDown_Vertical.TestResult;
                            }

                            if (mtf.name == Config.Key_RightDown_0_4F)
                            {
                                testResult.MTF0_4F_RightDown_V1.Value = mtf.childRects[0].mtfValue ?? 0;
                                testResult.MTF0_4F_RightDown_V1.Value *= fixConfig.MTF0_4F_RightDown_V1;
                                testResult.MTF0_4F_RightDown_V1.LowLimit = recipeConfig.MTF0_4F_RightDown_V1.Min;
                                testResult.MTF0_4F_RightDown_V1.UpLimit = recipeConfig.MTF0_4F_RightDown_V1.Max;
                                ctx.Result.Result &= testResult.MTF0_4F_RightDown_V1.TestResult;

                                testResult.MTF0_4F_RightDown_H1.Value = mtf.childRects[1].mtfValue ?? 0;
                                testResult.MTF0_4F_RightDown_H1.Value *= fixConfig.MTF0_4F_RightDown_H1;
                                testResult.MTF0_4F_RightDown_H1.LowLimit = recipeConfig.MTF0_4F_RightDown_H1.Min;
                                testResult.MTF0_4F_RightDown_H1.UpLimit = recipeConfig.MTF0_4F_RightDown_H1.Max;
                                ctx.Result.Result &= testResult.MTF0_4F_RightDown_H1.TestResult;

                                testResult.MTF0_4F_RightDown_H2.Value = mtf.childRects[3].mtfValue ?? 0;
                                testResult.MTF0_4F_RightDown_H2.Value *= fixConfig.MTF0_4F_RightDown_H2;
                                testResult.MTF0_4F_RightDown_H2.LowLimit = recipeConfig.MTF0_4F_RightDown_H2.Min;
                                testResult.MTF0_4F_RightDown_H2.UpLimit = recipeConfig.MTF0_4F_RightDown_H2.Max;
                                ctx.Result.Result &= testResult.MTF0_4F_RightDown_H2.TestResult;

                                testResult.MTF0_4F_RightDown_V2.Value = mtf.childRects[2].mtfValue ?? 0;
                                testResult.MTF0_4F_RightDown_V2.Value *= fixConfig.MTF0_4F_RightDown_V2;
                                testResult.MTF0_4F_RightDown_V2.LowLimit = recipeConfig.MTF0_4F_RightDown_V2.Min;
                                testResult.MTF0_4F_RightDown_V2.UpLimit = recipeConfig.MTF0_4F_RightDown_V2.Max;
                                ctx.Result.Result &= testResult.MTF0_4F_RightDown_V2.TestResult;

                                testResult.MTF0_4F_RightDown_horizontal.Value = mtf.horizontalAverage;
                                testResult.MTF0_4F_RightDown_horizontal.Value *= fixConfig.MTF0_4F_RightDown_horizontal;
                                testResult.MTF0_4F_RightDown_horizontal.LowLimit = recipeConfig.MTF0_4F_RightDown_horizontal.Min;
                                testResult.MTF0_4F_RightDown_horizontal.UpLimit = recipeConfig.MTF0_4F_RightDown_horizontal.Max;
                                ctx.Result.Result &= testResult.MTF0_4F_RightDown_horizontal.TestResult;

                                testResult.MTF0_4F_RightDown_Vertical.Value = mtf.verticalAverage;
                                testResult.MTF0_4F_RightDown_Vertical.Value *= fixConfig.MTF0_4F_RightDown_Vertical;
                                testResult.MTF0_4F_RightDown_Vertical.LowLimit = recipeConfig.MTF0_4F_RightDown_Vertical.Min;
                                testResult.MTF0_4F_RightDown_Vertical.UpLimit = recipeConfig.MTF0_4F_RightDown_Vertical.Max;
                                ctx.Result.Result &= testResult.MTF0_4F_RightDown_Vertical.TestResult;
                            }

                            if (mtf.name == Config.Key_LeftUp_0_8F)
                            {
                                testResult.MTF0_8F_LeftUp_V1.Value = mtf.childRects[0].mtfValue ?? 0;
                                testResult.MTF0_8F_LeftUp_V1.Value *= fixConfig.MTF0_8F_LeftUp_V1;
                                testResult.MTF0_8F_LeftUp_V1.LowLimit = recipeConfig.MTF0_8F_LeftUp_V1.Min;
                                testResult.MTF0_8F_LeftUp_V1.UpLimit = recipeConfig.MTF0_8F_LeftUp_V1.Max;
                                ctx.Result.Result &= testResult.MTF0_8F_LeftUp_V1.TestResult;

                                testResult.MTF0_8F_LeftUp_H1.Value = mtf.childRects[1].mtfValue ?? 0;
                                testResult.MTF0_8F_LeftUp_H1.Value *= fixConfig.MTF0_8F_LeftUp_H1;
                                testResult.MTF0_8F_LeftUp_H1.LowLimit = recipeConfig.MTF0_8F_LeftUp_H1.Min;
                                testResult.MTF0_8F_LeftUp_H1.UpLimit = recipeConfig.MTF0_8F_LeftUp_H1.Max;
                                ctx.Result.Result &= testResult.MTF0_8F_LeftUp_H1.TestResult;

                                testResult.MTF0_8F_LeftUp_H2.Value = mtf.childRects[3].mtfValue ?? 0;
                                testResult.MTF0_8F_LeftUp_H2.Value *= fixConfig.MTF0_8F_LeftUp_H2;
                                testResult.MTF0_8F_LeftUp_H2.LowLimit = recipeConfig.MTF0_8F_LeftUp_H2.Min;
                                testResult.MTF0_8F_LeftUp_H2.UpLimit = recipeConfig.MTF0_8F_LeftUp_H2.Max;
                                ctx.Result.Result &= testResult.MTF0_8F_LeftUp_H2.TestResult;

                                testResult.MTF0_8F_LeftUp_V2.Value = mtf.childRects[2].mtfValue ?? 0;
                                testResult.MTF0_8F_LeftUp_V2.Value *= fixConfig.MTF0_8F_LeftUp_V2;
                                testResult.MTF0_8F_LeftUp_V2.LowLimit = recipeConfig.MTF0_8F_LeftUp_V2.Min;
                                testResult.MTF0_8F_LeftUp_V2.UpLimit = recipeConfig.MTF0_8F_LeftUp_V2.Max;
                                ctx.Result.Result &= testResult.MTF0_8F_LeftUp_V2.TestResult;

                                testResult.MTF0_8F_LeftUp_horizontal.Value = mtf.horizontalAverage;
                                testResult.MTF0_8F_LeftUp_horizontal.Value *= fixConfig.MTF0_8F_LeftUp_horizontal;
                                testResult.MTF0_8F_LeftUp_horizontal.LowLimit = recipeConfig.MTF0_8F_LeftUp_horizontal.Min;
                                testResult.MTF0_8F_LeftUp_horizontal.UpLimit = recipeConfig.MTF0_8F_LeftUp_horizontal.Max;
                                ctx.Result.Result &= testResult.MTF0_8F_LeftUp_horizontal.TestResult;

                                testResult.MTF0_8F_LeftUp_Vertical.Value = mtf.verticalAverage;
                                testResult.MTF0_8F_LeftUp_Vertical.Value *= fixConfig.MTF0_8F_LeftUp_Vertical;
                                testResult.MTF0_8F_LeftUp_Vertical.LowLimit = recipeConfig.MTF0_8F_LeftUp_Vertical.Min;
                                testResult.MTF0_8F_LeftUp_Vertical.UpLimit = recipeConfig.MTF0_8F_LeftUp_Vertical.Max;
                                ctx.Result.Result &= testResult.MTF0_8F_LeftUp_Vertical.TestResult;
                            }

                            if (mtf.name == Config.Key_RightUp_0_8F)
                            {
                                testResult.MTF0_8F_RightUp_V1.Value = mtf.childRects[0].mtfValue ?? 0;
                                testResult.MTF0_8F_RightUp_V1.Value *= fixConfig.MTF0_8F_RightUp_V1;
                                testResult.MTF0_8F_RightUp_V1.LowLimit = recipeConfig.MTF0_8F_RightUp_V1.Min;
                                testResult.MTF0_8F_RightUp_V1.UpLimit = recipeConfig.MTF0_8F_RightUp_V1.Max;
                                ctx.Result.Result &= testResult.MTF0_8F_RightUp_V1.TestResult;

                                testResult.MTF0_8F_RightUp_H1.Value = mtf.childRects[1].mtfValue ?? 0;
                                testResult.MTF0_8F_RightUp_H1.Value *= fixConfig.MTF0_8F_RightUp_H1;
                                testResult.MTF0_8F_RightUp_H1.LowLimit = recipeConfig.MTF0_8F_RightUp_H1.Min;
                                testResult.MTF0_8F_RightUp_H1.UpLimit = recipeConfig.MTF0_8F_RightUp_H1.Max;
                                ctx.Result.Result &= testResult.MTF0_8F_RightUp_H1.TestResult;

                                testResult.MTF0_8F_RightUp_H2.Value = mtf.childRects[3].mtfValue ?? 0;
                                testResult.MTF0_8F_RightUp_H2.Value *= fixConfig.MTF0_8F_RightUp_H2;
                                testResult.MTF0_8F_RightUp_H2.LowLimit = recipeConfig.MTF0_8F_RightUp_H2.Min;
                                testResult.MTF0_8F_RightUp_H2.UpLimit = recipeConfig.MTF0_8F_RightUp_H2.Max;
                                ctx.Result.Result &= testResult.MTF0_8F_RightUp_H2.TestResult;

                                testResult.MTF0_8F_RightUp_V2.Value = mtf.childRects[2].mtfValue ?? 0;
                                testResult.MTF0_8F_RightUp_V2.Value *= fixConfig.MTF0_8F_RightUp_V2;
                                testResult.MTF0_8F_RightUp_V2.LowLimit = recipeConfig.MTF0_8F_RightUp_V2.Min;
                                testResult.MTF0_8F_RightUp_V2.UpLimit = recipeConfig.MTF0_8F_RightUp_V2.Max;
                                ctx.Result.Result &= testResult.MTF0_8F_RightUp_V2.TestResult;

                                testResult.MTF0_8F_RightUp_horizontal.Value = mtf.horizontalAverage;
                                testResult.MTF0_8F_RightUp_horizontal.Value *= fixConfig.MTF0_8F_RightUp_horizontal;
                                testResult.MTF0_8F_RightUp_horizontal.LowLimit = recipeConfig.MTF0_8F_RightUp_horizontal.Min;
                                testResult.MTF0_8F_RightUp_horizontal.UpLimit = recipeConfig.MTF0_8F_RightUp_horizontal.Max;
                                ctx.Result.Result &= testResult.MTF0_8F_RightUp_horizontal.TestResult;

                                testResult.MTF0_8F_RightUp_Vertical.Value = mtf.verticalAverage;
                                testResult.MTF0_8F_RightUp_Vertical.Value *= fixConfig.MTF0_8F_RightUp_Vertical;
                                testResult.MTF0_8F_RightUp_Vertical.LowLimit = recipeConfig.MTF0_8F_RightUp_Vertical.Min;
                                testResult.MTF0_8F_RightUp_Vertical.UpLimit = recipeConfig.MTF0_8F_RightUp_Vertical.Max;
                                ctx.Result.Result &= testResult.MTF0_8F_RightUp_Vertical.TestResult;
                            }

                            if (mtf.name == Config.Key_LeftDown_0_8F)
                            {
                                testResult.MTF0_8F_LeftDown_V1.Value = mtf.childRects[0].mtfValue ?? 0;
                                testResult.MTF0_8F_LeftDown_V1.Value *= fixConfig.MTF0_8F_LeftDown_V1;
                                testResult.MTF0_8F_LeftDown_V1.LowLimit = recipeConfig.MTF0_8F_LeftDown_V1.Min;
                                testResult.MTF0_8F_LeftDown_V1.UpLimit = recipeConfig.MTF0_8F_LeftDown_V1.Max;
                                ctx.Result.Result &= testResult.MTF0_8F_LeftDown_V1.TestResult;

                                testResult.MTF0_8F_LeftDown_H1.Value = mtf.childRects[1].mtfValue ?? 0;
                                testResult.MTF0_8F_LeftDown_H1.Value *= fixConfig.MTF0_8F_LeftDown_H1;
                                testResult.MTF0_8F_LeftDown_H1.LowLimit = recipeConfig.MTF0_8F_LeftDown_H1.Min;
                                testResult.MTF0_8F_LeftDown_H1.UpLimit = recipeConfig.MTF0_8F_LeftDown_H1.Max;
                                ctx.Result.Result &= testResult.MTF0_8F_LeftDown_H1.TestResult;

                                testResult.MTF0_8F_LeftDown_H2.Value = mtf.childRects[3].mtfValue ?? 0;
                                testResult.MTF0_8F_LeftDown_H2.Value *= fixConfig.MTF0_8F_LeftDown_H2;
                                testResult.MTF0_8F_LeftDown_H2.LowLimit = recipeConfig.MTF0_8F_LeftDown_H2.Min;
                                testResult.MTF0_8F_LeftDown_H2.UpLimit = recipeConfig.MTF0_8F_LeftDown_H2.Max;
                                ctx.Result.Result &= testResult.MTF0_8F_LeftDown_H2.TestResult;

                                testResult.MTF0_8F_LeftDown_V2.Value = mtf.childRects[2].mtfValue ?? 0;
                                testResult.MTF0_8F_LeftDown_V2.Value *= fixConfig.MTF0_8F_LeftDown_V2;
                                testResult.MTF0_8F_LeftDown_V2.LowLimit = recipeConfig.MTF0_8F_LeftDown_V2.Min;
                                testResult.MTF0_8F_LeftDown_V2.UpLimit = recipeConfig.MTF0_8F_LeftDown_V2.Max;
                                ctx.Result.Result &= testResult.MTF0_8F_LeftDown_V2.TestResult;

                                testResult.MTF0_8F_LeftDown_horizontal.Value = mtf.horizontalAverage;
                                testResult.MTF0_8F_LeftDown_horizontal.Value *= fixConfig.MTF0_8F_LeftDown_horizontal;
                                testResult.MTF0_8F_LeftDown_horizontal.LowLimit = recipeConfig.MTF0_8F_LeftDown_horizontal.Min;
                                testResult.MTF0_8F_LeftDown_horizontal.UpLimit = recipeConfig.MTF0_8F_LeftDown_horizontal.Max;
                                ctx.Result.Result &= testResult.MTF0_8F_LeftDown_horizontal.TestResult;

                                testResult.MTF0_8F_LeftDown_Vertical.Value = mtf.verticalAverage;
                                testResult.MTF0_8F_LeftDown_Vertical.Value *= fixConfig.MTF0_8F_LeftDown_Vertical;
                                testResult.MTF0_8F_LeftDown_Vertical.LowLimit = recipeConfig.MTF0_8F_LeftDown_Vertical.Min;
                                testResult.MTF0_8F_LeftDown_Vertical.UpLimit = recipeConfig.MTF0_8F_LeftDown_Vertical.Max;
                                ctx.Result.Result &= testResult.MTF0_8F_LeftDown_Vertical.TestResult;
                            }

                            if (mtf.name == Config.Key_RightDown_0_8F)
                            {
                                testResult.MTF0_8F_RightDown_V1.Value = mtf.childRects[0].mtfValue ?? 0;
                                testResult.MTF0_8F_RightDown_V1.Value *= fixConfig.MTF0_8F_RightDown_V1;
                                testResult.MTF0_8F_RightDown_V1.LowLimit = recipeConfig.MTF0_8F_RightDown_V1.Min;
                                testResult.MTF0_8F_RightDown_V1.UpLimit = recipeConfig.MTF0_8F_RightDown_V1.Max;
                                ctx.Result.Result &= testResult.MTF0_8F_RightDown_V1.TestResult;

                                testResult.MTF0_8F_RightDown_H1.Value = mtf.childRects[1].mtfValue ?? 0;
                                testResult.MTF0_8F_RightDown_H1.Value *= fixConfig.MTF0_8F_RightDown_H1;
                                testResult.MTF0_8F_RightDown_H1.LowLimit = recipeConfig.MTF0_8F_RightDown_H1.Min;
                                testResult.MTF0_8F_RightDown_H1.UpLimit = recipeConfig.MTF0_8F_RightDown_H1.Max;
                                ctx.Result.Result &= testResult.MTF0_8F_RightDown_H1.TestResult;

                                testResult.MTF0_8F_RightDown_H2.Value = mtf.childRects[3].mtfValue ?? 0;
                                testResult.MTF0_8F_RightDown_H2.Value *= fixConfig.MTF0_8F_RightDown_H2;
                                testResult.MTF0_8F_RightDown_H2.LowLimit = recipeConfig.MTF0_8F_RightDown_H2.Min;
                                testResult.MTF0_8F_RightDown_H2.UpLimit = recipeConfig.MTF0_8F_RightDown_H2.Max;
                                ctx.Result.Result &= testResult.MTF0_8F_RightDown_H2.TestResult;

                                testResult.MTF0_8F_RightDown_V2.Value = mtf.childRects[2].mtfValue ?? 0;
                                testResult.MTF0_8F_RightDown_V2.Value *= fixConfig.MTF0_8F_RightDown_V2;
                                testResult.MTF0_8F_RightDown_V2.LowLimit = recipeConfig.MTF0_8F_RightDown_V2.Min;
                                testResult.MTF0_8F_RightDown_V2.UpLimit = recipeConfig.MTF0_8F_RightDown_V2.Max;
                                ctx.Result.Result &= testResult.MTF0_8F_RightDown_V2.TestResult;

                                testResult.MTF0_8F_RightDown_horizontal.Value = mtf.horizontalAverage;
                                testResult.MTF0_8F_RightDown_horizontal.Value *= fixConfig.MTF0_8F_RightDown_horizontal;
                                testResult.MTF0_8F_RightDown_horizontal.LowLimit = recipeConfig.MTF0_8F_RightDown_horizontal.Min;
                                testResult.MTF0_8F_RightDown_horizontal.UpLimit = recipeConfig.MTF0_8F_RightDown_horizontal.Max;
                                ctx.Result.Result &= testResult.MTF0_8F_RightDown_horizontal.TestResult;

                                testResult.MTF0_8F_RightDown_Vertical.Value = mtf.verticalAverage;
                                testResult.MTF0_8F_RightDown_Vertical.Value *= fixConfig.MTF0_8F_RightDown_Vertical;
                                testResult.MTF0_8F_RightDown_Vertical.LowLimit = recipeConfig.MTF0_8F_RightDown_Vertical.Min;
                                testResult.MTF0_8F_RightDown_Vertical.UpLimit = recipeConfig.MTF0_8F_RightDown_Vertical.Max;
                                ctx.Result.Result &= testResult.MTF0_8F_RightDown_Vertical.TestResult;
                            }
  
                        }
                        testResult.MTFDetailViewReslut = mtfDetail;
                    }
                }


                ctx.Result.ViewResultJson = JsonConvert.SerializeObject(testResult);
                ctx.ObjectiveTestResult.MTFHVARTestResult = JsonConvert.DeserializeObject<MTFHARVTestResult>(ctx.Result.ViewResultJson) ?? new MTFHARVTestResult();
                return true;
            }
            catch (Exception ex)
            {
                log?.Error(ex);
                return false;
            }
        }

        public override void Render(IProcessExecutionContext ctx)
        {
            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) return;
            MTFHVARViewTestResult testResult = JsonConvert.DeserializeObject<MTFHVARViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) return;

            int id = 0;
            if (testResult.MTFDetailViewReslut.MTFResult.result.Count != 0)
            {
                foreach (var item in testResult.MTFDetailViewReslut.MTFResult.result)
                {
                    id++;
                    DVRectangleText Rectangle = new();
                    Rectangle.Attribute.Rect = new Rect(item.x, item.y, item.w, item.h);
                    Rectangle.Attribute.Brush = Brushes.Transparent;
                    Rectangle.Attribute.Pen = new Pen(Brushes.Red, 1);
                    Rectangle.Attribute.Id = id;
                    Rectangle.Attribute.Text = item.name + "_" + item.id;
                    Rectangle.Attribute.Msg = item.mtfValue.ToString();
                    Rectangle.Render();
                    ctx.ImageView.AddVisual(Rectangle);
                }
            }
        }

        public override string GenText(IProcessExecutionContext ctx)
        {
            var result = ctx.Result;
            string outtext = string.Empty;
            outtext += $"MTFHV 画面结果" + Environment.NewLine;


            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) return outtext;
            MTFHVARViewTestResult testResult = JsonConvert.DeserializeObject<MTFHVARViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) return outtext;

            outtext += $"name,horizontalAverage,verticalAverage,Average," + Environment.NewLine;

            if (testResult.MTFDetailViewReslut.MTFResult != null)
            {
                foreach (var item in testResult.MTFDetailViewReslut.MTFResult.resultChild)
                {
                    outtext += $"{item.name},{item.horizontalAverage},{item.verticalAverage},{item.Average}" + Environment.NewLine;
                }
            }
            return outtext;
        }


        public override IRecipeConfig GetRecipeConfig()
        {
            return RecipeManager.GetInstance().RecipeConfig.GetRequiredService<MTFHVRecipeConfig>();
        }

        public override IFixConfig GetFixConfig()
        {
            return FixManager.GetInstance().FixConfig.GetRequiredService<MTFHVARFixConfig>();
        }
    }
}
