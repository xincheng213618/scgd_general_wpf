using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.Jsons.MTF2;
using System.Collections.ObjectModel;

namespace ProjectARVRPro.Process.MTFHV
{
    public class MTFHVViewTestResult : MTFHVTestResult
    {
        public MTFDetailViewReslut MTFDetailViewReslut { get; set; }

    }

    public class MTFHVTestResult : ViewModelBase
    {

        /// <summary>
        /// MTF_HV_H 中心_0F 测试项
        /// </summary>
        public ObjectiveTestItem MTF_HV_H_Center_0F { get; set; } = new ObjectiveTestItem() { Name = "MTF_HV_H_Center_0F", Unit = "%" };
        /// <summary>
        /// MTF_HV_V 中心_0F 测试项
        /// </summary>
        public ObjectiveTestItem MTF_HV_V_Center_0F { get; set; } = new ObjectiveTestItem() { Name = "MTF_HV_V_Center_0F", Unit = "%" };

        /// <summary>
        /// MTF_HV_H 左上_0.3F 测试项
        /// </summary>
        public ObjectiveTestItem MTF_HV_H_LeftUp_0_3F { get; set; } = new ObjectiveTestItem() { Name = "MTF_HV_H_LeftUp_0_3F", Unit = "%" };


        /// <summary>
        /// MTF_HV_V 左上_0.3F 测试项
        /// </summary>
        public ObjectiveTestItem MTF_HV_V_LeftUp_0_3F { get; set; } = new ObjectiveTestItem() { Name = "MTF_HV_V_LeftUp_0_3F", Unit = "%" };

        /// <summary>
        /// MTF_HV_H 右上_0.3F 测试项
        /// </summary>
        public ObjectiveTestItem MTF_HV_H_RightUp_0_3F { get; set; } = new ObjectiveTestItem() { Name = "MTF_HV_H_RightUp_0_3F", Unit = "%" };

        /// <summary>
        /// MTF_HV_V 右上_0.3F 测试项
        /// </summary>
        public ObjectiveTestItem MTF_HV_V_RightUp_0_3F { get; set; } = new ObjectiveTestItem() { Name = "MTF_HV_V_RightUp_0_3F", Unit = "%" };


        /// <summary>
        /// MTF_HV_H 右下_0.3F 测试项
        /// </summary>
        public ObjectiveTestItem MTF_HV_H_RightDown_0_3F { get; set; } = new ObjectiveTestItem() { Name = "MTF_HV_H_RightDown_0_3F", Unit = "%" };

        /// <summary>
        /// MTF_HV_V 右下_0.3F 测试项
        /// </summary>
        public ObjectiveTestItem MTF_HV_V_RightDown_0_3F { get; set; } = new ObjectiveTestItem() { Name = "MTF_HV_V_RightDown_0_3F", Unit = "%" };

        /// <summary>
        /// MTF_HV_H 左下_0.3F 测试项
        /// </summary>
        public ObjectiveTestItem MTF_HV_H_LeftDown_0_3F { get; set; } = new ObjectiveTestItem() { Name = "MTF_HV_H_LeftDown_0_3F", Unit = "%" };


        /// <summary>
        /// MTF_HV_V 左下_0.3F 测试项
        /// </summary>
        public ObjectiveTestItem MTF_HV_V_LeftDown_0_3F { get; set; } = new ObjectiveTestItem() { Name = "MTF_HV_V_LeftDown_0_3F", Unit = "%" };

        /// <summary>
        /// MTF_HV_H 左上_0.6F 测试项
        /// </summary>
        public ObjectiveTestItem MTF_HV_H_LeftUp_0_6F { get; set; } = new ObjectiveTestItem() { Name = "MTF_HV_H_LeftUp_0_6F", Unit = "%" };

        /// <summary>
        /// MTF_HV_V 左上_0.6F 测试项
        /// </summary>
        public ObjectiveTestItem MTF_HV_V_LeftUp_0_6F { get; set; } = new ObjectiveTestItem() { Name = "MTF_HV_V_LeftUp_0_6F", Unit = "%" };

