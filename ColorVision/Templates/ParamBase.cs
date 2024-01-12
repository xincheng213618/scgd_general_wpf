
using ColorVision.MVVM;
using ColorVision.MySql.DAO;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;

namespace ColorVision.Templates
{
    public class ModelBase : ViewModelBase
    {
        private Dictionary<string, ModDetailModel> parameters;

        public ModelBase(List<ModDetailModel> detail)
        {
            parameters = new Dictionary<string, ModDetailModel>();
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

            if (ConfigHandler.GetInstance().SoftwareConfig.IsUseMySql)
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
            if (ConfigHandler.GetInstance().SoftwareConfig.IsUseMySql)
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
                return default(T);

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
        internal void GetDetail(List<ModDetailModel> list)
        {
            list.AddRange(parameters.Values.ToList());
        }


    }

    public class ParamBase: ModelBase
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
        public string Name { get => _Name; set { _Name = value ; NotifyPropertyChanged(); } }
        private string _Name;

        public ParamBase() : base(new List<ModDetailModel>())
        {
            this.Id = No++;
        }

        public ParamBase(int id,string  name,List<ModDetailModel> detail):base (detail)
        {
            this.Id = id;
            this.Name = name;
        }



    }
}
