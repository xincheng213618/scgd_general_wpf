using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using Newtonsoft.Json;
using SqlSugar;
using System.Globalization;
using System.IO;
using System.Threading;

namespace ProjectARVRPro.Process.DemuraAOI
{
    public sealed class DemuraAoiParser
    {
        private const int RebuildPixelsResultType = 32;
        private const int GradingV2ResultType = 243;
        private const int BlackScreenResultType = 49;
        private static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".bmp", ".jpg", ".jpeg", ".png", ".tif", ".tiff"
        };

        public static Task<DemuraAoiParseResult> ParseAsync(int batchId, DemuraAoiProcessConfig config, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(config);
            return Task.Run(() => Parse(batchId, config, cancellationToken), cancellationToken);
        }

        private static DemuraAoiParseResult Parse(int batchId, DemuraAoiProcessConfig config, CancellationToken cancellationToken)
        {
            var result = new DemuraAoiParseResult { BatchId = batchId };
            if (batchId <= 0)
            {
                result.DataErrors.Add("批次ID无效，无法解析Demura AOI结果。");
                return result;
            }
            if (!MySqlControl.GetInstance().IsConnect)
            {
                result.DataErrors.Add("数据库未连接，无法解析Demura AOI结果。");
                return result;
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var db = new SqlSugarClient(new ConnectionConfig
                {
                    ConnectionString = MySqlControl.GetConnectionString(),
                    DbType = DbType.MySql,
                    IsAutoCloseConnection = true
                });

                List<AlgResultMasterModel> masters = db.Queryable<AlgResultMasterModel>()
                    .Where(master => master.BatchId == batchId)
                    .OrderBy(master => master.Id, OrderByType.Desc)
                    .ToList();

                if (masters.Count == 0)
                {
                    result.DataErrors.Add($"批次{batchId}未查询到算法主结果。");
                    return result;
                }

                ParseW255(db, masters, config, result, cancellationToken);
                ParseGrading(db, masters, config, result, cancellationToken);
                ParseBlack(db, masters, config, result, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                result.DataErrors.Add($"读取Demura AOI数据库结果异常: {ex.Message}");
            }

            return result;
        }

        private static void ParseW255(SqlSugarClient db, List<AlgResultMasterModel> masters, DemuraAoiProcessConfig config, DemuraAoiParseResult result, CancellationToken cancellationToken)
        {
            string keyword = string.IsNullOrWhiteSpace(config.W255Keyword) ? "W255" : config.W255Keyword.Trim();
            List<int> masterIds = masters.Select(master => master.Id).ToList();
            List<DemuraAoiFileRecord> files = db.Queryable<AlgResultPoiCieFileModel>()
                .Where(file => masterIds.Contains(file.Pid))
                .Select(file => new DemuraAoiFileRecord
                {
                    Id = file.Id,
                    Pid = file.Pid,
                    FileName = file.FileName,
                    FileUrl = file.FileUrl
                })
                .ToList();

            Dictionary<int, AlgResultMasterModel> masterMap = masters.ToDictionary(master => master.Id);
            var candidates = files
                .Where(file => IsW255Image(file, keyword))
                .Select(file => new { File = file, Master = masterMap[file.Pid] })
                .OrderByDescending(item => NormalizeResultType((int)item.Master.ImgFileType) == RebuildPixelsResultType)
                .ThenByDescending(item => item.Master.Id)
                .ThenByDescending(item => item.File.Id)
                .ToList();

            if (candidates.Count == 0)
            {
                AddIssue(result, config.RequireW255, $"未找到文件名包含“{keyword}”的W255图像结果。");
                return;
            }

            var selected = candidates[0];
            if (candidates.Count > 1)
                result.Warnings.Add($"找到{candidates.Count}个W255候选图像，已选择MasterId={selected.Master.Id}、FileId={selected.File.Id}的最新结果。");

            string filePath = selected.File.FileUrl!;
            if (!WaitForFile(filePath, config, cancellationToken))
            {
                AddIssue(result, config.RequireW255, $"W255图像文件不存在或未写入完成: {filePath}");
                return;
            }

            W255UniformityResult uniformity = CalculateW255WithRetry(filePath, config, cancellationToken);
            result.W255 = uniformity;
            if (!uniformity.Success)
                AddIssue(result, config.RequireW255, uniformity.ErrorMessage);
        }

        private static void ParseGrading(SqlSugarClient db, List<AlgResultMasterModel> masters, DemuraAoiProcessConfig config, DemuraAoiParseResult result, CancellationToken cancellationToken)
        {
            AlgResultMasterModel? master = SelectLatestMaster(masters, GradingV2ResultType);
            if (master == null)
            {
                AddIssue(result, config.RequireAoiGrading, $"未找到AOI分级算法结果(type={GradingV2ResultType}/-13)。");
                return;
            }
            if (master.ResultCode.HasValue && master.ResultCode.Value != 0)
            {
                AddIssue(result, config.RequireAoiGrading, $"AOI分级算法执行失败: MasterId={master.Id}, ResultCode={master.ResultCode}, Result={master.Result}");
                return;
            }

            if (!TryReadPayload<GradingPayload>(db, master, config, cancellationToken, out GradingPayload? payload, out string resultFile, out string error))
            {
                AddIssue(result, config.RequireAoiGrading, $"AOI分级结果解析失败: {error}");
                return;
            }

            var semanticErrors = new List<string>();
            ValidateGrade(payload!.GradeLevel, "AOI GradeLevel", semanticErrors);
            ValidateNumber(payload.MaxDefectDensity, "MaxDefectDensity", false, semanticErrors);
            ValidateNumber(payload.DarkTotalDefects, "DarkTotalDefects", true, semanticErrors);
            ValidateNumber(payload.BrightTotalDefects, "BrightTotalDefects", true, semanticErrors);
            if (semanticErrors.Count > 0)
            {
                AddIssue(result, config.RequireAoiGrading, string.Join("; ", semanticErrors));
                return;
            }

            result.Grading = new DemuraAoiGradingData
            {
                MasterId = master.Id,
                ResultFile = resultFile,
                GradeLevel = payload.GradeLevel!.Trim(),
                MaxDefectDensity = payload.MaxDefectDensity!.Value,
                DarkTotalDefects = payload.DarkTotalDefects!.Value,
                BrightTotalDefects = payload.BrightTotalDefects!.Value,
                TimeStamp = payload.TimeStamp ?? string.Empty
            };
        }

        private static void ParseBlack(SqlSugarClient db, List<AlgResultMasterModel> masters, DemuraAoiProcessConfig config, DemuraAoiParseResult result, CancellationToken cancellationToken)
        {
            AlgResultMasterModel? master = SelectLatestMaster(masters, BlackScreenResultType);
            if (master == null)
            {
                AddIssue(result, config.RequireBlackResult, $"未找到黑场亮点算法结果(type={BlackScreenResultType})。");
                return;
            }
            if (master.ResultCode.HasValue && master.ResultCode.Value != 0)
            {
                AddIssue(result, config.RequireBlackResult, $"黑场亮点算法执行失败: MasterId={master.Id}, ResultCode={master.ResultCode}, Result={master.Result}");
                return;
            }

            if (!TryReadPayload<BlackPayload>(db, master, config, cancellationToken, out BlackPayload? payload, out string resultFile, out string error))
            {
                AddIssue(result, config.RequireBlackResult, $"黑场结果解析失败: {error}");
                return;
            }

            var semanticErrors = new List<string>();
            ValidateGrade(payload!.GradeLevel, "Black GradeLevel", semanticErrors);
            ValidateNumber(payload.BrightCount, "BrightCount", true, semanticErrors);
            if (semanticErrors.Count > 0)
            {
                AddIssue(result, config.RequireBlackResult, string.Join("; ", semanticErrors));
                return;
            }

            result.Black = new DemuraAoiBlackData
            {
                MasterId = master.Id,
                ResultFile = resultFile,
                BrightCount = payload.BrightCount!.Value,
                GradeLevel = payload.GradeLevel!.Trim(),
                TimeStamp = payload.TimeStamp ?? string.Empty
            };
        }

        private static bool TryReadPayload<T>(SqlSugarClient db, AlgResultMasterModel master, DemuraAoiProcessConfig config, CancellationToken cancellationToken, out T? payload, out string resultFile, out string error) where T : class
        {
            payload = null;
            resultFile = string.Empty;
            error = string.Empty;
            List<DetailCommonModel> details = db.Queryable<DetailCommonModel>().Where(detail => detail.PId == master.Id).ToList();
            if (details.Count != 1)
            {
                error = $"MasterId={master.Id}对应的common明细数量为{details.Count}，期望为1。";
                return false;
            }

            ResultFile? pointer;
            try
            {
                pointer = JsonConvert.DeserializeObject<ResultFile>(details[0].ResultJson);
            }
            catch (Exception ex)
            {
                error = $"结果文件指针JSON无效: {ex.Message}";
                return false;
            }
            if (string.IsNullOrWhiteSpace(pointer?.ResultFileName))
            {
                error = "结果文件指针缺少ResultFileName。";
                return false;
            }

            resultFile = ResolveResultFile(pointer.ResultFileName, master);
            int attempts = Math.Clamp(config.FileReadRetryCount, 1, 20);
            int delayMs = Math.Clamp(config.FileReadRetryDelayMs, 0, 2000);
            Exception? lastError = null;
            for (int attempt = 1; attempt <= attempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (!File.Exists(resultFile))
                        throw new FileNotFoundException("结果文件不存在。", resultFile);
                    string json = ReadAllTextShared(resultFile);
                    if (string.IsNullOrWhiteSpace(json))
                        throw new InvalidDataException("结果文件内容为空。");
                    payload = JsonConvert.DeserializeObject<T>(json) ?? throw new JsonSerializationException("结果JSON反序列化为空。");
                    return true;
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
                {
                    lastError = ex;
                    if (attempt < attempts && delayMs > 0)
                        Thread.Sleep(delayMs);
                }
            }

            error = $"读取结果文件失败: {resultFile}; {lastError?.Message}";
            return false;
        }

        private static string ReadAllTextShared(string filePath)
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        private static bool WaitForFile(string filePath, DemuraAoiProcessConfig config, CancellationToken cancellationToken)
        {
            int attempts = Math.Clamp(config.FileReadRetryCount, 1, 20);
            int delayMs = Math.Clamp(config.FileReadRetryDelayMs, 0, 2000);
            for (int attempt = 1; attempt <= attempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (File.Exists(filePath)) return true;
                if (attempt < attempts && delayMs > 0) Thread.Sleep(delayMs);
            }
            return false;
        }

        private static W255UniformityResult CalculateW255WithRetry(string filePath, DemuraAoiProcessConfig config, CancellationToken cancellationToken)
        {
            int attempts = Math.Clamp(config.FileReadRetryCount, 1, 20);
            int delayMs = Math.Clamp(config.FileReadRetryDelayMs, 0, 2000);
            W255UniformityResult result = new W255UniformityResult { FilePath = filePath, ErrorMessage = "W255图像读取失败。" };
            for (int attempt = 1; attempt <= attempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                result = W255UniformityCalculator.Calculate(filePath, config.W255RoiRadius);
                if (result.Success) return result;
                if (attempt < attempts && delayMs > 0) Thread.Sleep(delayMs);
            }
            return result;
        }

        private static AlgResultMasterModel? SelectLatestMaster(IEnumerable<AlgResultMasterModel> masters, int normalizedType)
        {
            return masters.Where(master => NormalizeResultType((int)master.ImgFileType) == normalizedType)
                .OrderByDescending(master => master.Id)
                .FirstOrDefault();
        }

        private static int NormalizeResultType(int value)
        {
            return value < 0 ? value & byte.MaxValue : value;
        }

        private static bool IsW255Image(DemuraAoiFileRecord file, string keyword)
        {
            if (string.IsNullOrWhiteSpace(file.FileUrl)) return false;
            string fileName = Path.GetFileName(file.FileUrl);
            if (!fileName.Contains(keyword, StringComparison.OrdinalIgnoreCase) &&
                !(file.FileName?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false))
                return false;
            return SupportedImageExtensions.Contains(Path.GetExtension(file.FileUrl));
        }

        private static string ResolveResultFile(string rawPath, AlgResultMasterModel master)
        {
            string path = rawPath.Trim().Trim('"');
            if (Path.IsPathRooted(path)) return Path.GetFullPath(path);

            string? firstCombined = null;
            foreach (string? baseFile in new[] { master.ResultImagFile, master.ImgFile })
            {
                string? directory = string.IsNullOrWhiteSpace(baseFile) ? null : Path.GetDirectoryName(baseFile);
                if (string.IsNullOrWhiteSpace(directory)) continue;
                string combined = Path.Combine(directory, path);
                firstCombined ??= combined;
                if (File.Exists(combined)) return Path.GetFullPath(combined);
            }
            return firstCombined == null ? path : Path.GetFullPath(firstCombined);
        }

        private static void ValidateGrade(string? grade, string name, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(grade)) errors.Add($"{name}缺失或为空");
        }

