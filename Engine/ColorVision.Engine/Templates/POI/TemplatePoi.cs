using ColorVision.Database;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.Templates.Browser;
using ColorVision.UI.Extension;
using Newtonsoft.Json;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows;

namespace ColorVision.Engine.Templates.POI
{
    public class TemplatePoi : ITemplate<PoiParam>,
        IITemplateLoad,
        IFlowPackageTemplateCodec
    {
        public static ObservableCollection<TemplateModel<PoiParam>> Params { get; set; } = new ObservableCollection<TemplateModel<PoiParam>>();

        private readonly PoiTemplateStorage storage;
        internal string BrowserOrderScope
        {
            get
            {
                if (storage.IsLocal) return "poi:local";
                var config = MySqlSetting.Instance.MySqlConfig;
                return "poi:" + TemplateBrowserOrderStore.MySqlScope(config.Host, config.Port, config.Database);
            }
        }
        public TemplatePoi() : this(PoiTemplateStorage.Default) { }
        public TemplatePoi(PoiTemplateStorage storage)
        {
            this.storage = storage;
            IsSideHide = true;
            TemplateDicId = -1;
            Title = ColorVision.Engine.Properties.Resources.POISetting;
            Code = "POI";
            TemplateParams = Params;
        }
        public EditPoiParam EditWindow { get; set; }
        public override Window CreateManagerWindow(int selectedIndex = 0) => new PoiTemplateManagerWindow(this, selectedIndex);
        public override void PreviewMouseDoubleClick(int index)
        {
            EditWindow = new EditPoiParam(Params[index].Value) { Owner = Application.Current.GetActiveWindow() };
            EditWindow.Show();
        }

        public override void Load()
        {
            var values = storage.Load();
            var ids = values.Select(x => x.Id).ToHashSet();
            foreach (var removed in Params.Where(x => !ids.Contains(x.Id)).ToList()) Params.Remove(removed);
            for (int i = 0; i < values.Count; i++)
            {
                var value = values[i];
                var existing = Params.FirstOrDefault(x => x.Id == value.Id);
                if (existing == null) Params.Insert(i, new TemplateModel<PoiParam>(value.Name, value));
                else
                {
                    existing.Value = value;
                    existing.Key = value.Name;
                    int previous = Params.IndexOf(existing);
                    if (previous != i) Params.Move(previous, i);
                }
            }
            Title = ColorVision.Engine.Properties.Resources.POISetting + (storage.IsLocal ? " · 本地" : " · MySQL");
            SaveIndex.Clear();
        }

        public override void Save()
        {
            foreach (var index in SaveIndex)
                if (index >= 0 && index < TemplateParams.Count)
                {
                    var value = TemplateParams[index].Value;
                    (value.Storage ?? storage).SaveMetadata(value);
                }
            SaveIndex.Clear();
        }

        public override void Save(TemplateModel<PoiParam> item) => (item.Value.Storage ?? storage).SaveMetadata(item.Value);

        public override void Delete(int index)
        {
            var value = TemplateParams[index].Value;
            (value.Storage ?? storage).Delete(value.Id);
            TemplateParams.RemoveAt(index);
        }

        public override bool CopyTo(int index)
        {
            PoiParam.LoadPoiDetailFromDB(TemplateParams[index].Value);
            string fileContent = TemplateParams[index].Value.ToJsonN();
            ImportTemp = JsonConvert.DeserializeObject<PoiParam>(fileContent);
            if (ImportTemp != null)
            {
                ImportTemp.Id = -1;
                foreach (var item in ImportTemp.PoiPoints)
                {
                    item.Id = -1;
                }
            }
            return true;
        }


        public override void Create(string templateName)
        {
            var value = ImportTemp != null ? CreatePortableSnapshot(ImportTemp) : new PoiParam { Id = -1 };
            value.Name = templateName;
            value.DetailsLoaded = true;
            storage.Save(value);
            TemplateParams.Add(new TemplateModel<PoiParam>(templateName, value));
        }

        public override object CreateDefault() => CreateTemp = new PoiParam { Id = -1 };

        public override bool ImportFile(string filePath)
        {
            if (!File.Exists(filePath)) return false;
            ImportName = Path.GetFileNameWithoutExtension(filePath);
            return TryPrepareFlowPackageImport(ImportName, File.ReadAllText(filePath));
        }

        public override void Export(int index)
        {
            var selected = TemplateParams.Where(x => x.IsSelected).ToList();
            if (selected.Count == 0) selected.Add(TemplateParams[index]);
            bool multiple = selected.Count > 1;
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = multiple ? "POI 模板包 (*.zip)|*.zip" : "POI 模板 (*.cfg)|*.cfg",
                FileName = multiple ? "POI.zip" : ColorVision.Common.Utilities.Tool.SanitizeFileName(selected[0].Key) + ".cfg",
                AddExtension = true,
                DefaultExt = multiple ? ".zip" : ".cfg"
            };
            if (dialog.ShowDialog(Application.Current.GetActiveWindow()) != true) return;
            var snapshots = selected.Select(x => CreatePortableSnapshot(x.Value, (x.Value.Storage ?? storage).ReadPoints(x.Id))).ToList();
            if (!multiple)
            {
                File.WriteAllText(dialog.FileName, JsonConvert.SerializeObject(snapshots[0], Formatting.Indented));
                return;
            }
            using var stream = File.Create(dialog.FileName);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
            for (int i = 0; i < snapshots.Count; i++)
            {
                var entry = archive.CreateEntry($"{i + 1}_{ColorVision.Common.Utilities.Tool.SanitizeFileName(snapshots[i].Name)}.cfg");
                using var writer = new StreamWriter(entry.Open());
                writer.Write(JsonConvert.SerializeObject(snapshots[i], Formatting.Indented));
            }
        }

