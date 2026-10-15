using System;
using System.ComponentModel;

namespace ColorVision.Engine
{
    public abstract class ResultViewConfig : ViewConfigBase
    {
        private int _maxHistoryCount = 500;

        [DisplayName("实时结果保留上限"), Category("View")]
        [Description("实时新增结果时，列表最多保留的条数，默认 500，至少 1 条。搜索按查询设置完整加载；之后有新实时结果时，旧搜索记录也可被淘汰。不删除数据库记录。")]
        public int MaxHistoryCount
        {
            get => _maxHistoryCount;
            set
            {
                int count = Math.Max(1, value);
                if (_maxHistoryCount == count) return;
                _maxHistoryCount = count;
                OnPropertyChanged();
            }
        }

    }
}
