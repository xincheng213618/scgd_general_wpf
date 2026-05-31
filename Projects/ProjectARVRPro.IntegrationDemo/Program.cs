using ProjectARVRPro;
using ProjectARVRPro.IntegrationDemo.Contracts.Socket;
using ProjectARVRPro.Process.W51;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;

namespace ProjectARVRPro.IntegrationDemo
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                if (args.Length == 0 || args.Any(arg => string.Equals(arg, "--ui", StringComparison.OrdinalIgnoreCase)))
                {
                    var application = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
                    application.Run(new MainWindow());
                    return 0;
                }

                DemoOptions options = DemoOptions.Parse(args);
                if (options.ShowHelp)
                {
                    DemoOptions.PrintHelp();
                    return 0;
                }

                if (!string.IsNullOrWhiteSpace(options.ParseFile))
                {
                    ResultParser.ParseFile(options.ParseFile, options.OutputDirectory);
                    return 0;
                }

                ArvrClient.RunAsync(options).GetAwaiter().GetResult();
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return -1;
            }
        }
    }

    internal sealed class DemoOptions
    {
        public string Host { get; private set; } = "127.0.0.1";
        public int Port { get; private set; } = 6666;
        public string SerialNumber { get; private set; } = "SN-" + DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        public string Mode { get; private set; } = "init";
        public bool AutoConfirmSwitchPg { get; private set; }
        public bool AutoConfirmAoi { get; private set; }
        public int TimeoutSeconds { get; private set; } = 300;
        public int MaxMessages { get; private set; } = 200;
        public string ParseFile { get; private set; }
        public string OutputDirectory { get; private set; } = "output";
        public bool ShowHelp { get; private set; }

        public static DemoOptions Parse(string[] args)
        {
            var options = new DemoOptions();
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                Func<string> nextValue = () =>
                {
                    if (i + 1 >= args.Length)
                        throw new ArgumentException("Missing value for " + arg);
                    return args[++i];
                };

                switch (arg)
                {
                    case "-h":
                    case "--host":
                        options.Host = nextValue();
                        break;
                    case "-p":
                    case "--port":
                        options.Port = int.Parse(nextValue(), CultureInfo.InvariantCulture);
                        break;
                    case "--sn":
                        options.SerialNumber = nextValue();
                        break;
                    case "--mode":
                        options.Mode = nextValue().Trim().ToLowerInvariant();
                        break;
                    case "--auto-confirm-switchpg":
                        options.AutoConfirmSwitchPg = true;
                        break;
                    case "--auto-confirm-aoi":
                        options.AutoConfirmAoi = true;
                        break;
                    case "--timeout-seconds":
                        options.TimeoutSeconds = int.Parse(nextValue(), CultureInfo.InvariantCulture);
                        break;
                    case "--max-messages":
                        options.MaxMessages = int.Parse(nextValue(), CultureInfo.InvariantCulture);
                        break;
                    case "--parse-file":
                        options.ParseFile = nextValue();
                        break;
                    case "-o":
                    case "--output":
                        options.OutputDirectory = nextValue();
                        break;
                    case "--help":
                    case "help":
                        options.ShowHelp = true;
                        break;
                    default:
                        throw new ArgumentException("Unknown argument: " + arg);
                }
            }

            if (options.Mode != "init" && options.Mode != "runall")
                throw new ArgumentException("--mode must be init or runall.");
            if (options.TimeoutSeconds <= 0)
                throw new ArgumentException("--timeout-seconds must be greater than 0.");
            if (options.MaxMessages <= 0)
                throw new ArgumentException("--max-messages must be greater than 0.");

            return options;
        }

        public static void PrintHelp()
        {
            Console.WriteLine("ProjectARVRPro integration demo (.NET Framework 4.8)");
            Console.WriteLine();
            Console.WriteLine("Window demo:");
            Console.WriteLine("  ProjectARVRPro.IntegrationDemo.exe");
            Console.WriteLine("  ProjectARVRPro.IntegrationDemo.exe --ui");
            Console.WriteLine();
            Console.WriteLine("Online TCP demo:");
            Console.WriteLine("  ProjectARVRPro.IntegrationDemo.exe --host 127.0.0.1 --port 6666 --sn SN001 --mode init");
            Console.WriteLine("  ProjectARVRPro.IntegrationDemo.exe --sn SN001 --mode init --auto-confirm-switchpg --auto-confirm-aoi");
            Console.WriteLine("  ProjectARVRPro.IntegrationDemo.exe --sn SN001 --mode runall");
            Console.WriteLine("  ProjectARVRPro.IntegrationDemo.exe --sn SN001 --mode init --timeout-seconds 300 --max-messages 200");
            Console.WriteLine();
            Console.WriteLine("Offline parser demo:");
            Console.WriteLine("  ProjectARVRPro.IntegrationDemo.exe --parse-file Samples\\project-arvr-result.json");
            Console.WriteLine();
            Console.WriteLine("Source run:");
            Console.WriteLine("  dotnet run --project ProjectARVRPro.IntegrationDemo.csproj -- --parse-file Samples\\project-arvr-result.json");
        }
    }

    internal static class ArvrClient
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        public static async Task RunAsync(DemoOptions options)
        {
            Directory.CreateDirectory(options.OutputDirectory);

            using (var tcpClient = new TcpClient())
            {
                Console.WriteLine("Connecting {0}:{1} ...", options.Host, options.Port);
                await tcpClient.ConnectAsync(options.Host, options.Port);
                Console.WriteLine("Connected.");

                using (NetworkStream stream = tcpClient.GetStream())
                {
                    string firstEvent = options.Mode == "runall" ? "RunAll" : "ProjectARVRInit";
                    await SendRequestAsync(stream, firstEvent, options.SerialNumber);

                    var reader = new JsonStreamMessageReader(stream);
                    var confirmedMessages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    for (int messageIndex = 0; messageIndex < options.MaxMessages; messageIndex++)
                    {
                        string json;
                        try
                        {
                            json = await reader.ReadMessageAsync(TimeSpan.FromSeconds(options.TimeoutSeconds));
                        }
                        catch (TimeoutException ex)
                        {
                            Console.WriteLine(ex.Message);
                            return;
                        }

                        if (json == null)
                        {
                            Console.WriteLine("Connection closed by server.");
                            return;
                        }

                        Console.WriteLine();
                        Console.WriteLine("Received:");
                        Console.WriteLine(json);

                        Dictionary<string, object> root = ResultParser.DeserializeDictionary(json);
                        string eventName = ResultParser.GetString(root, "EventName");
                        string serialNumber = ResultParser.GetString(root, "SerialNumber");

                        if (eventName == "SwitchPG")
                        {
                            Dictionary<string, object> data = ResultParser.GetDictionary(root, "Data");
                            object testType = data != null && data.ContainsKey("ARVRTestType") ? data["ARVRTestType"] : null;
                            Console.WriteLine("SwitchPG requested. ARVRTestType={0}.", testType ?? "<null>");
                            if (options.AutoConfirmSwitchPg || AskYesNo("Send SwitchPGCompleted after the display has switched?"))
                                await SendConfirmOnceAsync(stream, confirmedMessages, root, "SwitchPG", "SwitchPGCompleted", string.IsNullOrWhiteSpace(serialNumber) ? options.SerialNumber : serialNumber);
                        }
                        else if (eventName == "AoiSwitchPG")
                        {
                            Console.WriteLine("AOI switch image requested.");
                            if (options.AutoConfirmAoi || AskYesNo("Send AOITestSwitchImageComplete after AOI image switch?"))
                                await SendConfirmOnceAsync(stream, confirmedMessages, root, "AoiSwitchPG", "AOITestSwitchImageComplete", string.IsNullOrWhiteSpace(serialNumber) ? options.SerialNumber : serialNumber);
                        }
                        else if (eventName == "ProjectARVRResult")
                        {
                            ResultParser.ParseJson(json, options.OutputDirectory);
                            Console.WriteLine("Final result received. Demo completed.");
                            return;
                        }
                        else
                        {
                            Console.WriteLine("EventName={0}, Code={1}, Msg={2}", eventName, ResultParser.GetString(root, "Code"), ResultParser.GetString(root, "Msg"));
                        }
                    }

                    Console.WriteLine("Stopped after {0} messages without receiving ProjectARVRResult.", options.MaxMessages);
                }
            }
        }

        public static async Task<string> SendConfirmOnceAsync(NetworkStream stream, HashSet<string> confirmedMessages, Dictionary<string, object> root, string sourceEventName, string replyEventName, string serialNumber)
        {
            string key = BuildMessageKey(root, sourceEventName);
            if (!confirmedMessages.Add(key))
            {
                Console.WriteLine("Skipped duplicate {0} confirmation. Key={1}", sourceEventName, key);
                return string.Empty;
            }

            return await SendRequestAsync(stream, replyEventName, serialNumber);
        }

        public static async Task<string> SendRequestAsync(NetworkStream stream, string eventName, string serialNumber)
        {
            var request = new ProjectArvrSocketRequest
            {
                Version = "1.0",
                MsgID = Guid.NewGuid().ToString("N"),
                EventName = eventName,
                SerialNumber = serialNumber,
                Params = string.Empty
            };

            string json = Serializer.Serialize(request);
            byte[] payload = Encoding.UTF8.GetBytes(json);
            await stream.WriteAsync(payload, 0, payload.Length);
            await stream.FlushAsync();
            Console.WriteLine("Sent {0}: {1}", eventName, json);
            return json;
        }

        public static string BuildMessageKey(Dictionary<string, object> root, string eventName)
        {
            string msgId = ResultParser.GetString(root, "MsgID");
            string serialNumber = ResultParser.GetString(root, "SerialNumber");
            Dictionary<string, object> data = ResultParser.GetDictionary(root, "Data");
            string testType = data == null ? string.Empty : ResultParser.GetString(data, "ARVRTestType");

            if (!string.IsNullOrWhiteSpace(msgId))
                return eventName + "|" + msgId;

            return eventName + "|" + serialNumber + "|" + testType;
        }

        private static bool AskYesNo(string prompt)
        {
            Console.Write(prompt + " [y/N] ");
            string input = Console.ReadLine();
            return string.Equals(input, "y", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(input, "yes", StringComparison.OrdinalIgnoreCase);
        }
    }

    internal sealed class JsonStreamMessageReader
    {
        private readonly Stream _stream;
        private readonly byte[] _buffer = new byte[8192];
        private readonly StringBuilder _textBuffer = new StringBuilder();

        public JsonStreamMessageReader(Stream stream)
        {
            _stream = stream;
        }

        public async Task<string> ReadMessageAsync()
        {
            while (true)
            {
                string json;
                if (TryExtractJsonObject(_textBuffer, out json))
                    return json;

                int bytesRead = await _stream.ReadAsync(_buffer, 0, _buffer.Length);
                if (bytesRead == 0)
                    return _textBuffer.Length == 0 ? null : _textBuffer.ToString();

                _textBuffer.Append(Encoding.UTF8.GetString(_buffer, 0, bytesRead));
            }
        }

        private static bool TryExtractJsonObject(StringBuilder source, out string json)
        {
            json = null;
            int start = -1;
            int depth = 0;
            bool inString = false;
            bool escaped = false;

            for (int i = 0; i < source.Length; i++)
            {
                char current = source[i];
                if (start < 0)
                {
                    if (char.IsWhiteSpace(current))
                        continue;

                    if (current == '{')
                    {
                        start = i;
                        depth = 1;
                        continue;
                    }

                    throw new InvalidDataException("Unexpected character before JSON object: " + current);
                }

                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }

                    if (current == '\\')
                    {
                        escaped = true;
                        continue;
                    }

                    if (current == '"')
                        inString = false;

                    continue;
                }

                if (current == '"')
                {
                    inString = true;
                    continue;
                }

                if (current == '{')
                {
                    depth++;
                    continue;
                }

                if (current == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        int length = i - start + 1;
                        json = source.ToString(start, length);
                        source.Remove(0, i + 1);
                        return true;
                    }
                }
            }

            if (start > 0)
                source.Remove(0, start);

            return false;
        }

        public async Task<string> ReadMessageAsync(TimeSpan timeout)
        {
            if (timeout <= TimeSpan.Zero)
                return await ReadMessageAsync();

            Task<string> readTask = ReadMessageAsync();
            Task completedTask = await Task.WhenAny(readTask, Task.Delay(timeout));
            if (completedTask == readTask)
                return await readTask;

            _ = readTask.ContinueWith(task => { var ignored = task.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            try
            {
                _stream.Dispose();
            }
            catch
            {
            }

            throw new TimeoutException("No complete JSON message received in " + timeout.TotalSeconds.ToString("0", CultureInfo.InvariantCulture) + " seconds.");
        }
    }

    internal static class ResultParser
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 200 };

        public static ParsedProjectArvrResult ParseFile(string filePath, string outputDirectory)
        {
            string json = File.ReadAllText(filePath, Encoding.UTF8);
            return ParseJson(json, outputDirectory);
        }

        public static ParsedProjectArvrResult ParseJson(string json, string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);
            ParsedProjectArvrResult parsed = Parse(json);

            string timePart = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            string baseName = string.IsNullOrWhiteSpace(parsed.SerialNumber)
                ? "ProjectARVRResult_" + timePart
                : "ProjectARVRResult_" + SanitizeFileName(parsed.SerialNumber) + "_" + timePart;

            string jsonOutput = Path.Combine(outputDirectory, baseName + ".json");
            File.WriteAllText(jsonOutput, json, Encoding.UTF8);

            string csvOutput = Path.Combine(outputDirectory, baseName + "_items.csv");
            WriteCsv(csvOutput, parsed.Items);
            parsed.SavedJsonPath = jsonOutput;
            parsed.SavedCsvPath = csvOutput;

            Console.WriteLine();
            Console.WriteLine("TotalResult={0}, TotalResultString={1}", parsed.TotalResult.HasValue ? parsed.TotalResult.ToString() : "<null>", parsed.TotalResultString);
            Console.WriteLine("Flattened item count: {0}", parsed.Items.Count);
            Console.WriteLine("Saved JSON: {0}", jsonOutput);
            Console.WriteLine("Saved CSV : {0}", csvOutput);
            return parsed;
        }

        public static ParsedProjectArvrResult Parse(string json)
        {
            Dictionary<string, object> root = DeserializeDictionary(json);
            Dictionary<string, object> data = GetDictionary(root, "Data") ?? root;
            ObjectiveTestResult contractData = DeserializeContractData(data);

            return new ParsedProjectArvrResult
            {
                RawJson = json,
                EventName = GetString(root, "EventName"),
                SerialNumber = GetString(root, "SerialNumber"),
                Code = GetNullableInt(root, "Code"),
                Msg = GetString(root, "Msg"),
                Data = contractData,
                TotalResult = GetNullableBool(data, "TotalResult"),
                TotalResultString = GetString(data, "TotalResultString"),
                Items = FlattenResultItems(data).ToList()
            };
        }

        public static Dictionary<string, object> DeserializeDictionary(string json)
        {
            var root = Serializer.DeserializeObject(json) as Dictionary<string, object>;
            if (root == null)
                throw new InvalidDataException("JSON root must be an object.");
            return root;
        }

        public static Dictionary<string, object> GetDictionary(Dictionary<string, object> source, string propertyName)
        {
            object value;
            if (source == null || !source.TryGetValue(propertyName, out value))
                return null;
            return value as Dictionary<string, object>;
        }

        public static string GetString(Dictionary<string, object> source, string propertyName)
        {
            object value;
            if (source == null || !source.TryGetValue(propertyName, out value) || value == null)
                return string.Empty;
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        public static void WriteCsv(string filePath, IEnumerable<ResultItem> items)
        {
            var lines = new List<string> { "Screen,Item,Description,Value,TestValue,Unit,LowLimit,UpLimit,TestResult,Path" };
            foreach (ResultItem item in items)
            {
                lines.Add(string.Join(",", new[]
                {
                    Csv(item.Screen),
                    Csv(item.Item),
                    Csv(item.Description),
                    Csv(item.Value),
                    Csv(item.TestValue),
                    Csv(item.Unit),
                    Csv(item.LowLimit),
                    Csv(item.UpLimit),
                    Csv(item.TestResult),
                    Csv(item.Path)
                }));
            }

            File.WriteAllLines(filePath, lines, Encoding.UTF8);
        }

        private static ObjectiveTestResult DeserializeContractData(Dictionary<string, object> data)
        {
            try
            {
                string json = Serializer.Serialize(data);
                return Serializer.Deserialize<ObjectiveTestResult>(json);
            }
            catch
            {
                return null;
            }
        }

        private static IEnumerable<ResultItem> FlattenResultItems(Dictionary<string, object> data)
        {
            foreach (KeyValuePair<string, object> property in data)
            {
                if (property.Key == "DynamicTestResults")
                {
                    foreach (ResultItem item in FlattenDynamicResults(property.Value))
                        yield return item;
                    continue;
                }

                foreach (ResultItem item in FlattenElement(property.Key, property.Key, property.Value))
                    yield return item;
            }
        }

        private static IEnumerable<ResultItem> FlattenDynamicResults(object dynamicResults)
        {
            var dict = dynamicResults as Dictionary<string, object>;
            if (dict == null)
                yield break;

            foreach (KeyValuePair<string, object> screen in dict)
            {
                var list = screen.Value as object[];
                if (list == null)
                    continue;

                int index = 0;
                foreach (object itemObject in list)
                {
                    var itemDict = itemObject as Dictionary<string, object>;
                    ResultItem item;
                    if (TryReadObjectiveTestItem(screen.Key, "Item" + (index + 1), itemDict, out item))
                        yield return item;
                    index++;
                }
            }
        }

        private static IEnumerable<ResultItem> FlattenElement(string screenName, string path, object value)
        {
            var dict = value as Dictionary<string, object>;
            if (dict != null)
            {
                ResultItem item;
                if (TryReadObjectiveTestItem(screenName, LastPathSegment(path), dict, out item))
                {
                    item.Path = path;
                    yield return item;
                    yield break;
                }

                foreach (ResultItem poiItem in TryReadPoixyuvData(screenName, LastPathSegment(path), path, dict))
                    yield return poiItem;

                foreach (KeyValuePair<string, object> child in dict)
                {
                    if (child.Key == "TotalResult" || child.Key == "TotalResultString")
                        continue;

                    foreach (ResultItem childItem in FlattenElement(screenName, path + "." + child.Key, child.Value))
                        yield return childItem;
                }
            }
            else
            {
                var list = value as object[];
                if (list == null)
                    yield break;

                for (int i = 0; i < list.Length; i++)
                {
                    foreach (ResultItem childItem in FlattenElement(screenName, path + "[" + i + "]", list[i]))
                        yield return childItem;
                }
            }
        }

        private static bool TryReadObjectiveTestItem(string screenName, string fallbackItemName, Dictionary<string, object> element, out ResultItem item)
        {
            item = null;
            if (element == null)
                return false;

            bool looksLikeItem = element.ContainsKey("Value") &&
                                 (element.ContainsKey("LowLimit") || element.ContainsKey("UpLimit") || element.ContainsKey("TestResult"));
            if (!looksLikeItem)
                return false;

            item = new ResultItem
            {
                Screen = screenName,
                Item = GetString(element, "Name", fallbackItemName),
                Description = OpticalParameterDescriptions.GetDescription(screenName, fallbackItemName, GetString(element, "Name", fallbackItemName)),
                Value = GetString(element, "Value"),
                TestValue = GetString(element, "TestValue"),
                Unit = GetString(element, "Unit"),
                LowLimit = GetString(element, "LowLimit"),
                UpLimit = GetString(element, "UpLimit"),
                TestResult = GetString(element, "TestResult"),
                Path = fallbackItemName
            };
            return true;
        }

        private static IEnumerable<ResultItem> TryReadPoixyuvData(string screenName, string fallbackName, string path, Dictionary<string, object> element)
        {
            if (element == null || !element.ContainsKey("Y") || !element.ContainsKey("x") || !element.ContainsKey("y") || !element.ContainsKey("u") || !element.ContainsKey("v"))
                yield break;

            string name = GetString(element, "Name", fallbackName);
            yield return CreatePoiItem(screenName, name + "(Lv)", GetString(element, "Y"), "cd/m2", path + ".Y");
            yield return CreatePoiItem(screenName, name + "(Cx)", GetString(element, "x"), string.Empty, path + ".x");
            yield return CreatePoiItem(screenName, name + "(Cy)", GetString(element, "y"), string.Empty, path + ".y");
            yield return CreatePoiItem(screenName, name + "(u')", GetString(element, "u"), string.Empty, path + ".u");
            yield return CreatePoiItem(screenName, name + "(v')", GetString(element, "v"), string.Empty, path + ".v");

            if (element.ContainsKey("CCT"))
                yield return CreatePoiItem(screenName, name + "(CCT)", GetString(element, "CCT"), "K", path + ".CCT");
            if (element.ContainsKey("Wave"))
                yield return CreatePoiItem(screenName, name + "(Wave)", GetString(element, "Wave"), string.Empty, path + ".Wave");
        }

        private static ResultItem CreatePoiItem(string screenName, string itemName, string value, string unit, string path)
        {
            return new ResultItem
            {
                Screen = screenName,
                Item = itemName,
                Description = OpticalParameterDescriptions.GetDescription(screenName, path, itemName),
                Value = value,
                Unit = unit,
                Path = path
            };
        }

        private static string GetString(Dictionary<string, object> source, string propertyName, string defaultValue)
        {
            string value = GetString(source, propertyName);
            return string.IsNullOrEmpty(value) ? defaultValue : value;
        }

        private static int? GetNullableInt(Dictionary<string, object> source, string propertyName)
        {
            string value = GetString(source, propertyName);
            int number;
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out number) ? number : (int?)null;
        }

        private static bool? GetNullableBool(Dictionary<string, object> source, string propertyName)
        {
            string value = GetString(source, propertyName);
            bool result;
            return bool.TryParse(value, out result) ? result : (bool?)null;
        }

        private static string Csv(string value)
        {
            value = value ?? string.Empty;
            return value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r")
                ? "\"" + value.Replace("\"", "\"\"") + "\""
                : value;
        }

        private static string LastPathSegment(string path)
        {
            int dotIndex = path.LastIndexOf('.');
            return dotIndex >= 0 ? path.Substring(dotIndex + 1) : path;
        }

        private static string SanitizeFileName(string value)
        {
            char[] invalidChars = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(value.Length);
            foreach (char c in value)
                builder.Append(invalidChars.Contains(c) ? '_' : c);
            return builder.ToString();
        }
    }

    internal sealed class ParsedProjectArvrResult
    {
        public string RawJson { get; set; } = string.Empty;
        public string EventName { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public int? Code { get; set; }
        public string Msg { get; set; } = string.Empty;
        public ObjectiveTestResult Data { get; set; }
        public W51TestResult W51TestResult { get { return Data == null ? null : Data.W51TestResult; } }
        public bool? TotalResult { get; set; }
        public string TotalResultString { get; set; } = string.Empty;
        public string SavedJsonPath { get; set; } = string.Empty;
        public string SavedCsvPath { get; set; } = string.Empty;
        public List<ResultItem> Items { get; set; } = new List<ResultItem>();
    }

    internal sealed class ResultItem
    {
        public string Screen { get; set; } = string.Empty;
        public string Item { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string TestValue { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string LowLimit { get; set; } = string.Empty;
        public string UpLimit { get; set; } = string.Empty;
        public string TestResult { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
    }

    internal static class OpticalParameterDescriptions
    {
        public static string GetDescription(string screenName, string propertyName, string itemName)
        {
            string key = Normalize(propertyName);
            string itemKey = Normalize(itemName);

            if (Contains(key, itemKey, "HorizontalFieldOfViewAngle", "Horizontal_Field_Of_View_Angle"))
                return "水平视场角，表示画面水平方向可观察范围，单位 degree。";
            if (Contains(key, itemKey, "VerticalFieldOfViewAngle", "Vertical_Field of_View_Angle"))
                return "垂直视场角，表示画面垂直方向可观察范围，单位 degree。";
            if (Contains(key, itemKey, "DiagonalFieldOfViewAngle", "Diagonal_Field_of_View_Angle"))
                return "对角线视场角，表示画面对角方向可观察范围，单位 degree。";
            if (Contains(key, itemKey, "LuminanceUniformity", "Luminance_Uniformity"))
                return "亮度均匀性，通常按最小亮度/最大亮度*100% 计算，数值越高表示亮度越均匀。";
            if (Contains(key, itemKey, "ColorUniformity", "Color_Uniformity"))
                return "色度均匀性，通常取各测点与中心或参考点的最大 Delta u'v'，数值越小表示颜色越均匀。";
            if (Contains(key, itemKey, "CenterCorrelatedColorTemperature", "Correlated_Color_Temperature"))
                return "中心相关色温 CCT，表示中心点白光色温，单位 K。";
            if (Contains(key, itemKey, "CenterLunimance", "CenterLuminance", "Center_Lunimance", "CenterLuminace"))
                return "中心点亮度，表示中心测点亮度，单位 cd/m^2。";
            if (Contains(key, itemKey, "CenterCIE1931ChromaticCoordinatesx", "Coordinates_x"))
                return "中心点 CIE 1931 色品坐标 x，无单位。";
            if (Contains(key, itemKey, "CenterCIE1931ChromaticCoordinatesy", "Coordinates_y"))
                return "中心点 CIE 1931 色品坐标 y，无单位。";
            if (Contains(key, itemKey, "CenterCIE1976ChromaticCoordinatesu", "Coordinates_u"))
                return "中心点 CIE 1976 色品坐标 u'，无单位。";
            if (Contains(key, itemKey, "CenterCIE1976ChromaticCoordinatesv", "Coordinates_v"))
                return "中心点 CIE 1976 色品坐标 v'，无单位。";
            if (Contains(key, itemKey, "FOFOContrast"))
                return "FOFO 对比度，白场亮度与黑场亮度的对比关系，单位 %。";
            if (Contains(key, itemKey, "ChessboardContrast"))
                return "棋盘格对比度，基于棋盘格亮暗区域计算的对比度指标。";
            if (Contains(key, itemKey, "HorizontalTVDistortion"))
                return "水平 TV 畸变，表示水平方向几何畸变比例，单位 %。";
            if (Contains(key, itemKey, "VerticalTVDistortion"))
                return "垂直 TV 畸变，表示垂直方向几何畸变比例，单位 %。";
            if (Contains(key, itemKey, "Optic_Distortion", "OpticDistortion"))
                return "光学畸变，表示镜头或系统引起的整体几何畸变，单位 %。";
            if (Contains(key, itemKey, "DistortionTop"))
                return "九点法上边畸变，表示上侧边缘位置的局部畸变，单位 %。";
            if (Contains(key, itemKey, "DistortionBottom"))
                return "九点法下边畸变，表示下侧边缘位置的局部畸变，单位 %。";
            if (Contains(key, itemKey, "DistortionLeft"))
                return "九点法左边畸变，表示左侧边缘位置的局部畸变，单位 %。";
            if (Contains(key, itemKey, "DistortionRight"))
                return "九点法右边畸变，表示右侧边缘位置的局部畸变，单位 %。";
            if (Contains(key, itemKey, "KeystoneHoriz"))
                return "水平梯形畸变，表示画面左右宽度不一致导致的梯形误差，单位 %。";
            if (Contains(key, itemKey, "KeystoneVert"))
                return "垂直梯形畸变，表示画面上下高度不一致导致的梯形误差，单位 %。";
            if (Contains(key, itemKey, "ImageCenterXTilt"))
                return "图像中心 X 方向偏移或倾斜角，单位 degree。";
            if (Contains(key, itemKey, "ImageCenterYTilt"))
                return "图像中心 Y 方向偏移或倾斜角，单位 degree。";
            if (Contains(key, itemKey, "ImageCenterRotation"))
                return "图像中心旋转角，单位 degree。";
            if (Contains(key, itemKey, "OptCenterRotation"))
                return "光学中心旋转角，单位 degree。";
            if (Contains(key, itemKey, "OptCenterXTilt"))
                return "光学中心 X 方向偏移或倾斜角，单位 degree。";
            if (Contains(key, itemKey, "OptCenterYTilt"))
                return "光学中心 Y 方向偏移或倾斜角，单位 degree。";
            if (itemKey.IndexOf("MTF", StringComparison.OrdinalIgnoreCase) >= 0 || itemKey.StartsWith("P_", StringComparison.OrdinalIgnoreCase))
                return "MTF 调制传递函数，表示成像系统在指定位置和方向的清晰度/解析力，H 为水平线对方向，V 为垂直线对方向，0F/0.3F/0.6F/0.8F 表示不同视场位置。";
            if (itemKey.EndsWith("(Lv)", StringComparison.OrdinalIgnoreCase) || key.EndsWith(".Y", StringComparison.OrdinalIgnoreCase))
                return "测点亮度 Lv/Y，单位 cd/m^2。";
            if (itemKey.EndsWith("(Cx)", StringComparison.OrdinalIgnoreCase) || key.EndsWith(".x", StringComparison.OrdinalIgnoreCase))
                return "测点 CIE 1931 色品坐标 x。";
            if (itemKey.EndsWith("(Cy)", StringComparison.OrdinalIgnoreCase) || key.EndsWith(".y", StringComparison.OrdinalIgnoreCase))
                return "测点 CIE 1931 色品坐标 y。";
            if (itemKey.EndsWith("(u')", StringComparison.OrdinalIgnoreCase) || key.EndsWith(".u", StringComparison.OrdinalIgnoreCase))
                return "测点 CIE 1976 色品坐标 u'。";
            if (itemKey.EndsWith("(v')", StringComparison.OrdinalIgnoreCase) || key.EndsWith(".v", StringComparison.OrdinalIgnoreCase))
                return "测点 CIE 1976 色品坐标 v'。";
            if (itemKey.EndsWith("(CCT)", StringComparison.OrdinalIgnoreCase) || key.EndsWith(".CCT", StringComparison.OrdinalIgnoreCase))
                return "测点相关色温 CCT，单位 K。";
            if (itemKey.EndsWith("(Wave)", StringComparison.OrdinalIgnoreCase) || key.EndsWith(".Wave", StringComparison.OrdinalIgnoreCase))
                return "测点主波长或波长相关结果。";

            return "ARVRPro 输出的客观测试项。具体含义以项目测试配置和字段名为准。";
        }

        public static string BuildGuideText()
        {
            return "常用光学参数说明\r\n" +
                   "\r\n" +
                   "FOV / Field Of View: 视场角，Horizontal/Vertical/Diagonal 分别表示水平、垂直、对角方向，单位 degree。\r\n" +
                   "LuminanceUniformity: 亮度均匀性，通常为最小亮度/最大亮度*100%，越高越均匀。\r\n" +
                   "ColorUniformity: 色度均匀性，通常为最大 Delta u'v'，越小越均匀。\r\n" +
                   "CenterLunimance: 中心点亮度，单位 cd/m^2。字段名保留 ARVRPro 当前拼写。\r\n" +
                   "CIE1931 x/y: CIE 1931 色品坐标，无单位。\r\n" +
                   "CIE1976 u'/v': CIE 1976 UCS 色品坐标，无单位。\r\n" +
                   "CCT: 相关色温，单位 K。\r\n" +
                   "FOFOContrast: 白场/黑场对比关系，单位 %。\r\n" +
                   "ChessboardContrast: 棋盘格亮暗区域对比度。\r\n" +
                   "TV/Optic/Keystone Distortion: 几何畸变、光学畸变和梯形畸变，单位通常为 %。\r\n" +
                   "OpticCenter/ImageCenter: 光学中心或图像中心的偏移、倾斜、旋转，单位通常为 degree。\r\n" +
                   "MTF: 调制传递函数，用于描述清晰度/解析力；H/V 为方向，0F/0.3F/0.6F/0.8F 为不同视场位置。\r\n" +
                   "\r\n" +
                   "ObjectiveTestItem 字段说明\r\n" +
                   "Name: ARVRPro 输出的测试项显示名。\r\n" +
                   "TestValue: 给人查看的测试值字符串，可能包含格式化或符号。\r\n" +
                   "Value: 数值型测试值，推荐用于 MES/上位机判定和存档。\r\n" +
                   "LowLimit/UpLimit: 下限/上限；为 0 时通常表示该侧不参与判定。\r\n" +
                   "Unit: 单位。\r\n" +
                   "TestResult: 单项判定结果，true 表示通过。";
        }

        private static bool Contains(string key, string itemKey, params string[] tokens)
        {
            foreach (string token in tokens)
            {
                if (key.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    itemKey.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static string Normalize(string value)
        {
            return value ?? string.Empty;
        }
    }
}
