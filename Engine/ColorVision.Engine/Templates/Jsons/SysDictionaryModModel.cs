using ColorVision.Database;
using ColorVision.Engine.Templates.SysDictionary;
using SqlSugar;
using System;

namespace ColorVision.Engine.Templates.Jsons
{

    public class DicTemplateJsonDao : BaseTableDao<SysDictionaryModModel>
    {

        public static DicTemplateJsonDao Instance { get; set; } = new DicTemplateJsonDao();
    }

}