        public object CaptureFlowPackageValue(int index)
        {
            PoiParam value = TemplateParams[index].Value;
            if (value.Id == -1 || value.Id == 0) throw new InvalidDataException("POI 模板尚未保存，无法确认模板明细是否完整。");
            IReadOnlyList<PoiPoint> persistedPoints =
                (value.Storage ?? storage).ReadPoints(value.Id);
            return CreatePortableSnapshot(value, persistedPoints);
        }

        internal static PoiParam CreatePortableSnapshot(
            PoiParam source,
            IEnumerable<PoiPoint>? authoritativePoints = null)
        {
            ArgumentNullException.ThrowIfNull(source);

            PoiParam snapshot =
                JsonConvert.DeserializeObject<PoiParam>(
                    JsonConvert.SerializeObject(source))
                ?? throw new InvalidDataException(
                    "无法创建可移植的 POI 模板快照。");
            if (authoritativePoints != null)
            {
                List<PoiPoint> points =
                    JsonConvert.DeserializeObject<List<PoiPoint>>(
                        JsonConvert.SerializeObject(
                            authoritativePoints))
                    ?? throw new InvalidDataException(
                        "无法创建可移植的 POI 点位快照。");
                snapshot.PoiPoints =
                    new ObservableCollection<PoiPoint>(points);
            }

            snapshot.Id = -1;
            foreach (PoiPoint point in snapshot.PoiPoints)
            {
                point.Id = -1;
            }
            return snapshot;
        }

        public bool TryPrepareFlowPackageImport(
            string templateName,
            string serializedContent)
        {
            PoiParam? value =
                JsonConvert.DeserializeObject<PoiParam>(
                    serializedContent);
            if (value == null)
                return false;
            PoiParam snapshot = CreatePortableSnapshot(value);
            snapshot.Name = templateName;
            ImportTemp = snapshot;
            return true;
        }

