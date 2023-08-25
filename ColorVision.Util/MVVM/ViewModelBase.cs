using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ColorVision.MVVM
{
    /// <summary>
    /// 实例化一个Mode
    /// </summary>
    public abstract class ViewModelBase :INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        /// <summary>
        /// 消息通知事件
        /// </summary>
        public void NotifyPropertyChanged([CallerMemberName] string propertyName = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        protected virtual bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = "")
        {
            storage = value;
            NotifyPropertyChanged(propertyName);
            return true;
        }
        protected virtual bool SetProperty1<T>(ref T storage, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(storage, value))
                return false;
            storage = value;
            NotifyPropertyChanged(propertyName);
            return true;
        }
    }

}
