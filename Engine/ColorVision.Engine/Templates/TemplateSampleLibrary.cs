using ColorVision.Common.Utilities;
using ColorVision.UI;
using ColorVision.UI.Extension;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ColorVision.Engine.Templates
{
    [SugarTable("template_samples")]
    public class TemplateSampleRecord
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        [SugarColumn(Length = 128, IsNullable = false)]
        public string TemplateCode { get; set; } = string.Empty;

        [SugarColumn(Length = 512, IsNullable = true)]
        public string TemplateType { get; set; } = string.Empty;

        [SugarColumn(Length = 128, IsNullable = false)]
        public string GroupName { get; set; } = TemplateSampleLibrary.DefaultGroupName;

        [SugarColumn(Length = 256, IsNullable = false)]
        public string Name { get; set; } = string.Empty;

        [SugarColumn(Length = 512, IsNullable = true)]
        public string Description { get; set; } = string.Empty;

        [SugarColumn(ColumnDataType = "TEXT", IsNullable = false)]
        public string Content { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }

    public sealed class TemplateSampleLibrary
    {
        public const string DefaultGroupName = "默认组";

        private static readonly Lazy<TemplateSampleLibrary> InstanceHolder =
            new Lazy<TemplateSampleLibrary>(() => new TemplateSampleLibrary());

        private readonly object _syncRoot = new object();

        public static string DirectoryPath { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Environments.AssemblyCompany,
            "Templates");

        public static string DbPath { get; set; } = Path.Combine(DirectoryPath, "TemplateSamples.db");

        private TemplateSampleLibrary()
        {
            Directory.CreateDirectory(DirectoryPath);

            using SqlSugarClient db = CreateDbClient();
            db.CodeFirst.InitTables<TemplateSampleRecord>();
        }

        public static TemplateSampleLibrary GetInstance()
        {
            return InstanceHolder.Value;
        }

        public IReadOnlyList<TemplateSampleRecord> GetSamples(ITemplate template)
        {
            string templateCode = NormalizeTemplateCode(template);
            string templateType = NormalizeTemplateType(template);

            lock (_syncRoot)
            {
                using SqlSugarClient db = CreateDbClient();
                return db.Queryable<TemplateSampleRecord>()
                    .Where(it => it.TemplateCode == templateCode || it.TemplateType == templateType)
                    .OrderBy(it => it.GroupName)
                    .OrderBy(it => it.Name, OrderByType.Asc)
                    .ToList();
            }
        }

        public IReadOnlyList<string> GetGroupNames(ITemplate template)
        {
            return GetSamples(template)
                .Select(it => NormalizeGroupName(it.GroupName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(it => it, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        public TemplateSampleRecord SaveFromTemplate(ITemplate template, int index, string groupName, string sampleName, string description)
        {
            if (index < 0 || index >= template.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            string normalizedGroup = NormalizeGroupName(groupName);
            string normalizedName = NormalizeSampleName(sampleName, template.GetTemplateName(index));
            string templateCode = NormalizeTemplateCode(template);
            string templateType = NormalizeTemplateType(template);
            string content = GetTemplateContent(template, index);
            DateTime now = DateTime.Now;

            lock (_syncRoot)
            {
                using SqlSugarClient db = CreateDbClient();
                TemplateSampleRecord? record = db.Queryable<TemplateSampleRecord>()
                    .First(it => it.TemplateCode == templateCode && it.GroupName == normalizedGroup && it.Name == normalizedName);

                if (record == null)
                {
                    record = new TemplateSampleRecord
                    {
                        TemplateCode = templateCode,
                        TemplateType = templateType,
                        GroupName = normalizedGroup,
                        Name = normalizedName,
                        Description = description?.Trim() ?? string.Empty,
                        Content = content,
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    record.Id = db.Insertable(record).ExecuteReturnIdentity();
                }
                else
                {
                    record.TemplateType = templateType;
                    record.Description = description?.Trim() ?? record.Description;
                    record.Content = content;
                    record.UpdatedAt = now;
                    db.Updateable(record).ExecuteCommand();
                }

                return record;
            }
        }

        public static SqlSugarClient CreateDbClient()
        {
            return new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={DbPath}",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute
            });
        }

        private static string GetTemplateContent(ITemplate template, int index)
        {
            object templateValue = template.GetValue(index);
            if (templateValue is TemplateBase templateBase)
            {
                return templateBase.GetValue().ToJsonN();
            }

            return templateValue.ToJsonN();
        }

        private static string NormalizeTemplateCode(ITemplate template)
        {
            return string.IsNullOrWhiteSpace(template.Code) ? template.GetType().FullName ?? template.GetType().Name : template.Code.Trim();
        }

        private static string NormalizeTemplateType(ITemplate template)
        {
            return template.GetTemplateType?.FullName ?? template.GetType().FullName ?? template.GetType().Name;
        }

        private static string NormalizeGroupName(string groupName)
        {
            return string.IsNullOrWhiteSpace(groupName) ? DefaultGroupName : groupName.Trim();
        }

        private static string NormalizeSampleName(string sampleName, string fallbackName)
        {
            string normalized = string.IsNullOrWhiteSpace(sampleName) ? fallbackName : sampleName.Trim();
            normalized = string.IsNullOrWhiteSpace(normalized) ? "TemplateSample" : normalized;
            return Tool.SanitizeFileName(normalized);
        }
    }
}