        private static void ValidateNumber(double? value, string name, bool requireInteger, List<string> errors)
        {
            if (!value.HasValue)
            {
                errors.Add($"{name}字段缺失");
                return;
            }
            if (!double.IsFinite(value.Value) || value.Value < 0)
            {
                errors.Add($"{name}不是有效的非负数: {value.Value.ToString(CultureInfo.InvariantCulture)}");
                return;
            }
            if (requireInteger && Math.Abs(value.Value - Math.Round(value.Value)) > 1e-6)
                errors.Add($"{name}应为整数: {value.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        private static void AddIssue(DemuraAoiParseResult result, bool required, string message)
        {
            if (required) result.DataErrors.Add(message);
            else result.Warnings.Add(message);
        }

        private sealed class GradingPayload
        {
            public string? GradeLevel { get; set; }
            public double? MaxDefectDensity { get; set; }
            public double? DarkTotalDefects { get; set; }
            public double? BrightTotalDefects { get; set; }
            public string? TimeStamp { get; set; }
        }

        private sealed class BlackPayload
        {
            public double? BrightCount { get; set; }
            public string? GradeLevel { get; set; }
            public string? TimeStamp { get; set; }
        }

        private sealed class DemuraAoiFileRecord
        {
            public int Id { get; set; }
            public int Pid { get; set; }
            public string? FileName { get; set; }
            public string? FileUrl { get; set; }
        }
    }

    public static class DemuraAoiEvaluator
    {
        public static DemuraAoiEvaluationResult Evaluate(DemuraAoiParseResult parseResult, DemuraAoiRecipeConfig recipe)
        {
            ArgumentNullException.ThrowIfNull(parseResult);
            ArgumentNullException.ThrowIfNull(recipe);

            var result = new DemuraAoiEvaluationResult();
            AddBooleanItem(result.Items, "AOIDataIntegrity", parseResult.IsDataValid,
                parseResult.IsDataValid ? "PASS" : string.Join("; ", parseResult.DataErrors));

            if (parseResult.W255?.Success == true)
            {
                double value = recipe.W255Uniformity.Apply(parseResult.W255.Uniformity);
                result.Items.Add(new ObjectiveTestItem
                {
                    Name = "W255LuminanceUniformity",
                    Value = value,
                    TestValue = (value * 100).ToString("F4", CultureInfo.InvariantCulture) + "%",
                    Unit = "%",
                    LowLimit = recipe.W255Uniformity.Min,
                    UpLimit = recipe.W255Uniformity.Max
                });
            }

            if (parseResult.Grading != null)
            {
                AddGradeItem(result.Items, "AOIGrade", parseResult.Grading.GradeLevel, recipe.AoiGrade);
                AddRangeItem(result.Items, "MaxDefectDensity", parseResult.Grading.MaxDefectDensity, recipe.MaxDefectDensity);
                AddRangeItem(result.Items, "DarkTotalDefects", parseResult.Grading.DarkTotalDefects, recipe.DarkTotalDefects, "count");
                AddRangeItem(result.Items, "BrightTotalDefects", parseResult.Grading.BrightTotalDefects, recipe.BrightTotalDefects, "count");
            }

            if (parseResult.Black != null)
            {
                AddGradeItem(result.Items, "BlackGrade", parseResult.Black.GradeLevel, recipe.BlackGrade);
                AddRangeItem(result.Items, "BlackBrightCount", parseResult.Black.BrightCount, recipe.BlackBrightCount, "count");
            }

            foreach (ObjectiveTestItem item in result.Items.Where(item => item.Name != "AOIDataIntegrity" && !item.TestResult))
                result.SpecificationFailures.Add($"{item.Name}: value={item.TestValue}, range=[{item.LowLimit},{item.UpLimit}]");

            if (!parseResult.IsDataValid)
            {
                result.Outcome = DemuraAoiOutcome.DataError;
                result.Message = "Demura AOI数据异常: " + string.Join("; ", parseResult.DataErrors);
            }
            else if (result.SpecificationFailures.Count > 0)
            {
                result.Outcome = DemuraAoiOutcome.SpecificationNg;
                result.Message = "Demura AOI卡控NG: " + string.Join("; ", result.SpecificationFailures);
            }
            else
            {
                result.Outcome = DemuraAoiOutcome.Pass;
                result.Message = "Demura AOI PASS";
            }

            return result;
        }

        private static void AddBooleanItem(System.Collections.ObjectModel.ObservableCollection<ObjectiveTestItem> items, string name, bool value, string testValue)
        {
            items.Add(new ObjectiveTestItem
            {
                Name = name,
                Value = value ? 1 : 0,
                TestValue = testValue,
                LowLimit = 1,
                UpLimit = 1,
                Unit = string.Empty
            });
        }

        private static void AddGradeItem(System.Collections.ObjectModel.ObservableCollection<ObjectiveTestItem> items, string name, string grade, DemuraAoiGradeRecipe recipe)
        {
            bool pass = recipe.IsAllowed(grade);
            items.Add(new ObjectiveTestItem
            {
                Name = name,
                Value = recipe.IsUse ? (pass ? 1 : 0) : 1,
                TestValue = grade,
                LowLimit = recipe.IsUse ? 1 : 0,
                UpLimit = recipe.IsUse ? 1 : 0,
                Unit = string.Empty
            });
        }

        private static void AddRangeItem(System.Collections.ObjectModel.ObservableCollection<ObjectiveTestItem> items, string name, double rawValue, DemuraAoiRangeRecipe recipe, string unit = "")
        {
            double value = recipe.Apply(rawValue);
            items.Add(new ObjectiveTestItem
            {
                Name = name,
                Value = value,
                TestValue = value.ToString("F6", CultureInfo.InvariantCulture),
                LowLimit = recipe.IsUse ? recipe.Min : 0,
                UpLimit = recipe.IsUse ? recipe.Max : 0,
                Unit = unit
            });
        }
    }
}
