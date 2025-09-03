using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ColorVision.Common.MVVM
{
    /// <summary>
    /// 实例化一个Mode
    /// </summary>'
    [Serializable]
    public class ViewModelBase :INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        /// <summary>
        /// 消息通知事件
        /// </summary>
        protected void OnPropertyChanged([CallerMemberName] string propertyName = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        protected virtual bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = "")
        {
            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

}