        public override bool Import()
        {
            System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
            ofd.Filter = "*.cfg|*.cfg";
            ofd.Title = ColorVision.Engine.Properties.Resources.Engine_Dlg_ImportTemplate;
            ofd.RestoreDirectory = true;
            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return false;
            try
            {
                return ImportFile(ofd.FileName);
            }
            catch (JsonException ex)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), $"解析模板文件时出错: {ex.Message}", "ColorVision");
                return false;
            }
        }

        public override bool SwapTemplateOrder(int index1, int index2)
        {
            if (index1 < 0 || index1 >= TemplateParams.Count || index2 < 0 || index2 >= TemplateParams.Count)
                return false;

            if (index1 == index2)
                return true;

            try
            {
                var template1 = TemplateParams[index1];
                var template2 = TemplateParams[index2];

                // Get the IDs from database
                int id1 = template1.Value.Id;
                int id2 = template2.Value.Id;

                if (PoiTemplateStorage.IsLocalId(id1) || PoiTemplateStorage.IsLocalId(id2))
                {
                    if (!PoiTemplateStorage.IsLocalId(id1) || !PoiTemplateStorage.IsLocalId(id2)) return false;
                    (template1.Value.Storage ?? storage).SwapLocalOrder(id1, id2);
                    (TemplateParams[index1], TemplateParams[index2]) = (TemplateParams[index2], TemplateParams[index1]);
                    return true;
                }
                if (!MySqlSetting.IsConnect) return false;

                // Swap the IDs in the database using a three-step process to avoid constraint violations
                // Use int.MinValue plus a hash-based offset incorporating both IDs to minimize collision risk
                int tempId = int.MinValue + Math.Abs((id1 ^ id2).GetHashCode());
                using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });

                // Step 1: Move template1 to temporary ID
                var poiMaster1 = Db.Queryable<PoiMasterModel>().InSingle(id1);
                if (poiMaster1 != null)
                {
                    poiMaster1.Id = tempId;
                    Db.Updateable(poiMaster1).ExecuteCommand();
                }

                var poiDetails1 = Db.Queryable<PoiDetailModel>().Where(x => x.Pid == id1).ToList();
                foreach (var detail in poiDetails1)
                {
                    detail.Pid = tempId;
                }
                if (poiDetails1.Count > 0)
                    Db.Updateable(poiDetails1).ExecuteCommand();

                // Step 2: Move template2 to id1
                var poiMaster2 = Db.Queryable<PoiMasterModel>().InSingle(id2);
                if (poiMaster2 != null)
                {
                    poiMaster2.Id = id1;
                    Db.Updateable(poiMaster2).ExecuteCommand();
                }

                var poiDetails2 = Db.Queryable<PoiDetailModel>().Where(x => x.Pid == id2).ToList();
                foreach (var detail in poiDetails2)
                {
                    detail.Pid = id1;
                }
                if (poiDetails2.Count > 0)
                    Db.Updateable(poiDetails2).ExecuteCommand();

                // Step 3: Move template1 from temporary to id2
                poiMaster1 = Db.Queryable<PoiMasterModel>().InSingle(tempId);
                if (poiMaster1 != null)
                {
                    poiMaster1.Id = id2;
                    Db.Updateable(poiMaster1).ExecuteCommand();
                }

                poiDetails1 = Db.Queryable<PoiDetailModel>().Where(x => x.Pid == tempId).ToList();
                foreach (var detail in poiDetails1)
                {
                    detail.Pid = id2;
                }
                if (poiDetails1.Count > 0)
                    Db.Updateable(poiDetails1).ExecuteCommand();

                // Update the in-memory values
                template1.Value.Id = id2;
                template2.Value.Id = id1;

                // Swap the items in the ObservableCollection using proper swap
                var temp = TemplateParams[index1];
                TemplateParams[index1] = TemplateParams[index2];
                TemplateParams[index2] = temp;

                return true;
            }
            catch (System.Exception)
            {
                // Let the caller handle the error display
                return false;
            }
        }
    }

}
