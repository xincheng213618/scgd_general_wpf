using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;

namespace ColorVision.Engine.Templates
{
    public class ModelBase : ViewModelBase
    {
        private Dictionary<string, ModDetailModel> parameters;

        public ModelBase()
        {
            parameters = new Dictionary<string, ModDetailModel>();
        }

        public ModelBase(List<ModDetailModel> detail) : this()
        {
            AddDetail(detail);
        }

        public void AddDetail(List<ModDetailModel> detail)
        {
            if (detail != null)
            {
                foreach (var flowDetailModel in detail)
                {
                    if (flowDetailModel.Symbol != null && !parameters.ContainsKey(flowDetailModel.Symbol))
                    {
                        parameters.Add(flowDetailModel.Symbol, flowDetailModel);
                    }
                }
            }
        }

        protected override bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = "")
        {
            storage = value;
            if (parameters.Count > 0)
            {
                if (parameters.TryGetValue(propertyName, out ModDetailModel modDetailModel))
                {
                    modDetailModel.ValueB = modDetailModel.ValueA;
                    if (typeof(T) == typeof(double[]) && value is double[] doule)
                    {
                        modDetailModel.ValueA = DoubleArrayToString(doule);
                    }
                    else
                    {
                        modDetailModel.ValueA = value?.ToString();
                    }
                }
            }
            NotifyPropertyChanged(propertyName);
            return true;
        }

        public static double[] StringToDoubleArray(string input, char separator = ',')
        {
            if (string.IsNullOrWhiteSpace(input))
                return Array.Empty<double>();

            // 分割字符串
            string[] parts = input.Split(separator, StringSplitOptions.RemoveEmptyEntries);
            double[] doubles = new double[parts.Length];

            for (int i = 0; i < parts.Length; i++)
            {
                // 尝试转换每个部分
                if (!double.TryParse(parts[i].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out doubles[i]))
                {
                    return Array.Empty<double>();
                }
            }

            return doubles;
        }

        // 将 double 数组转换为字符串
        public static string DoubleArrayToString(double[] array, char separator = ',')
        {
            return string.Join(separator.ToString(), array.Select(d => d.ToString(CultureInfo.InvariantCulture)));
        }


        protected void SetProperty<T>(T value, [CallerMemberName] string propertyName = "")
        {
            if (parameters.TryGetValue(propertyName, out ModDetailModel modDetailModel))
            {
                modDetailModel.ValueB = modDetailModel.ValueA;
                modDetailModel.ValueA = value?.ToString();
            }
        }


        public T? GetValue<T>(T? storage, [CallerMemberName] string propertyName = "")
        {
            if (parameters != null && parameters.Count > 0)
            {
                string val = "";
                if (parameters.TryGetValue(propertyName, out ModDetailModel modDetailModel))
                {
                    val = modDetailModel.ValueA;
                    if (typeof(T) == typeof(int))
                    {
                        if (string.IsNullOrEmpty(val)) val = "0";
                        return (T)(object)int.Parse(val);
                    }
                    else if (typeof(T) == typeof(string))
                    {
                        return (T)(object)val;
                    }
                    else if (typeof(T) == typeof(bool))
                    {
                        if (string.IsNullOrEmpty(val)) val = "False";
                        bool result1 = bool.TryParse(val, out bool result);
                        return (T)(object)(result && result1);
                    }
                    else if (typeof(T) == typeof(float))
                    {
                        if (string.IsNullOrEmpty(val)) val = "0.0";
                        return (T)(object)float.Parse(val);
                    }
                    else if (typeof(T) == typeof(double))
                    {
                        if (string.IsNullOrEmpty(val)) val = "0.0";
                        return (T)(object)double.Parse(val);
                    }
                    else if (typeof(T).IsEnum)
                    {
                        Enum.TryParse(typeof(T), val, out object obj);
                        return (T)obj;
                    }
                    else if (typeof(T) == typeof(double[]))
                    {
                        return (T)(object)StringToDoubleArray(val ?? string.Empty);
                    }
                }
                return default;
            }
            return storage;
        }


        public ModDetailModel? GetParameter(string key)
        {
            if (parameters.TryGetValue(key, out ModDetailModel modDetailModel))
            {
                return modDetailModel;
            }
            else
            {
                return null;
            }
        }
        public void GetDetail(List<ModDetailModel> list)
        {
            list.AddRange(parameters.Values.ToList());
        }


    }

    public class ParamBase : ModelBase
    {
        public static int No { get; set; }

        public event EventHandler IsEnabledChanged;

        [Category("设置"), DisplayName("是否启用模板"), Browsable(false)]
        public bool IsEnable
        {
            get => _IsEnable; set
            {
                if (IsEnable == value) return;
                _IsEnable = value;
                if (value == true) IsEnabledChanged?.Invoke(this, new EventArgs());
                NotifyPropertyChanged();
            }
        }
        private bool _IsEnable;

        [Browsable(false)]
        public int Id { get => _Id; set { _Id = value; NotifyPropertyChanged(); } }
        private int _Id;

        [Browsable(false)]
        public string Name { get => _Name; set { _Name = value; NotifyPropertyChanged(); } }
        private string _Name;

        public virtual RelayCommand CreateCommand { get; set; }

        public ObservableCollection<ModDetailModel> ModDetailModels { get; set; } = new ObservableCollection<ModDetailModel>();
        public ParamBase() : base(new List<ModDetailModel>())
        {
            Id = No++;
            CreateCommand = new RelayCommand(a => new CreateModeDetail(this) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(), a => true);
        }

        public ParamBase(int id, string name, List<ModDetailModel> detail) : base(detail)
        {
            Id = id;
            Name = name;
            ModDetailModels = new ObservableCollection<ModDetailModel>(detail);
            CreateCommand = new RelayCommand(a => new CreateModeDetail(this) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(), a => true);
        }
    }
}
