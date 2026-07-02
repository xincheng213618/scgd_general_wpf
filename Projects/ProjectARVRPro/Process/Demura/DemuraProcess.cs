#pragma warning disable CS8601, CS8602, CS8604
using ColorVision.Database;
using ColorVision.Engine;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace ProjectARVRPro.Process.Demura
{
    public class DemuraProcess : ProcessBase<DemuraProcessConfig>
    {
        private const string ToolFolderName = "DemuraTool";
        private const string ToolExeName = "DemuraTool_x64.exe";
        private const string Prepared128FileName = "G128.csv";
        private const string Prepared255FileName = "G255.csv";
        private static readonly char[] CsvSeparators = new[] { ',', ';', '\t', ' ' };
        private static readonly HashSet<string> PreviewImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".bmp", ".jpg", ".jpeg", ".png", ".tif", ".tiff" };

        public override bool Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null || ctx.ObjectiveTestResult == null) return false;

            var log = ctx.Log;
            DemuraViewTestResult testResult = new DemuraViewTestResult();

            try
            {
                testResult.PreviewImageFile = FindPreviewImageFile(ctx.Batch.Id);
                var masters = AlgResultMasterDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                var candidates = CollectImageConvertCandidates(masters, Config.ImageConvertResultType);

                ApplyCandidate(testResult.W128, FindCandidate(candidates, Config.W128Keyword, Prepared128FileName));
                ApplyCandidate(testResult.W255, FindCandidate(candidates, Config.W255Keyword, Prepared255FileName));

                bool hasRequiredCsv = testResult.W128.SourceExists && testResult.W255.SourceExists;
                if (Config.PrepareDemuraTool)
                {
                    if (hasRequiredCsv)
                    {
                        PrepareDemuraTool(testResult);
                    }
                    else
                    {
                        testResult.Message = "未找到完整的W128/W255 CSV，已跳过DemuraTool准备。";
                    }
                }

                RebuildItems(testResult);
                UpdateObjectiveTestResult(ctx.ObjectiveTestResult, Config.Name, testResult.Items);

                ctx.Result.FileName = GetPreviewFileName(testResult);
                ctx.Result.ViewResultJson = JsonConvert.SerializeObject(testResult);
                ctx.Result.Result &= testResult.Items.All(item => item.TestResult);
                if (!ctx.Result.Result && string.IsNullOrWhiteSpace(ctx.Result.Msg))
                    ctx.Result.Msg = string.IsNullOrWhiteSpace(testResult.Message) ? "Demura CSV准备失败" : testResult.Message;

                return true;
            }
            catch (Exception ex)
            {
                log?.Error(ex);
                ctx.Result.Result = false;
                ctx.Result.Msg = ex.Message;
                return false;
            }
        }

        public override void Render(IProcessExecutionContext ctx)
        {
        }

        public override string GenText(IProcessExecutionContext ctx)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"{Config.Name} 画面结果");

            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) return sb.ToString();

            DemuraTestResult? testResult = JsonConvert.DeserializeObject<DemuraTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) return sb.ToString();

            AppendCsvText(sb, "W128", testResult.W128);
            AppendCsvText(sb, "W255", testResult.W255);
            AppendFileLinkText(sb, "PreviewImage", testResult.PreviewImageFile);
            AppendFileLinkText(sb, "ToolDirectory", testResult.ToolDirectory);
            AppendFileLinkText(sb, "DemuraConfig", testResult.DemuraConfigFile);
            AppendBinLinkText(sb, "DynamicBin", testResult.DynamicBinFile);
            AppendBinLinkText(sb, "MergedBin", testResult.MergedBinFile);
            if (testResult.MergedBinExists)
                sb.AppendLine("FlashAddress:0x00003000");
            sb.AppendLine("BurnStatus:未接入烧录接口，当前仅生成待烧录bin。");
            if (!string.IsNullOrWhiteSpace(testResult.Message))
                sb.AppendLine($"Message:{testResult.Message}");

            sb.AppendLine("Name,Value,Unit,LowLimit,UpLimit,Result");
            foreach (var item in testResult.Items)
            {
                sb.AppendLine($"{item.Name},{item.Value},{item.Unit},{item.LowLimit},{item.UpLimit},{(item.TestResult ? "PASS" : "Fail")}");
            }

            return sb.ToString();
        }

        private void PrepareDemuraTool(DemuraViewTestResult testResult)
        {
            string? bundledToolDir = ResolveBundledToolDirectory();
            if (string.IsNullOrWhiteSpace(bundledToolDir))
            {
                testResult.Message = "未找到打包的DemuraTool_x64资源。";
                return;
            }

            string workDirectory = BuildWorkDirectory();
            CopyMissingDirectoryFiles(bundledToolDir, workDirectory);

            string csvDirectory = Path.Combine(workDirectory, "csv");
            Directory.CreateDirectory(csvDirectory);

            string g128 = Path.Combine(csvDirectory, Prepared128FileName);
            string g255 = Path.Combine(csvDirectory, Prepared255FileName);
            File.Copy(testResult.W128.SourceFile, g128, true);
            File.Copy(testResult.W255.SourceFile, g255, true);
            testResult.W128.PreparedFile = g128;
            testResult.W255.PreparedFile = g255;

            string configFile = Path.Combine(workDirectory, "DemuraConfig.ini");
            File.WriteAllText(configFile, BuildDemuraConfig(), Encoding.UTF8);

            string exeFile = Path.Combine(workDirectory, ToolExeName);
            string staticBin = Path.Combine(workDirectory, "DemuraStatic.bin");
            string dynamicBin = Path.Combine(workDirectory, "DemuraDynamic.bin");
            string mergedBin = Path.Combine(workDirectory, "DemuraMerged.bin");

            testResult.ToolDirectory = workDirectory;
            testResult.WorkDirectory = workDirectory;
            testResult.CsvDirectory = csvDirectory;
            testResult.ToolExecutable = exeFile;
            testResult.DemuraConfigFile = configFile;
            testResult.StaticBinFile = staticBin;
            testResult.DynamicBinFile = dynamicBin;
            testResult.MergedBinFile = mergedBin;
            testResult.ToolPrepared = File.Exists(exeFile) && File.Exists(Path.Combine(workDirectory, "DemuraSaveBin.dll")) && File.Exists(Path.Combine(workDirectory, "GenDemuraCompressionLUT.dll")) && File.Exists(g128) && File.Exists(g255);

            if (Config.GenerateBinWithDll)
            {
                try
                {
                    var binResult = DemuraBinGenerator.Generate(workDirectory, g128, g255, Config.InputWidth, Config.InputHeight, Config.OutputWidth, Config.OutputHeight, Config.BlockMode, Config.PaddingMode);
                    testResult.StaticBinFile = binResult.StaticBinFile;
                    testResult.DynamicBinFile = binResult.DynamicBinFile;
                    testResult.MergedBinFile = binResult.MergedBinFile;
                    testResult.BinGenerated = binResult.Success;
                    if (!binResult.Success)
                        AppendMessage(testResult, binResult.Message);
                }
                catch (Exception ex)
                {
                    testResult.BinGenerated = false;
                    AppendMessage(testResult, $"Demura bin生成失败：{ex.Message}");
                }
            }

            if (Config.LaunchDemuraTool && File.Exists(exeFile))
            {
                System.Diagnostics.Process.Start(new ProcessStartInfo(exeFile) { WorkingDirectory = workDirectory, UseShellExecute = true });
                testResult.ToolLaunched = true;
            }

            testResult.MergedBinExists = File.Exists(mergedBin);
        }

        private string BuildDemuraConfig()
        {
            return $"""
# Demura Tool Configuration
# Generated by ProjectARVRPro

[Size]
input_width = {Config.InputWidth}
input_height = {Config.InputHeight}
output_width = {Config.OutputWidth}
output_height = {Config.OutputHeight}

[Demura]
block_mode = {Config.BlockMode}
padding_mode = {Config.PaddingMode}

[Paths]
csv_dir = csv
csv_128 = {Prepared128FileName}
csv_255 = {Prepared255FileName}
static_bin = DemuraStatic.bin
dynamic_bin = DemuraDynamic.bin
merged_bin = DemuraMerged.bin
flash_address = 0x00003000
""";
        }

        private void RebuildItems(DemuraViewTestResult testResult)
        {
            testResult.Items.Clear();
            int expectedCount = Math.Max(0, Config.InputWidth) * Math.Max(0, Config.InputHeight);
            AddCsvItems(testResult.Items, "W128", testResult.W128, expectedCount);
            AddCsvItems(testResult.Items, "W255", testResult.W255, expectedCount);

            if (Config.PrepareDemuraTool)
            {
                AddBoolItem(testResult.Items, "DemuraToolPrepared", testResult.ToolPrepared, testResult.WorkDirectory);
                AddBoolItem(testResult.Items, "G128Prepared", File.Exists(testResult.W128.PreparedFile), testResult.W128.PreparedFile);
                AddBoolItem(testResult.Items, "G255Prepared", File.Exists(testResult.W255.PreparedFile), testResult.W255.PreparedFile);
            }

            if (Config.PrepareDemuraTool && Config.GenerateBinWithDll)
            {
                AddBoolItem(testResult.Items, "DemuraBinGenerated", testResult.BinGenerated, testResult.MergedBinFile);
                AddBoolItem(testResult.Items, "DemuraStaticBin", File.Exists(testResult.StaticBinFile), testResult.StaticBinFile);
                AddBoolItem(testResult.Items, "DemuraDynamicBin", File.Exists(testResult.DynamicBinFile), testResult.DynamicBinFile);
                AddBoolItem(testResult.Items, "DemuraMergedBin", testResult.MergedBinExists, testResult.MergedBinFile);
            }
            else if (Config.RequireMergedBin)
            {
                AddBoolItem(testResult.Items, "DemuraMergedBin", testResult.MergedBinExists, testResult.MergedBinFile);
            }
        }

        private void AddCsvItems(ObservableCollection<ObjectiveTestItem> items, string prefix, DemuraCsvFileResult csv, int expectedCount)
        {
            AddBoolItem(items, $"{prefix}FileExists", csv.SourceExists, csv.SourceFile);
            AddItem(items, $"{prefix}FileSize", csv.FileSize, csv.FileSize.ToString(CultureInfo.InvariantCulture), "byte");
            AddItem(items, $"{prefix}LineCount", csv.LineCount, csv.LineCount.ToString(CultureInfo.InvariantCulture), "line");

            if (Config.ValidateInputSize && expectedCount > 0)
                AddItem(items, $"{prefix}ValueCount", csv.ValueCount, csv.ValueCount.ToString(CultureInfo.InvariantCulture), "count", expectedCount, expectedCount);
            else
                AddItem(items, $"{prefix}ValueCount", csv.ValueCount, csv.ValueCount.ToString(CultureInfo.InvariantCulture), "count");

            AddItem(items, $"{prefix}Min", csv.Min, csv.Min.ToString("F4", CultureInfo.InvariantCulture));
            AddItem(items, $"{prefix}Max", csv.Max, csv.Max.ToString("F4", CultureInfo.InvariantCulture));
            AddItem(items, $"{prefix}Average", csv.Average, csv.Average.ToString("F4", CultureInfo.InvariantCulture));
        }

        private static void AddBoolItem(ObservableCollection<ObjectiveTestItem> items, string name, bool value, string testValue)
        {
            AddItem(items, name, value ? 1 : 0, string.IsNullOrWhiteSpace(testValue) ? value.ToString() : testValue, string.Empty, 1, 1);
        }

        private static void AddItem(ObservableCollection<ObjectiveTestItem> items, string name, double value, string testValue, string unit = "", double lowLimit = 0, double upLimit = 0)
        {
            items.Add(new ObjectiveTestItem
            {
                Name = name,
                Value = value,
                TestValue = testValue,
                Unit = unit,
                LowLimit = lowLimit,
                UpLimit = upLimit
            });
        }

        private static void UpdateObjectiveTestResult(ObjectiveTestResult objectiveTestResult, string outputName, ObservableCollection<ObjectiveTestItem> items)
        {
            if (!objectiveTestResult.DynamicTestResults.TryGetValue(outputName, out var dynamicItems))
            {
                dynamicItems = new ObservableCollection<ObjectiveTestItem>();
                objectiveTestResult.DynamicTestResults[outputName] = dynamicItems;
            }

            dynamicItems.Clear();
            foreach (var item in items)
                dynamicItems.Add(item);
        }

        private static void ApplyCandidate(DemuraCsvFileResult result, DemuraFileCandidate? candidate)
        {
            if (candidate == null)
            {
                result.ParseMessage = $"未找到W{result.Gray} CSV。";
                return;
            }

            result.SourceFile = candidate.FilePath;
            result.SourceField = candidate.SourceField;
            result.MasterId = candidate.MasterId;
            result.SourceExists = File.Exists(candidate.FilePath);

            if (!result.SourceExists)
            {
                result.ParseMessage = "文件不存在。";
                return;
            }

            CsvSummary summary = ReadCsvSummary(candidate.FilePath);
            result.FileSize = summary.FileSize;
            result.LineCount = summary.LineCount;
            result.ValueCount = summary.ValueCount;
            result.Min = summary.Min;
            result.Max = summary.Max;
            result.Average = summary.Average;
            result.ParseMessage = summary.Message;
        }

        private static List<DemuraFileCandidate> CollectImageConvertCandidates(List<AlgResultMasterModel> masters, int imageConvertResultType)
        {
            List<DemuraFileCandidate> candidates = new List<DemuraFileCandidate>();

            foreach (var master in masters.Where(m => (int)m.ImgFileType == imageConvertResultType).OrderByDescending(m => m.Id))
            {
                AddCandidate(candidates, master, master.ImgFile, nameof(master.ImgFile));
                AddCandidate(candidates, master, master.ResultImagFile, nameof(master.ResultImagFile));

                foreach (var image in AlgResultImageDao.Instance.GetAllByPid(master.Id))
                {
                    AddCandidate(candidates, master, image.FileName, "AlgResultImageModel.FileName");
                }
            }

            return candidates;
        }

        private static void AddCandidate(List<DemuraFileCandidate> candidates, AlgResultMasterModel master, string? rawPath, string sourceField)
        {
            if (string.IsNullOrWhiteSpace(rawPath)) return;

            string filePath = ResolveFilePath(rawPath, master);
            if (candidates.Any(c => string.Equals(c.FilePath, filePath, StringComparison.OrdinalIgnoreCase))) return;

            candidates.Add(new DemuraFileCandidate
            {
                MasterId = master.Id,
                FilePath = filePath,
                SourceField = sourceField
            });
        }

        private static string ResolveFilePath(string rawPath, AlgResultMasterModel master)
        {
            string filePath = rawPath.Trim().Trim('"');
            if (File.Exists(filePath)) return Path.GetFullPath(filePath);

            foreach (string? baseFile in new[] { master.ImgFile, master.ResultImagFile })
            {
                if (string.IsNullOrWhiteSpace(baseFile)) continue;
                string? directory = Path.GetDirectoryName(baseFile);
                if (string.IsNullOrWhiteSpace(directory)) continue;

                string combined = Path.Combine(directory, filePath);
                if (File.Exists(combined)) return Path.GetFullPath(combined);
            }

            return filePath;
        }

        private static DemuraFileCandidate? FindCandidate(List<DemuraFileCandidate> candidates, string keyword, string preparedFileName)
        {
            return candidates
                .Where(candidate => IsMatch(candidate.FilePath, keyword, preparedFileName))
                .OrderByDescending(candidate => File.Exists(candidate.FilePath))
                .ThenByDescending(candidate => candidate.MasterId)
                .FirstOrDefault();
        }

        private static bool IsMatch(string filePath, string keyword, string preparedFileName)
        {
            string fileName = Path.GetFileName(filePath);
            return Contains(fileName, keyword) || Contains(fileName, Path.GetFileNameWithoutExtension(preparedFileName));
        }

        private static bool Contains(string value, string keyword)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   !string.IsNullOrWhiteSpace(keyword) &&
                   value.Contains(keyword, StringComparison.OrdinalIgnoreCase);
        }

        private static string FindPreviewImageFile(int batchId)
        {
            var images = MeasureImgResultDao.Instance.GetAllByBatchId(batchId);
            foreach (var image in images)
            {
                if (!string.IsNullOrWhiteSpace(image.FileUrl))
                    return image.FileUrl;
            }

            return string.Empty;
        }

        private static CsvSummary ReadCsvSummary(string filePath)
        {
            CsvSummary summary = new CsvSummary { FileSize = new FileInfo(filePath).Length };
            double sum = 0;

            foreach (string line in File.ReadLines(filePath))
            {
                summary.LineCount++;
                foreach (string token in line.Split(CsvSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (!TryParseDouble(token, out double value)) continue;

                    if (summary.ValueCount == 0)
                    {
                        summary.Min = value;
                        summary.Max = value;
                    }
                    else
                    {
                        summary.Min = Math.Min(summary.Min, value);
                        summary.Max = Math.Max(summary.Max, value);
                    }

                    summary.ValueCount++;
                    sum += value;
                }
            }

            if (summary.ValueCount > 0)
                summary.Average = sum / summary.ValueCount;
            else
                summary.Message = "CSV中未解析到数值。";

            return summary;
        }

        private static bool TryParseDouble(string token, out double value)
        {
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return true;

            return double.TryParse(token, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        }

        private static string? ResolveBundledToolDirectory()
        {
            string? assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            foreach (string? baseDirectory in new[] { assemblyDirectory, AppContext.BaseDirectory })
            {
                if (string.IsNullOrWhiteSpace(baseDirectory)) continue;

                string candidate = Path.Combine(baseDirectory, "Tools", ToolFolderName);
                if (File.Exists(Path.Combine(candidate, ToolExeName))) return candidate;
            }

            return null;
        }

        private static string BuildWorkDirectory()
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(root, "ColorVision", "ProjectARVRPro", ToolFolderName);
        }

        private static string SanitizeFileName(string value)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                value = value.Replace(c.ToString(), "");

            return string.IsNullOrWhiteSpace(value) ? "Batch" : value;
        }

        private static void CopyMissingDirectoryFiles(string sourceDirectory, string targetDirectory)
        {
            Directory.CreateDirectory(targetDirectory);

            foreach (string directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(sourceDirectory, directory);
                Directory.CreateDirectory(Path.Combine(targetDirectory, relative));
            }

            foreach (string file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(sourceDirectory, file);
                string targetFile = Path.Combine(targetDirectory, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(targetFile) ?? targetDirectory);
                if (!File.Exists(targetFile))
                    File.Copy(file, targetFile, false);
            }
        }

        private static string GetPreviewFileName(DemuraTestResult result)
        {
            if (!string.IsNullOrWhiteSpace(result.PreviewImageFile)) return result.PreviewImageFile;

            foreach (string fileName in new[] { result.W255.SourceFile, result.W128.SourceFile })
            {
                if (File.Exists(fileName) && PreviewImageExtensions.Contains(Path.GetExtension(fileName)))
                    return fileName;
            }

            return string.Empty;
        }

        private static void AppendMessage(DemuraTestResult result, string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            if (string.IsNullOrWhiteSpace(result.Message))
                result.Message = message;
            else
                result.Message += Environment.NewLine + message;
        }

        private static void AppendCsvText(StringBuilder sb, string name, DemuraCsvFileResult result)
        {
            AppendFileLinkText(sb, $"{name}Source", result.SourceFile);
            AppendFileLinkText(sb, $"{name}Csv", result.PreparedFile);
            sb.AppendLine($"{name} MasterId:{result.MasterId} Field:{result.SourceField}");
            sb.AppendLine($"{name} Count:{result.ValueCount} Lines:{result.LineCount} Size:{result.FileSize} Min:{result.Min:F4} Max:{result.Max:F4} Average:{result.Average:F4}");
            if (!string.IsNullOrWhiteSpace(result.ParseMessage))
                sb.AppendLine($"{name} Message:{result.ParseMessage}");
        }

        private static void AppendBinLinkText(StringBuilder sb, string name, string filePath)
        {
            AppendFileLinkText(sb, name, filePath);
        }

        private static void AppendFileLinkText(StringBuilder sb, string name, string filePath)
        {
            string displayName = string.IsNullOrWhiteSpace(filePath) ? "-" : Path.GetFileName(filePath);
            bool exists = File.Exists(filePath) || Directory.Exists(filePath);
            if (!string.IsNullOrWhiteSpace(filePath) && Directory.Exists(filePath))
                displayName = new DirectoryInfo(filePath).Name;
            string fileLink = string.IsNullOrWhiteSpace(filePath) ? displayName : $"[[file|{filePath}|{displayName}]]";
            sb.AppendLine($"{name}:{fileLink} Exists:{exists}");
        }

        private sealed class DemuraBinGenerationResult
        {
            public bool Success { get; set; }

            public string StaticBinFile { get; set; } = string.Empty;

            public string DynamicBinFile { get; set; } = string.Empty;

            public string MergedBinFile { get; set; } = string.Empty;

            public string Message { get; set; } = string.Empty;
        }

        private static class DemuraBinGenerator
        {
            private const int StaticBufferLength = 768;
            private const int DynamicBufferLength = 166320;

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            [return: MarshalAs(UnmanagedType.I1)]
            private delegate bool DemuraStaticSaveBinDelegate(ref StaticArray array, [Out] byte[] buffer, uint bufferLength);

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            [return: MarshalAs(UnmanagedType.I1)]
            private delegate bool DemuraDynamicSaveBinDelegate([In] float[] data, uint length, ref DynamicParams param, [Out] byte[] buffer, uint bufferLength);

            public static DemuraBinGenerationResult Generate(string workDirectory, string csv128, string csv255, int inputWidth, int inputHeight, int outputWidth, int outputHeight, int blockMode, int paddingMode)
            {
                var result = new DemuraBinGenerationResult
                {
                    StaticBinFile = Path.Combine(workDirectory, "DemuraStatic.bin"),
                    DynamicBinFile = Path.Combine(workDirectory, "DemuraDynamic.bin"),
                    MergedBinFile = Path.Combine(workDirectory, "DemuraMerged.bin")
                };

                IntPtr lutHandle = IntPtr.Zero;
                IntPtr demuraHandle = IntPtr.Zero;

                try
                {
                    ValidateDimensions(inputWidth, inputHeight, outputWidth, outputHeight);

                    string lutDll = Path.Combine(workDirectory, "GenDemuraCompressionLUT.dll");
                    string demuraDll = Path.Combine(workDirectory, "DemuraSaveBin.dll");
                    if (!File.Exists(lutDll)) return Fail(result, "未找到GenDemuraCompressionLUT.dll。");
                    if (!File.Exists(demuraDll)) return Fail(result, "未找到DemuraSaveBin.dll。");

                    lutHandle = NativeLibrary.Load(lutDll);
                    demuraHandle = NativeLibrary.Load(demuraDll);

                    var staticSaveBin = Marshal.GetDelegateForFunctionPointer<DemuraStaticSaveBinDelegate>(NativeLibrary.GetExport(demuraHandle, "DemuraStaticSaveBin"));
                    var dynamicSaveBin = Marshal.GetDelegateForFunctionPointer<DemuraDynamicSaveBinDelegate>(NativeLibrary.GetExport(demuraHandle, "DemuraDynamicSaveBin"));

                    byte[] staticBuffer = GenerateStaticBin(staticSaveBin);
                    File.WriteAllBytes(result.StaticBinFile, staticBuffer);

                    float[] dynamicData = BuildDynamicData(csv128, csv255, inputWidth, inputHeight, outputWidth, outputHeight, paddingMode);
                    byte[] dynamicBuffer = GenerateDynamicBin(dynamicSaveBin, dynamicData, outputWidth, outputHeight, blockMode);
                    File.WriteAllBytes(result.DynamicBinFile, dynamicBuffer);

                    using (FileStream stream = File.Create(result.MergedBinFile))
                    {
                        stream.Write(staticBuffer, 0, staticBuffer.Length);
                        stream.Write(dynamicBuffer, 0, dynamicBuffer.Length);
                    }

                    result.Success = File.Exists(result.MergedBinFile);
                    return result;
                }
                catch (Exception ex)
                {
                    return Fail(result, ex.Message);
                }
                finally
                {
                    if (demuraHandle != IntPtr.Zero) NativeLibrary.Free(demuraHandle);
                    if (lutHandle != IntPtr.Zero) NativeLibrary.Free(lutHandle);
                }
            }

            private static DemuraBinGenerationResult Fail(DemuraBinGenerationResult result, string message)
            {
                result.Success = false;
                result.Message = message;
                return result;
            }

            private static void ValidateDimensions(int inputWidth, int inputHeight, int outputWidth, int outputHeight)
            {
                if (inputWidth <= 0 || inputHeight <= 0 || outputWidth <= 0 || outputHeight <= 0)
                    throw new InvalidOperationException("Demura输入/输出尺寸必须大于0。");

                if (outputWidth < inputWidth || outputHeight < inputHeight)
                    throw new InvalidOperationException("Demura输出尺寸不能小于输入尺寸。");
            }

            private static byte[] GenerateStaticBin(DemuraStaticSaveBinDelegate staticSaveBin)
            {
                var array = new StaticArray
                {
                    BurstDataPoint = BuildPoint(),
                    BurstDataSlope = BuildSlope(),
                    BurstDataScaler = Enumerable.Repeat((ushort)4095, 84).ToArray(),
                    BurstDataTYPE2 = BuildType2()
                };

                byte[] buffer = new byte[StaticBufferLength];
                if (!staticSaveBin(ref array, buffer, (uint)buffer.Length))
                    throw new InvalidOperationException("DemuraStaticSaveBin返回失败。");

                return buffer;
            }

            private static byte[] GenerateDynamicBin(DemuraDynamicSaveBinDelegate dynamicSaveBin, float[] dynamicData, int outputWidth, int outputHeight, int blockMode)
            {
                var parameters = new DynamicParams
                {
                    Width = (uint)outputWidth,
                    Height = (uint)outputHeight,
                    BlockMode = (uint)Math.Max(0, blockMode)
                };

                byte[] buffer = new byte[DynamicBufferLength];
                if (!dynamicSaveBin(dynamicData, (uint)dynamicData.Length, ref parameters, buffer, (uint)buffer.Length))
                    throw new InvalidOperationException("DemuraDynamicSaveBin返回失败。");

                return buffer;
            }

            private static float[] BuildDynamicData(string csv128, string csv255, int inputWidth, int inputHeight, int outputWidth, int outputHeight, int paddingMode)
            {
                float[] values128 = ReadCsvValues(csv128, inputWidth * inputHeight);
                float[] values255 = ReadCsvValues(csv255, inputWidth * inputHeight);
                float[] padded128 = PadData(values128, inputWidth, inputHeight, outputWidth, outputHeight, paddingMode);
                float[] padded255 = PadData(values255, inputWidth, inputHeight, outputWidth, outputHeight, paddingMode);
                float[] data = new float[padded128.Length + padded255.Length];
                Array.Copy(padded128, 0, data, 0, padded128.Length);
                Array.Copy(padded255, 0, data, padded128.Length, padded255.Length);
                return data;
            }

            private static float[] ReadCsvValues(string filePath, int expectedCount)
            {
                float[] values = new float[expectedCount];
                int index = 0;

                foreach (string line in File.ReadLines(filePath))
                {
                    if (index >= expectedCount) break;

                    foreach (string token in line.Split(CsvSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        if (index >= expectedCount) break;
                        if (!TryParseFloat(token, out float value)) continue;
                        values[index++] = value <= 0 ? 1.0f : value;
                    }
                }

                if (index < expectedCount)
                    throw new InvalidOperationException($"{Path.GetFileName(filePath)}数据不足，需要{expectedCount}个数值，实际解析{index}个。");

                return values;
            }

            private static bool TryParseFloat(string token, out float value)
            {
                if (float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                    return true;

                return float.TryParse(token, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
            }

            private static float[] PadData(float[] data, int sourceWidth, int sourceHeight, int targetWidth, int targetHeight, int paddingMode)
            {
                float[] output = new float[targetWidth * targetHeight];
                for (int y = 0; y < sourceHeight; y++)
                    Array.Copy(data, y * sourceWidth, output, y * targetWidth, sourceWidth);

                float meanValue = paddingMode == 3 ? Mean(data) : 0;

                for (int y = 0; y < sourceHeight; y++)
                {
                    for (int x = sourceWidth; x < targetWidth; x++)
                    {
                        output[y * targetWidth + x] = paddingMode switch
                        {
                            1 or 2 => data[y * sourceWidth + sourceWidth - 1],
                            3 => meanValue,
                            _ => 0
                        };
                    }
                }

                for (int y = sourceHeight; y < targetHeight; y++)
                {
                    for (int x = 0; x < targetWidth; x++)
                    {
                        output[y * targetWidth + x] = paddingMode switch
                        {
                            1 or 2 => output[(sourceHeight - 1) * targetWidth + x],
                            3 => meanValue,
                            _ => 0
                        };
                    }
                }

                return output;
            }

            private static float Mean(float[] data)
            {
                double sum = 0;
                foreach (float value in data)
                    sum += value;

                return (float)(sum / data.Length);
            }

            private static ushort[] BuildPoint()
            {
                ushort[] values = new ushort[84];
                ushort[] first = new ushort[] { 112, 256, 1023, 1023, 1023, 1023, 1023 };
                ushort[] rest = new ushort[] { 112, 256, 512, 1023, 1023, 1023, 1023 };
                Array.Copy(first, 0, values, 0, first.Length);
                for (int group = 1; group < 12; group++)
                    Array.Copy(rest, 0, values, group * rest.Length, rest.Length);

                return values;
            }

            private static ushort[] BuildSlope()
            {
                ushort[] values = new ushort[96];
                ushort[] first = new ushort[] { 73, 57, 0, 0, 0, 9, 64, 64 };
                ushort[] rest = new ushort[] { 73, 57, 16, 4, 0, 9, 64, 64 };
                Array.Copy(first, 0, values, 0, first.Length);
                for (int group = 1; group < 12; group++)
                    Array.Copy(rest, 0, values, group * rest.Length, rest.Length);

                return values;
            }

            private static ushort[] BuildType2()
            {
                return new ushort[]
                {
                    1024, 0, 22,
                    1024, 372, 22,
                    1024, 745, 22,
                    1024, 1117, 22,
                    1024, 1489, 22,
                    1024, 1861, 22,
                    1024, 2234, 22,
                    1024, 2606, 22,
                    1024, 2978, 22,
                    1024, 3350, 22,
                    1024, 3723, 22,
                    1024, 4095, 0
                };
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct StaticArray
            {
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 84)]
                public ushort[] BurstDataPoint;

                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 96)]
                public ushort[] BurstDataSlope;

                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 84)]
                public ushort[] BurstDataScaler;

                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 36)]
                public ushort[] BurstDataTYPE2;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct DynamicParams
            {
                public uint Width;

                public uint Height;

                public uint BlockMode;
            }
        }

        private sealed class DemuraFileCandidate
        {
            public int MasterId { get; set; }

            public string FilePath { get; set; } = string.Empty;

            public string SourceField { get; set; } = string.Empty;
        }

        private sealed class CsvSummary
        {
            public long FileSize { get; set; }

            public int LineCount { get; set; }

            public int ValueCount { get; set; }

            public double Min { get; set; }

            public double Max { get; set; }

            public double Average { get; set; }

            public string Message { get; set; } = string.Empty;
        }
    }
}