        /// <summary>
        /// MTF_HV_H 右上_0.6F 测试项
        /// </summary>
        public ObjectiveTestItem MTF_HV_H_RightUp_0_6F { get; set; } = new ObjectiveTestItem() { Name = "MTF_HV_H_RightUp_0_6F", Unit = "%" };

        /// <summary>
        /// MTF_HV_V 右上_0.6F 测试项
        /// </summary>
        public ObjectiveTestItem MTF_HV_V_RightUp_0_6F { get; set; } = new ObjectiveTestItem() { Name = "MTF_HV_V_RightUp_0_6F", Unit = "%" };

        /// <summary>
        /// MTF_HV_H 右下_0.6F 测试项
        /// </summary>
        public ObjectiveTestItem MTF_HV_H_RightDown_0_6F { get; set; } = new ObjectiveTestItem() { Name = "MTF_HV_H_RightDown_0_6F", Unit = "%" };

        /// <summary>
        /// MTF_HV_V 右下_0.6F 测试项
        /// </summary>
        public ObjectiveTestItem MTF_HV_V_RightDown_0_6F { get; set; } = new ObjectiveTestItem() { Name = "MTF_HV_V_RightDown_0_6F", Unit = "%" };

        /// <summary>
        /// MTF_HV_H 左下_0.6F 测试项
        /// </summary>
        public ObjectiveTestItem MTF_HV_H_LeftDown_0_6F { get; set; } = new ObjectiveTestItem() { Name = "MTF_HV_H_LeftDown_0_6F", Unit = "%" };



        /// <summary>
        /// MTF_HV_V 左下_0.6F 测试项
        /// </summary>
        public ObjectiveTestItem MTF_HV_V_LeftDown_0_6F { get; set; } = new ObjectiveTestItem() { Name = "MTF_HV_V_LeftDown_0_6F", Unit = "%" };

        /// <summary>
        /// MTF_HV_H 左上_0.8F 测试项
        /// </summary>
        public ObjectiveTestItem MTF_HV_H_LeftUp_0_8F { get; set; } = new ObjectiveTestItem() { Name = "MTF_HV_H_LeftUp_0_8F", Unit = "%" };

        /// <summary>
        /// MTF_HV_V 左上_0.8F 测试项
        /// </summary>
        public ObjectiveTestItem MTF_HV_V_LeftUp_0_8F { get; set; } = new ObjectiveTestItem() { Name = "MTF_HV_V_LeftUp_0_8F", Unit = "%" };

        /// <summary>
        /// MTF_HV_H 右上_0.8F 测试项
        /// </summary>
        public ObjectiveTestItem MTF_HV_H_RightUp_0_8F { get; set; } = new ObjectiveTestItem() { Name = "MTF_HV_H_RightUp_0_8F", Unit = "%" };


        /// <summary>
        /// MTF_HV_V 右上_0.8F 测试项
        /// </summary>
        public ObjectiveTestItem MTF_HV_V_RightUp_0_8F { get; set; } = new ObjectiveTestItem() { Name = "MTF_HV_V_RightUp_0_8F", Unit = "%" };

        /// <summary>
        /// MTF_HV_H 右下_0.8F 测试项
        /// </summary>
        public ObjectiveTestItem MTF_HV_H_RightDown_0_8F { get; set; } = new ObjectiveTestItem() { Name = "MTF_HV_H_RightDown_0_8F", Unit = "%" };


        /// <summary>
        /// MTF_HV_V 右下_0.8F 测试项
        /// </summary>
        public ObjectiveTestItem MTF_HV_V_RightDown_0_8F { get; set; } = new ObjectiveTestItem() { Name = "MTF_HV_V_RightDown_0_8F", Unit = "%" };

        /// <summary>
        /// MTF_HV_H 左下_0.8F 测试项
        /// </summary>
        public ObjectiveTestItem MTF_HV_H_LeftDown_0_8F { get; set; } = new ObjectiveTestItem() { Name = "MTF_HV_H_LeftDown_0_8F", Unit = "%" };
        /// <summary>
        /// MTF_HV_V 左下_0.8F 测试项
        /// </summary>
        public ObjectiveTestItem MTF_HV_V_LeftDown_0_8F { get; set; } = new ObjectiveTestItem() { Name = "MTF_HV_V_LeftDown_0_8F", Unit = "%" };

    }

}
