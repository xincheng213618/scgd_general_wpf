#pragma warning disable CS8603,CS8625
using ColorVision.Database;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace ColorVision.Engine.Templates.Flow
{
    /// <summary>
    /// 流程包数据模型，用于导出/导入流程及其关联的模板
    /// </summary>
    public class FlowPackageManifest
    {
        public string FlowName { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0";
        public List<FlowPackageTemplate> Templates { get; set; } = new List<FlowPackageTemplate>();
    }

    /// <summary>
    /// 流程包中的模板数据
    /// </summary>
    public class FlowPackageTemplate
    {
        public string TemplateName { get; set; } = string.Empty;
        public string TemplateCode { get; set; } = string.Empty;
        public int TemplateDicId { get; set; }
        public string? SerializedContent { get; set; }
        public List<FlowPackageDetailItem> Details { get; set; } = new List<FlowPackageDetailItem>();
    }

    /// <summary>
    /// 模板详情项 (对应 ModDetailModel 的可序列化版本)
    /// </summary>
    public class FlowPackageDetailItem
    {
        public int SysPid { get; set; }
        public string? ValueA { get; set; }
        public string? ValueB { get; set; }
        public bool IsEnable { get; set; } = true;
        public bool IsDelete { get; set; }
    }

    /// <summary>
    /// STN 数据解析和修改工具
    /// </summary>
    public static class FlowPackageHelper
    {
        /// <summary>
        /// 已知的模板属性名称集合 (STNodeProperty 标记的模板引用属性)
        /// </summary>
        private static readonly HashSet<string> TemplatePropertyNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "TempName",
            "TemplateName",
            "CalibTempName",
            "CaliTempName",
            "POITempName",
            "POIFilterTempName",
            "POIReviseTempName",
            "FilterTemplateName",
            "ReviseTemplateName",
            "OutputTemplateName",
            "RePOITemplateName",
            "SavePOITempName",
            "XRTempName",
            "CamTempName",
            "ExpTempName",
            "AlgTempName",
            "AutoFocusTemp",
            "ModelName",
            "LayoutROITemplate",
        };

        /// <summary>
        /// 从 STN 数据中提取所有被节点引用的模板名称
        /// </summary>
        public static HashSet<string> ExtractTemplateNames(byte[] stnData)
        {
            var templateNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (stnData == null || stnData.Length < 5)
                return templateNames;

            byte[] decompressed = DecompressSTN(stnData);
            if (decompressed == null)
                return templateNames;

            ParseNodesForTemplateNames(decompressed, templateNames, null);
            return templateNames;
        }

        /// <summary>
        /// 替换 STN 数据中的模板名称引用，返回修改后的 STN 数据
        /// </summary>
        public static byte[] ReplaceTemplateNames(byte[] stnData, Dictionary<string, string> nameMap)
        {
            if (stnData == null || stnData.Length < 5 || nameMap == null || nameMap.Count == 0)
                return stnData;

            byte[] decompressed = DecompressSTN(stnData);
            if (decompressed == null)
                return stnData;

            byte[] modified = RebuildDecompressedData(decompressed, nameMap);
            return CompressSTN(modified);
        }

        /// <summary>
        /// 将流程数据和模板数据打包为 .cvflow ZIP 文件
        /// </summary>
        public static void ExportFlowPackage(string outputPath, string flowName, byte[] stnData, FlowPackageManifest manifest)
        {
            using var zipToOpen = new FileStream(outputPath, FileMode.Create);
            using var archive = new ZipArchive(zipToOpen, ZipArchiveMode.Create);

            // 写入 STN 文件
            var stnEntry = archive.CreateEntry("flow.stn");
            using (var stream = stnEntry.Open())
            {
                stream.Write(stnData, 0, stnData.Length);
            }

            // 写入 manifest
            var manifestEntry = archive.CreateEntry("manifest.json");
            using (var stream = manifestEntry.Open())
            using (var writer = new StreamWriter(stream, Encoding.UTF8))
            {
                var json = JsonConvert.SerializeObject(manifest, Formatting.Indented);
                writer.Write(json);
            }
        }

        /// <summary>
        /// 从 .cvflow ZIP 文件中读取流程包数据
        /// </summary>
        public static (byte[] StnData, FlowPackageManifest? Manifest) ImportFlowPackage(string inputPath)
        {
            byte[]? stnData = null;
            FlowPackageManifest? manifest = null;

            using var zipToOpen = new FileStream(inputPath, FileMode.Open, FileAccess.Read);
            using var archive = new ZipArchive(zipToOpen, ZipArchiveMode.Read);

            var stnEntry = archive.GetEntry("flow.stn");
            if (stnEntry != null)
            {
                using var stream = stnEntry.Open();
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                stnData = ms.ToArray();
            }

            var manifestEntry = archive.GetEntry("manifest.json");
            if (manifestEntry != null)
            {
                using var stream = manifestEntry.Open();
                using var reader = new StreamReader(stream, Encoding.UTF8);
                var json = reader.ReadToEnd();
                manifest = JsonConvert.DeserializeObject<FlowPackageManifest>(json);
            }

            return (stnData ?? Array.Empty<byte>(), manifest);
        }

        /// <summary>
        /// 解压 STN 数据 (跳过5字节header后GZip解压)
        /// </summary>
        private static byte[]? DecompressSTN(byte[] stnData)
        {
            if (stnData.Length < 5)
                return null;

            // 验证 header: "STND" + version 1
            if (stnData[0] != 83 || stnData[1] != 84 || stnData[2] != 78 || stnData[3] != 68)
                return null;

            try
            {
                using var ms = new MemoryStream(stnData, 5, stnData.Length - 5);
                using var gzip = new GZipStream(ms, CompressionMode.Decompress);
                using var output = new MemoryStream();
                gzip.CopyTo(output);
                return output.ToArray();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 压缩数据为 STN 格式 (加上 "STND" + version header)
        /// </summary>
        private static byte[] CompressSTN(byte[] decompressedData)
        {
            using var output = new MemoryStream();

            // 写入 header
            output.Write(new byte[] { 83, 84, 78, 68 }, 0, 4); // "STND"
            output.WriteByte(1); // version

            using (var gzip = new GZipStream(output, CompressionMode.Compress, leaveOpen: true))
            {
                gzip.Write(decompressedData, 0, decompressedData.Length);
            }

            return output.ToArray();
        }

        /// <summary>
        /// 解析解压后的数据，提取模板名称
        /// </summary>
        private static void ParseNodesForTemplateNames(byte[] data, HashSet<string> templateNames, Dictionary<string, string> nameMap)
        {
            int pos = 0;

            // 跳过 canvas metadata: offsetX(4) + offsetY(4) + scale(4) + nodeCount(4)
            if (data.Length < 16) return;
            int nodeCount = BitConverter.ToInt32(data, 12);
            pos = 16;

            for (int i = 0; i < nodeCount && pos < data.Length; i++)
            {
                if (pos + 4 > data.Length) break;
                int nodeDataLength = BitConverter.ToInt32(data, pos);
                pos += 4;

                if (pos + nodeDataLength > data.Length) break;
                byte[] nodeData = new byte[nodeDataLength];
                Array.Copy(data, pos, nodeData, 0, nodeDataLength);
                pos += nodeDataLength;

                // 解析节点数据中的属性
                var properties = ParseNodeProperties(nodeData);
                foreach (var kvp in properties)
                {
                    if (TemplatePropertyNames.Contains(kvp.Key))
                    {
                        string value = Encoding.UTF8.GetString(kvp.Value);
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            templateNames?.Add(value);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 重建解压后的数据，替换模板名称
        /// </summary>
        private static byte[] RebuildDecompressedData(byte[] data, Dictionary<string, string> nameMap)
        {
            int pos = 0;
            if (data.Length < 16) return data;

            // 读取 canvas metadata
            byte[] canvasMetadata = new byte[16];
            Array.Copy(data, 0, canvasMetadata, 0, 16);
            int nodeCount = BitConverter.ToInt32(data, 12);
            pos = 16;

            var output = new List<byte>();
            output.AddRange(canvasMetadata);

            // 处理每个节点
            for (int i = 0; i < nodeCount && pos < data.Length; i++)
            {
                if (pos + 4 > data.Length) break;
                int nodeDataLength = BitConverter.ToInt32(data, pos);
                pos += 4;

                if (pos + nodeDataLength > data.Length) break;
                byte[] nodeData = new byte[nodeDataLength];
                Array.Copy(data, pos, nodeData, 0, nodeDataLength);
                pos += nodeDataLength;

                // 重建节点数据，替换模板名称
                byte[] modifiedNodeData = RebuildNodeData(nodeData, nameMap);
                output.AddRange(BitConverter.GetBytes(modifiedNodeData.Length));
                output.AddRange(modifiedNodeData);
            }

            // 复制剩余数据 (connections)
            if (pos < data.Length)
            {
                byte[] remaining = new byte[data.Length - pos];
                Array.Copy(data, pos, remaining, 0, remaining.Length);
                output.AddRange(remaining);
            }

            return output.ToArray();
        }

        /// <summary>
        /// 解析单个节点数据，提取所有属性键值对
        /// </summary>
        private static Dictionary<string, byte[]> ParseNodeProperties(byte[] nodeData)
        {
            var properties = new Dictionary<string, byte[]>();
            int pos = 0;

            // 跳过 class info
            if (pos >= nodeData.Length) return properties;
            int classInfoLen = nodeData[pos];
            pos += 1 + classInfoLen;

            // 跳过 type GUID
            if (pos >= nodeData.Length) return properties;
            int guidLen = nodeData[pos];
            pos += 1 + guidLen;

            // 读取属性键值对
            while (pos + 8 <= nodeData.Length) // 至少需要 4+4 bytes for key and value lengths
            {
                int keyLen = BitConverter.ToInt32(nodeData, pos);
                pos += 4;
                if (pos + keyLen > nodeData.Length || keyLen < 0) break;

                string key = Encoding.UTF8.GetString(nodeData, pos, keyLen);
                pos += keyLen;

                if (pos + 4 > nodeData.Length) break;
                int valueLen = BitConverter.ToInt32(nodeData, pos);
                pos += 4;
                if (pos + valueLen > nodeData.Length || valueLen < 0) break;

                byte[] value = new byte[valueLen];
                Array.Copy(nodeData, pos, value, 0, valueLen);
                pos += valueLen;

                properties[key] = value;
            }

            return properties;
        }

        /// <summary>
        /// 重建节点数据，替换指定属性的值
        /// </summary>
        private static byte[] RebuildNodeData(byte[] nodeData, Dictionary<string, string> nameMap)
        {
            var output = new List<byte>();
            int pos = 0;

            // 复制 class info
            if (pos >= nodeData.Length) return nodeData;
            int classInfoLen = nodeData[pos];
            output.Add(nodeData[pos]);
            pos++;
            for (int j = 0; j < classInfoLen && pos < nodeData.Length; j++)
            {
                output.Add(nodeData[pos]);
                pos++;
            }

            // 复制 type GUID
            if (pos >= nodeData.Length) return output.ToArray();
            int guidLen = nodeData[pos];
            output.Add(nodeData[pos]);
            pos++;
            for (int j = 0; j < guidLen && pos < nodeData.Length; j++)
            {
                output.Add(nodeData[pos]);
                pos++;
            }

            // 处理属性键值对
            while (pos + 8 <= nodeData.Length)
            {
                int keyLen = BitConverter.ToInt32(nodeData, pos);
                pos += 4;
                if (pos + keyLen > nodeData.Length || keyLen < 0) break;

                string key = Encoding.UTF8.GetString(nodeData, pos, keyLen);
                byte[] keyBytes = new byte[keyLen];
                Array.Copy(nodeData, pos, keyBytes, 0, keyLen);
                pos += keyLen;

                if (pos + 4 > nodeData.Length) break;
                int valueLen = BitConverter.ToInt32(nodeData, pos);
                pos += 4;
                if (pos + valueLen > nodeData.Length || valueLen < 0) break;

                byte[] valueBytes = new byte[valueLen];
                Array.Copy(nodeData, pos, valueBytes, 0, valueLen);
                pos += valueLen;

                // 检查是否需要替换值
                if (TemplatePropertyNames.Contains(key))
                {
                    string oldValue = Encoding.UTF8.GetString(valueBytes);
                    if (nameMap.TryGetValue(oldValue, out string newValue))
                    {
                        valueBytes = Encoding.UTF8.GetBytes(newValue);
                    }
                }

                // 写入属性
                output.AddRange(BitConverter.GetBytes(keyBytes.Length));
                output.AddRange(keyBytes);
                output.AddRange(BitConverter.GetBytes(valueBytes.Length));
                output.AddRange(valueBytes);
            }

            return output.ToArray();
        }

        /// <summary>
        /// 收集流程中所有引用的模板信息，用于导出
        /// </summary>
        public static FlowPackageManifest CollectTemplatesForExport(string flowName, byte[] stnData)
        {
            var manifest = new FlowPackageManifest
            {
                FlowName = flowName,
                Version = "2.0"
            };

            var knownTemplateNames = new HashSet<string>(
                TemplateControl.ITemplateNames.Values.SelectMany(item => item.GetTemplateNames()),
                StringComparer.OrdinalIgnoreCase);
            var exportedTemplateNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pendingTemplateNames = new Queue<string>(ExtractTemplateNames(stnData));

            while (pendingTemplateNames.Count > 0)
            {
                string name = pendingTemplateNames.Dequeue();
                if (!exportedTemplateNames.Add(name))
                    continue;

                if (!TryResolveTemplate(name, out string templateCode, out ITemplate iTemplate, out int index))
                    continue;

                object templateValue = iTemplate.GetParamValue(index);
                if (templateValue == null)
                    continue;

                string serializedContent = SerializeTemplateContent(templateValue);
                var pkgTemplate = new FlowPackageTemplate
                {
                    TemplateName = name,
                    TemplateCode = templateCode,
                    TemplateDicId = iTemplate.TemplateDicId,
                    SerializedContent = serializedContent,
                    Details = ExtractTemplateDetails(templateValue)
                };
                manifest.Templates.Add(pkgTemplate);

                foreach (string referencedTemplateName in ExtractTemplateNamesFromSerializedContent(serializedContent, knownTemplateNames, name))
                {
                    if (!exportedTemplateNames.Contains(referencedTemplateName))
                    {
                        pendingTemplateNames.Enqueue(referencedTemplateName);
                    }
                }
            }

            return manifest;
        }

        /// <summary>
        /// 导入模板到数据库，返回名称映射表 (旧名称 → 新名称)
        /// </summary>
        public static Dictionary<string, string> ImportTemplates(FlowPackageManifest manifest, string flowName)
        {
            var nameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (manifest?.Templates == null) return nameMap;
            var reservedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var importPlans = new List<(ITemplate Template, string NewName, FlowPackageTemplate PackageTemplate)>();

            foreach (var pkgTemplate in manifest.Templates)
            {
                string originalName = pkgTemplate.TemplateName;
                string templateCode = pkgTemplate.TemplateCode;

                // 查找对应的 ITemplate 实例
                if (!TemplateControl.ITemplateNames.TryGetValue(templateCode, out var iTemplate))
                    continue;

                // 检查是否存在同名模板
                string newName = originalName;
                if (IsReservedTemplateName(originalName, reservedNames))
                {
                    // 名称冲突，生成新名称
                    newName = GenerateUniqueName(originalName, flowName, reservedNames);
                }

                reservedNames.Add(newName);
                importPlans.Add((iTemplate, newName, pkgTemplate));

                if (!originalName.Equals(newName, StringComparison.OrdinalIgnoreCase))
                {
                    nameMap[originalName] = newName;
                }
            }

            foreach (var importPlan in importPlans)
            {
                CreateTemplateFromPackage(importPlan.Template, importPlan.NewName, importPlan.PackageTemplate, nameMap);
            }

            return nameMap;
        }

        /// <summary>
        /// 生成不冲突的模板名称
        /// </summary>
        private static string GenerateUniqueName(string baseName, string flowName, HashSet<string> reservedNames)
        {
            string candidate = $"{baseName}_{flowName}";
            if (!IsReservedTemplateName(candidate, reservedNames))
                return candidate;

            for (int i = 1; i < 9999; i++)
            {
                candidate = $"{baseName}_{flowName}_{i}";
                if (!IsReservedTemplateName(candidate, reservedNames))
                    return candidate;
            }

            return $"{baseName}_{Guid.NewGuid():N}";
        }

        /// <summary>
        /// 从包数据创建模板
        /// </summary>
        private static void CreateTemplateFromPackage(ITemplate iTemplate, string templateName, FlowPackageTemplate pkgTemplate, Dictionary<string, string> nameMap)
        {
            if (!string.IsNullOrWhiteSpace(pkgTemplate.SerializedContent))
            {
                string adjustedContent = ReplaceTemplateReferencesInJsonContent(pkgTemplate.SerializedContent, nameMap);
                if (iTemplate.ImportJsonContent(templateName, adjustedContent))
                {
                    iTemplate.Create(templateName);
                    return;
                }
            }

            // 某些模板（如 POI）不使用 ModMaster/ModDetail 架构，需走模板自身的 Create 逻辑。
            if (!CanCreateParamFromModData(iTemplate))
            {
                iTemplate.Create(templateName);
                return;
            }

            using var Db = new SqlSugar.SqlSugarClient(new SqlSugar.ConnectionConfig
            {
                ConnectionString = Database.MySqlControl.GetConnectionString(),
                DbType = SqlSugar.DbType.MySql,
                IsAutoCloseConnection = true
            });

            var modMaster = new ModMasterModel
            {
                Pid = pkgTemplate.TemplateDicId,
                Name = templateName,
                TenantId = 0
            };
            int id = Db.Insertable(modMaster).ExecuteReturnIdentity();
            modMaster.Id = id;

            // 创建 detail 记录
            var details = new List<ModDetailModel>();
            if (pkgTemplate.Details != null && pkgTemplate.Details.Count > 0)
            {
                foreach (var item in pkgTemplate.Details)
                {
                    details.Add(new ModDetailModel
                    {
                        SysPid = item.SysPid,
                        Pid = modMaster.Id,
                        ValueA = ReplaceTemplateReferencesInString(item.ValueA, nameMap),
                        ValueB = ReplaceTemplateReferencesInString(item.ValueB, nameMap),
                        IsEnable = item.IsEnable,
                        IsDelete = item.IsDelete
                    });
                }
            }
            else
            {
                // 如果没有 detail 数据，使用系统默认值
                foreach (var item in SysDictionaryModDetailDao.Instance.GetAllByPid(pkgTemplate.TemplateDicId))
                {
                    details.Add(new ModDetailModel { SysPid = item.Id, Pid = modMaster.Id, ValueA = item.DefaultValue });
                }
            }

            // 清除默认创建的detail，替换为导入的数据 (与 ITemplate<T>.Create 中的模式一致)
            Db.Deleteable<ModDetailModel>().Where(x => x.Pid == modMaster.Id).ExecuteCommand();
            Db.Insertable(details).ExecuteCommand();

            // 将新模板加入到内存中的模板集合
            var modDetailModels = Db.Queryable<ModDetailModel>().Where(x => x.Pid == modMaster.Id).ToList();
            AddTemplateToCollection(iTemplate, modMaster, modDetailModels);
        }

        private static bool TryResolveTemplate(string templateName, out string templateCode, out ITemplate template, out int index)
        {
            foreach (var kvp in TemplateControl.ITemplateNames)
            {
                int templateIndex;
                try
                {
                    templateIndex = kvp.Value.GetTemplateIndex(templateName);
                }
                catch (NotImplementedException)
                {
                    continue;
                }
                catch (NotSupportedException)
                {
                    continue;
                }

                if (templateIndex >= 0)
                {
                    templateCode = kvp.Key;
                    template = kvp.Value;
                    index = templateIndex;
                    return true;
                }
            }

            templateCode = string.Empty;
            template = null!;
            index = -1;
            return false;
        }

        private static List<FlowPackageDetailItem> ExtractTemplateDetails(object templateValue)
        {
            if (templateValue is not ParamModBase paramModBase)
                return new List<FlowPackageDetailItem>();

            var details = new List<ModDetailModel>();
            paramModBase.GetDetail(details);
            return details.Select(d => new FlowPackageDetailItem
            {
                SysPid = d.SysPid,
                ValueA = d.ValueA,
                ValueB = d.ValueB,
                IsEnable = d.IsEnable,
                IsDelete = d.IsDelete
            }).ToList();
        }

        private static string SerializeTemplateContent(object templateValue)
        {
            return JsonConvert.SerializeObject(templateValue, Formatting.Indented);
        }

        private static HashSet<string> ExtractTemplateNamesFromSerializedContent(string? serializedContent, HashSet<string> knownTemplateNames, string currentTemplateName)
        {
            var referencedTemplateNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectTemplateNamesFromString(serializedContent, knownTemplateNames, referencedTemplateNames, currentTemplateName);
            return referencedTemplateNames;
        }

        private static void CollectTemplateNamesFromString(string? rawValue, HashSet<string> knownTemplateNames, HashSet<string> referencedTemplateNames, string currentTemplateName)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
                return;

            string trimmedValue = rawValue.Trim();
            if (knownTemplateNames.Contains(trimmedValue) && !trimmedValue.Equals(currentTemplateName, StringComparison.OrdinalIgnoreCase))
            {
                referencedTemplateNames.Add(trimmedValue);
            }

            if (!LooksLikeJson(trimmedValue))
                return;

            try
            {
                JToken token = JToken.Parse(trimmedValue);
                CollectTemplateNamesFromToken(token, knownTemplateNames, referencedTemplateNames, currentTemplateName);
            }
            catch (JsonException)
            {
            }
        }

        private static void CollectTemplateNamesFromToken(JToken token, HashSet<string> knownTemplateNames, HashSet<string> referencedTemplateNames, string currentTemplateName)
        {
            if (token.Type == JTokenType.String)
            {
                CollectTemplateNamesFromString(token.Value<string>(), knownTemplateNames, referencedTemplateNames, currentTemplateName);
                return;
            }

            foreach (JToken child in token.Children())
            {
                CollectTemplateNamesFromToken(child, knownTemplateNames, referencedTemplateNames, currentTemplateName);
            }
        }

        private static bool IsReservedTemplateName(string templateName, HashSet<string> reservedNames)
        {
            return reservedNames.Contains(templateName) || TemplateControl.ExitsTemplateName(templateName);
        }

        private static string ReplaceTemplateReferencesInJsonContent(string jsonContent, Dictionary<string, string> nameMap)
        {
            if (string.IsNullOrWhiteSpace(jsonContent) || nameMap.Count == 0)
                return jsonContent;

            try
            {
                JToken token = JToken.Parse(jsonContent);
                if (!ReplaceTemplateReferencesInToken(token, nameMap))
                    return jsonContent;

                return token.ToString(Formatting.Indented);
            }
            catch (JsonException)
            {
                return jsonContent;
            }
        }

        private static bool ReplaceTemplateReferencesInToken(JToken token, Dictionary<string, string> nameMap)
        {
            if (token.Type == JTokenType.String)
            {
                string? currentValue = token.Value<string>();
                string? replacedValue = ReplaceTemplateReferencesInString(currentValue, nameMap);
                if (!string.Equals(currentValue, replacedValue, StringComparison.Ordinal))
                {
                    ((JValue)token).Value = replacedValue;
                    return true;
                }

                return false;
            }

            bool changed = false;
            if (token is JObject objectToken)
            {
                foreach (JProperty property in objectToken.Properties().ToList())
                {
                    if (ReplaceTemplateReferencesInToken(property.Value, nameMap))
                        changed = true;
                }

                return changed;
            }

            foreach (JToken child in token.Children())
            {
                if (ReplaceTemplateReferencesInToken(child, nameMap))
                    changed = true;
            }

            return changed;
        }

        private static string? ReplaceTemplateReferencesInString(string? rawValue, Dictionary<string, string> nameMap)
        {
            if (string.IsNullOrWhiteSpace(rawValue) || nameMap.Count == 0)
                return rawValue;

            if (nameMap.TryGetValue(rawValue, out string directReplacement))
                return directReplacement;

            string trimmedValue = rawValue.Trim();
            if (!trimmedValue.Equals(rawValue, StringComparison.Ordinal) && nameMap.TryGetValue(trimmedValue, out directReplacement))
                return directReplacement;

            if (!LooksLikeJson(trimmedValue))
                return rawValue;

            try
            {
                JToken nestedToken = JToken.Parse(trimmedValue);
                if (!ReplaceTemplateReferencesInToken(nestedToken, nameMap))
                    return rawValue;

                return nestedToken.ToString(Formatting.None);
            }
            catch (JsonException)
            {
                return rawValue;
            }
        }

        private static bool LooksLikeJson(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            char firstChar = value[0];
            char lastChar = value[^1];
            return (firstChar == '{' && lastChar == '}') || (firstChar == '[' && lastChar == ']');
        }

        private static bool CanCreateParamFromModData(ITemplate iTemplate)
        {
            var templateType = iTemplate.GetType();
            var baseType = templateType;
            while (baseType != null && (!baseType.IsGenericType || baseType.GetGenericTypeDefinition() != typeof(ITemplate<>)))
            {
                baseType = baseType.BaseType;
            }

            if (baseType == null) return false;

            var paramType = baseType.GetGenericArguments()[0];
            return paramType.GetConstructor(new[] { typeof(ModMasterModel), typeof(List<ModDetailModel>) }) != null;
        }

        /// <summary>
        /// 通过反射将新创建的模板添加到 ITemplate 的 TemplateParams 集合中
        /// </summary>
        private static void AddTemplateToCollection(ITemplate iTemplate, ModMasterModel modMaster, List<ModDetailModel> details)
        {
            // 获取 ITemplate<T> 的泛型参数类型 T
            var templateType = iTemplate.GetType();
            var baseType = templateType;
            while (baseType != null && (!baseType.IsGenericType || baseType.GetGenericTypeDefinition() != typeof(ITemplate<>)))
            {
                baseType = baseType.BaseType;
            }

            if (baseType == null) return;

            var paramType = baseType.GetGenericArguments()[0]; // typeof(T)

            // 创建 T 实例: new T(modMaster, details)
            var ctor = paramType.GetConstructor(new[] { typeof(ModMasterModel), typeof(List<ModDetailModel>) });
            if (ctor == null)
            {
                iTemplate.Load();
                return;
            }

            var param = ctor.Invoke(new object[] { modMaster, details });
            if (param == null) return;

            // 创建 TemplateModel<T> 实例
            var templateModelType = typeof(TemplateModel<>).MakeGenericType(paramType);
            var templateModel = Activator.CreateInstance(templateModelType, new object[] { modMaster.Name ?? "default", param });

            // 获取 TemplateParams 属性并添加
            var templateParamsProperty = templateType.GetProperty("TemplateParams");
            if (templateParamsProperty != null)
            {
                var collection = templateParamsProperty.GetValue(iTemplate);
                var addMethod = collection?.GetType().GetMethod("Add");
                addMethod?.Invoke(collection, new[] { templateModel });
            }
        }
    }
}
