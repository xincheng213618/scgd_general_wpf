using ColorVision.Common.MVVM;
using ColorVision.UI;
using log4net;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Windows.Controls;

namespace ColorVision.Engine
{
    public class DisplayAlgorithmParam
    {
        public Type Type { get; set; }
        public string?  ImageFilePath { get; set; }
    }

    public class DisplayAlgorithmManager : ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(DisplayAlgorithmManager));
        private static DisplayAlgorithmManager _instance;
        private static readonly object _locker = new();
        public static DisplayAlgorithmManager GetInstance() { lock (_locker) { _instance ??= new DisplayAlgorithmManager(); return _instance; } }
        
        private const string PersistFileName = "DisplayAlgorithmConfig.json";
        private static string PersistDirectory => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ColorVision", "Config");
        private static string PersistFilePath => System.IO.Path.Combine(PersistDirectory, PersistFileName);
        
        public ObservableCollection<IResultHandleBase> ResultHandles { get; set; }
        public ObservableCollection<IDisplayAlgorithm> DisplayAlgorithms { get; } = new ObservableCollection<IDisplayAlgorithm>();
        public ObservableCollection<DisplayAlgorithmMeta> AlgorithmMetas { get; } = new ObservableCollection<DisplayAlgorithmMeta>();

        // Properties for creation
        public string NewMetaName { get => _NewMetaName; set { _NewMetaName = value; OnPropertyChanged(); System.Windows.Input.CommandManager.InvalidateRequerySuggested(); } }
        private string _NewMetaName;

        public IDisplayAlgorithm SelectedAlgorithm { get => _SelectedAlgorithm; set { _SelectedAlgorithm = value; OnPropertyChanged(); System.Windows.Input.CommandManager.InvalidateRequerySuggested(); } }
        private IDisplayAlgorithm _SelectedAlgorithm;

        public DisplayAlgorithmMeta SelectedAlgorithmMeta { get => _SelectedAlgorithmMeta; set { _SelectedAlgorithmMeta = value; OnPropertyChanged(); System.Windows.Input.CommandManager.InvalidateRequerySuggested(); } }
        private DisplayAlgorithmMeta _SelectedAlgorithmMeta;

        public RelayCommand EditCommand { get; set; }
        public RelayCommand AddMetaCommand { get; set; }
        public RelayCommand RemoveMetaCommand { get; set; }
        public RelayCommand MoveUpCommand { get; set; }
        public RelayCommand MoveDownCommand { get; set; }

        public DisplayAlgorithmManager()
        {
            ResultHandles = new ObservableCollection<IResultHandleBase>();

            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                foreach (Type type in assembly.GetTypes().Where(t => typeof(IResultHandleBase).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    if (Activator.CreateInstance(type) is IResultHandleBase ViewResultAlgRender)
                    {
                        ResultHandles.Add(ViewResultAlgRender);
                    }
                }
            }
            
            LoadDisplayAlgorithms();
            
            AlgorithmMetas.CollectionChanged += AlgorithmMetas_CollectionChanged;
            EditCommand = new RelayCommand(a => Edit());
            AddMetaCommand = new RelayCommand(a => AddMeta(), a => CanAddMeta());
            RemoveMetaCommand = new RelayCommand(a => RemoveMeta(), a => SelectedAlgorithmMeta != null);
            MoveUpCommand = new RelayCommand(a => MoveUp(), a => CanMoveUp());
            MoveDownCommand = new RelayCommand(a => MoveDown(), a => CanMoveDown());
            
            LoadPersistedMetas();
        }
        
        private void LoadDisplayAlgorithms()
        {
            var algorithmList = new System.Collections.Generic.List<IDisplayAlgorithm>();
            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                try
                {
                    foreach (Type type in assembly.GetTypes().Where(t => typeof(IDisplayAlgorithm).IsAssignableFrom(t) && !t.IsAbstract))
                    {
                        var attr = type.GetCustomAttribute<DisplayAlgorithmAttribute>();
                        if (attr != null)
                        {
                            // Create instance without device parameter for the manager list
                            try
                            {
                                if (Activator.CreateInstance(type) is IDisplayAlgorithm algorithm)
                                {
                                    algorithmList.Add(algorithm);
                                }
                            }
                            catch
                            {
                                // Skip algorithms that require constructor parameters
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                }
            }
            
            // Sort by metadata order, then by display name
            var sortedAlgorithms = algorithmList
                .Select(a => new { Algorithm = a, Metadata = DisplayAlgorithmMetadata.FromAlgorithm(a) })
                .OrderBy(x => x.Metadata.Order)
                .ThenBy(x => x.Metadata.DisplayName)
                .Select(x => x.Algorithm);
            
            foreach (var algorithm in sortedAlgorithms)
            {
                DisplayAlgorithms.Add(algorithm);
            }
        }
        
        private void AlgorithmMetas_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (DisplayAlgorithmMeta meta in e.NewItems)
                {
                    meta.PropertyChanged += Meta_PropertyChanged;
                }
            }
            if (e.OldItems != null)
            {
                foreach (DisplayAlgorithmMeta meta in e.OldItems)
                {
                    meta.PropertyChanged -= Meta_PropertyChanged;
                }
            }
            SavePersistedMetas();
            NotifyAllOrders();
        }

        private void NotifyAllOrders()
        {
            int order = 1;
            foreach (var meta in AlgorithmMetas)
            {
                meta.OnPropertyChanged(nameof(DisplayAlgorithmMeta.Order));
            }
        }

        private void Meta_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            SavePersistedMetas();
        }

        public void Edit()
        {
            DisplayAlgorithmManagerWindow managerWindow = new DisplayAlgorithmManagerWindow() 
            { 
                Owner = System.Windows.Application.Current.GetActiveWindow(), 
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner 
            };
            managerWindow.DataContext = this;
            managerWindow.ShowDialog();
        }

        private bool CanAddMeta()
        {
            return !string.IsNullOrWhiteSpace(NewMetaName) && SelectedAlgorithm != null;
        }

        private void AddMeta()
        {
            if (!CanAddMeta()) return;
            if (AlgorithmMetas.Any(m => m.Name.Equals(NewMetaName, StringComparison.OrdinalIgnoreCase)))
            {
                System.Windows.MessageBox.Show(System.Windows.Application.Current.GetActiveWindow(), "名称重复", "ColorVision");
                return;
            }
            
            // Create a new instance of the algorithm
            IDisplayAlgorithm newAlgorithm = null;
            try
            {
                newAlgorithm = Activator.CreateInstance(SelectedAlgorithm.GetType()) as IDisplayAlgorithm;
            }
            catch
            {
                // If instance creation fails, use the selected one
                newAlgorithm = SelectedAlgorithm;
            }
            
            AlgorithmMetas.Add(new DisplayAlgorithmMeta
            {
                Name = NewMetaName,
                DisplayAlgorithm = newAlgorithm
            });
            NewMetaName = string.Empty;
        }

        private void RemoveMeta()
        {
            if (SelectedAlgorithmMeta != null)
            {
                AlgorithmMetas.Remove(SelectedAlgorithmMeta);
                SelectedAlgorithmMeta = null;
            }
        }

        private bool CanMoveUp()
        {
            return SelectedAlgorithmMeta != null && AlgorithmMetas.IndexOf(SelectedAlgorithmMeta) > 0;
        }

        private void MoveUp()
        {
            if (!CanMoveUp()) return;
            int index = AlgorithmMetas.IndexOf(SelectedAlgorithmMeta);
            AlgorithmMetas.Move(index, index - 1);
        }

        private bool CanMoveDown()
        {
            return SelectedAlgorithmMeta != null && AlgorithmMetas.IndexOf(SelectedAlgorithmMeta) < AlgorithmMetas.Count - 1;
        }

        private void MoveDown()
        {
            if (!CanMoveDown()) return;
            int index = AlgorithmMetas.IndexOf(SelectedAlgorithmMeta);
            AlgorithmMetas.Move(index, index + 1);
        }

        private void LoadPersistedMetas()
        {
            try
            {
                if (!System.IO.Directory.Exists(PersistDirectory)) System.IO.Directory.CreateDirectory(PersistDirectory);
                if (!System.IO.File.Exists(PersistFilePath)) return;
                string json = System.IO.File.ReadAllText(PersistFilePath);
                var list = Newtonsoft.Json.JsonConvert.DeserializeObject<System.Collections.Generic.List<DisplayAlgorithmMetaPersist>>(json) 
                    ?? new System.Collections.Generic.List<DisplayAlgorithmMetaPersist>();
                    
                AlgorithmMetas.CollectionChanged -= AlgorithmMetas_CollectionChanged;
                foreach (var item in list)
                {
                    IDisplayAlgorithm algorithm = null;
                    var templateAlgorithm = DisplayAlgorithms.FirstOrDefault(a => a.GetType().FullName == item.AlgorithmTypeFullName);
                    
                    if (templateAlgorithm != null)
                    {
                        try
                        {
                            algorithm = Activator.CreateInstance(templateAlgorithm.GetType()) as IDisplayAlgorithm;
                        }
                        catch
                        {
                            algorithm = templateAlgorithm;
                        }
                    }
                    else
                    {
                        try
                        {
                            var t = AppDomain.CurrentDomain.GetAssemblies()
                                .SelectMany(a => a.GetTypes())
                                .FirstOrDefault(x => x.FullName == item.AlgorithmTypeFullName && typeof(IDisplayAlgorithm).IsAssignableFrom(x));
                            if (t != null)
                            {
                                algorithm = Activator.CreateInstance(t) as IDisplayAlgorithm;
                                if (algorithm != null && !DisplayAlgorithms.Any(a => a.GetType().FullName == algorithm.GetType().FullName))
                                    DisplayAlgorithms.Add(algorithm);
                            }
                        }
                        catch (Exception ex)
                        {
                            log.Warn($"无法实例化算法类型 {item.AlgorithmTypeFullName}: {ex.Message}");
                        }
                    }
                    
                    DisplayAlgorithmMeta meta = new DisplayAlgorithmMeta() 
                    { 
                        Name = item.Name, 
                        DisplayAlgorithm = algorithm,
                        ConfigJson = item.ConfigJson,
                        Tag = item.Tag
                    };
                    
                    meta.ApplyConfig();
                    meta.PropertyChanged += Meta_PropertyChanged;
                    AlgorithmMetas.Add(meta);
                }
                AlgorithmMetas.CollectionChanged += AlgorithmMetas_CollectionChanged;
            }
            catch (Exception ex)
            {
                log.Error("加载DisplayAlgorithmMetas失败", ex);
            }
        }

        private void SavePersistedMetas()
        {
            try
            {
                if (!System.IO.Directory.Exists(PersistDirectory)) System.IO.Directory.CreateDirectory(PersistDirectory);
                var list = AlgorithmMetas.Select(m => new DisplayAlgorithmMetaPersist
                {
                    Name = m.Name,
                    AlgorithmTypeFullName = m.DisplayAlgorithm?.GetType().FullName,
                    ConfigJson = m.ConfigJson,
                    Tag = m.Tag
                }).ToList();
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(list, Newtonsoft.Json.Formatting.Indented);
                System.IO.File.WriteAllText(PersistFilePath, json);
            }
            catch (Exception ex)
            {
                log.Error("保存DisplayAlgorithmMetas失败", ex);
            }
        }
        
        public event EventHandler<DisplayAlgorithmParam> SelectParamChanged;

        public void SetType(DisplayAlgorithmParam param)
        {
            if (param == null) return;
            SelectParamChanged?.Invoke(this, param);
        }

    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class DisplayAlgorithmAttribute : Attribute
    {
        public int Order { get; }
        public string Name { get; }
        public string Group { get; }

        public DisplayAlgorithmAttribute(int order, string name, string group)
        {
            Order = order;
            Name = name;
            Group = group;
        }
    }
    
    public interface IDisplayAlgorithm
    {
        public bool IsLocalFile { get; set; }

        public string ImageFilePath { get; set; }

        public UserControl GetUserControl();
    }

    public abstract class DisplayAlgorithmBase : ViewModelBase, IDisplayAlgorithm
    {
        public bool IsLocalFile { get => _IsLocalFile; set { _IsLocalFile = value; OnPropertyChanged(); } }
        private bool _IsLocalFile;

        public string ImageFilePath { get => _ImageFilePath; set { _ImageFilePath = value; OnPropertyChanged(); } }
        private string _ImageFilePath;



        public virtual UserControl GetUserControl()
        {
            throw new NotImplementedException();
        }
    };
}
