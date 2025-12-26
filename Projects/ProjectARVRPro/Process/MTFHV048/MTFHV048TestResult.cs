using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.Jsons.MTF2;

namespace ProjectARVRPro.Process.MTFHV048
{
    public class MTFHV048ViewTestResult : MTFHV048TestResult
    {
        public MTFDetailViewReslut MTFDetailViewReslut { get; set; }

    }

    public class MTFHV048TestResult : ViewModelBase
    {

        /// <summary>
        /// MTF048_H 中心_0F 水平方向
        /// </summary>
        public ObjectiveTestItem MTF_HV_H_Center_0F { get; set; } = new ObjectiveTestItem() { Name = "MTF_HV_H_Center_0F", Unit = "%" };
        /// <summary>
        /// MTF048_V 中心_0F 垂直方向
        /// </summary>
        public ObjectiveTestItem MTF_HV_V_Center_0F { get; set; } = new ObjectiveTestItem() { Name = "MTF_HV_V_Center_0F", Unit = "%" };

        /// <summary>
        /// MTF048_H 左上_0.3F 水平方向
        /// </summary>
        public ObjectiveTestItem MTF_HV_H_LeftUp_0_5F { get; set; } = new ObjectiveTestItem() { Name = "MTF_HV_H_LeftUp_0_5F", Unit = "%" };


        /// <summary>
        /// MTF048_V 左上_0.3F 垂直方向
        /// </summary>
        public ObjectiveTestItem MTF_HV_V_LeftUp_0_5F { get; set; } = new ObjectiveTestItem() { Name = "MTF_HV_V_LeftUp_0_5F", Unit = "%" };

        /// <summary>
        /// MTF048_H 右上_0.3F 水平方向
        /// </summary>
        public ObjectiveTestItem MTF_HV_H_RightUp_0_5F { get; set; } = new ObjectiveTestItem() { Name = "MTF_HV_H_RightUp_0_5F", Unit = "%" };

        /// <summary>
        /// MTF048_V 右上_0.3F 垂直方向
        /// </summary>
        public ObjectiveTestItem MTF_HV_V_RightUp_0_5F { get; set; } = new ObjectiveTestItem() { Name = "MTF_HV_V_RightUp_0_5F", Unit = "%" };


        /// <summary>
        /// MTF048_H 右下_0.3F 水平方向
        /// </summary>
        public ObjectiveTestItem MTF_HV_H_RightDown_0_5F { get; set; } = new ObjectiveTestItem() { Name = "MTF_HV_H_RightDown_0_5F", Unit = "%" };

        /// <summary>
        /// MTF048_V 右下_0.3F 垂直方向
        /// </summary>
        public ObjectiveTestItem MTF_HV_V_RightDown_0_5F { get; set; } = new ObjectiveTestItem() { Name = "MTF_HV_V_RightDown_0_5F", Unit = "%" };

        /// <summary>
        /// MTF048_H 左下_0.3F 水平方向
        /// </summary>
        public ObjectiveTestItem MTF_HV_H_LeftDown_0_5F { get; set; } = new ObjectiveTestItem() { Name = "MTF_HV_H_LeftDown_0_5F", Unit = "%" };


        /// <summary>
        /// MTF048_V 左下_0.3F 垂直方向
        /// </summary>
        public ObjectiveTestItem MTF_HV_V_LeftDown_0_5F { get; set; } = new ObjectiveTestItem() { Name = "MTF_HV_V_LeftDown_0_5F", Unit = "%" };

        /// <summary>
        /// MTF048_H 左上_0.8F 水平方向
        /// </summary>
        public ObjectiveTestItem MTF_HV_H_LeftUp_0_8F { get; set; } = new ObjectiveTestItem() { Name = "MTF_HV_H_LeftUp_0_8F", Unit = "%" };

        /// <summary>
        /// MTF048_V 左上_0.8F 垂直方向
        /// </summary>
        public ObjectiveTestItem MTF_HV_V_LeftUp_0_8F { get; set; } = new ObjectiveTestItem() { Name = "MTF_HV_V_LeftUp_0_8F", Unit = "%" };

        /// <summary>
        /// MTF048_H 右上_0.8F 水平方向
        /// </summary>
        public ObjectiveTestItem MTF_HV_H_RightUp_0_8F { get; set; } = new ObjectiveTestItem() { Name = "MTF_HV_H_RightUp_0_8F", Unit = "%" };


        /// <summary>
        /// MTF048_V 右上_0.8F 垂直方向
        /// </summary>
        public ObjectiveTestItem MTF_HV_V_RightUp_0_8F { get; set; } = new ObjectiveTestItem() { Name = "MTF_HV_V_RightUp_0_8F", Unit = "%" };

        /// <summary>
        /// MTF048_H 右下_0.8F 水平方向
        /// </summary>
        public ObjectiveTestItem MTF_HV_H_RightDown_0_8F { get; set; } = new ObjectiveTestItem() { Name = "MTF_HV_H_RightDown_0_8F", Unit = "%" };


        /// <summary>
        /// MTF048_V 右下_0.8F 垂直方向
        /// </summary>
        public ObjectiveTestItem MTF_HV_V_RightDown_0_8F { get; set; } = new ObjectiveTestItem() { Name = "MTF_HV_V_RightDown_0_8F", Unit = "%" };

        /// <summary>
        /// MTF048_H 左下_0.8F 水平方向
        /// </summary>
        public ObjectiveTestItem MTF_HV_H_LeftDown_0_8F { get; set; } = new ObjectiveTestItem() { Name = "MTF_HV_H_LeftDown_0_8F", Unit = "%" };
        /// <summary>
        /// MTF048_V 左下_0.8F 垂直方向
        /// </summary>
        public ObjectiveTestItem MTF_HV_V_LeftDown_0_8F { get; set; } = new ObjectiveTestItem() { Name = "MTF_HV_V_LeftDown_0_8F", Unit = "%" };

    }

}
