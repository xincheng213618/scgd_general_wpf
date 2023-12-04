using System.Data;
using ColorVision.MySql;

namespace ColorVision.Services.Algorithm.MySql
{
    public class AlgorithmGhostResultModel : PKModel
    {
        public int BatchId { get; set; }

        public int ImgId { get; set; }
        public string? Value { get; set; }
        public string? LedCentersX { get; set; }
        public string? LedCentersY { get; set; }
        public string? BlobGray { get; set; }
        public string? GhostAverageGray { get; set; }
        public string? SingleLedPixelNum { get; set; }
        public string? LEDPixelX { get; set; }

        public string? LEDPixelY { get; set; }
        public string? SingleGhostPixelNum { get; set; }
        public string? GhostPixelX { get; set; }
        public string? GhostPixelY { get; set; }
        public bool Result { get; set; }
    }


    public class AlgorithmGhostResult : BaseDaoMaster<AlgorithmGhostResultModel>
    {
        public AlgorithmGhostResult() : base(string.Empty, "t_scgd_algorithm_ghost_result", "id", false)
        {
        }
        public override DataTable CreateColumns(DataTable dInfo)
        {
            dInfo.Columns.Add("id", typeof(int));
            dInfo.Columns.Add("batch_id", typeof(int));
            dInfo.Columns.Add("img_id", typeof(int));
            dInfo.Columns.Add("value", typeof(string));
            dInfo.Columns.Add("LedCenters_X", typeof(string));
            dInfo.Columns.Add("LedCenters_Y", typeof(string));
            dInfo.Columns.Add("blobGray", typeof(string));
            dInfo.Columns.Add("ghostAverageGray", typeof(string));
            dInfo.Columns.Add("singleLedPixelNum", typeof(string));
            dInfo.Columns.Add("LED_pixel_X", typeof(string));
            dInfo.Columns.Add("LED_pixel_Y", typeof(string));
            dInfo.Columns.Add("singleGhostPixelNum", typeof(string));
            dInfo.Columns.Add("Ghost_pixel_X", typeof(string));
            dInfo.Columns.Add("Ghost_pixel_Y", typeof(string));
            dInfo.Columns.Add("ret", typeof(bool));
            return dInfo;
        }


        public override AlgorithmGhostResultModel GetModel(DataRow item)
        {
            AlgorithmGhostResultModel model = new AlgorithmGhostResultModel
            {
                Id = item.Field<int>("id"),
                BatchId = item.Field<int>("batch_id"),
                ImgId = item.Field<int>("img_id"),
                Value = item.Field<string>("value"),
                LedCentersX = item.Field<string>("LedCenters_X"),
                LedCentersY = item.Field<string>("LedCenters_Y"),
                BlobGray = item.Field<string>("blobGray"),
                GhostAverageGray = item.Field<string>("ghostAverageGray"),
                SingleLedPixelNum = item.Field<string>("singleLedPixelNum"),
                LEDPixelX = item.Field<string>("LED_pixel_X"),
                LEDPixelY = item.Field<string>("LED_pixel_Y"),
                SingleGhostPixelNum = item.Field<string>("singleGhostPixelNum"),
                GhostPixelX = item.Field<string>("Ghost_pixel_X"),
                GhostPixelY = item.Field<string>("Ghost_pixel_Y"),
                Result = item.Field<bool>("ret"),
            };
            return model;
        }

        public override DataRow Model2Row(AlgorithmGhostResultModel item, DataRow row)
        {
            if (item != null)
            {
                if (item.Id > 0) row["id"] = item.Id;
                row["batch_id"] = item.BatchId;
                row["img_id"] = item.ImgId;
                row["value"] = item.Value;
                row["LedCenters_X"] = item.LedCentersX;
                row["LedCenters_Y"] = item.LedCentersY;
                row["blobGray"] = item.BlobGray;
                row["ghostAverageGray"] = item.GhostAverageGray;
                row["singleLedPixelNum"] = item.SingleLedPixelNum;
                row["LED_pixel_X"] = item.LEDPixelX;
                row["LED_pixel_Y"] = item.LEDPixelY;
                row["singleGhostPixelNum"] = item.SingleGhostPixelNum;
                row["Ghost_pixel_X"] = item.GhostPixelX;
                row["Ghost_pixel_Y"] = item.GhostPixelY;
                row["ret"] = item.Result;
            }
            return row;
        }
    }
}
