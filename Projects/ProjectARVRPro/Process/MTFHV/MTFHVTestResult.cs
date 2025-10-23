using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.Jsons.MTF2;

namespace ProjectARVRPro.Process.MTFHV
{
    public class MTFHVViewTestResult : MTFHVTestResult
    {
        public MTFDetailViewReslut MTFDetailViewReslut { get; set; }

    }

    public class MTFHVTestResult : ViewModelBase
    {

        /// <summary>
        /// MTF_HV_H ����_0F ������
        /// </summary>
        public ObjectiveTestItem MTF_HV_H_Center_0F { get; set; }

        /// <summary>
        /// MTF_HV_H ����_0.5F ������
        /// </summary>
        public ObjectiveTestItem MTF_HV_H_LeftUp_0_4F { get; set; }

        /// <summary>
        /// MTF_HV_H ����_0.5F ������
        /// </summary>
        public ObjectiveTestItem MTF_HV_H_RightUp_0_4F { get; set; }

        /// <summary>
        /// MTF_HV_H ����_0.5F ������
        /// </summary>
        public ObjectiveTestItem MTF_HV_H_RightDown_0_4F { get; set; }

        /// <summary>
        /// MTF_HV_H ����_0.5F ������
        /// </summary>
        public ObjectiveTestItem MTF_HV_H_LeftDown_0_4F { get; set; }

        /// <summary>
        /// MTF_HV_H ����_0.8F ������
        /// </summary>
        public ObjectiveTestItem MTF_HV_H_LeftUp_0_8F { get; set; }

        /// <summary>
        /// MTF_HV_H ����_0.8F ������
        /// </summary>
        public ObjectiveTestItem MTF_HV_H_RightUp_0_8F { get; set; }

        /// <summary>
        /// MTF_HV_H ����_0.8F ������
        /// </summary>
        public ObjectiveTestItem MTF_HV_H_RightDown_0_8F { get; set; }

        /// <summary>
        /// MTF_HV_H ����_0.8F ������
        /// </summary>
        public ObjectiveTestItem MTF_HV_H_LeftDown_0_8F { get; set; }

        /// <summary>
        /// MTF_HV_V ����_0F ������
        /// </summary>
        public ObjectiveTestItem MTF_HV_V_Center_0F { get; set; }

        /// <summary>
        /// MTF_HV_V ����_0.5F ������
        /// </summary>
        public ObjectiveTestItem MTF_HV_V_LeftUp_0_4F { get; set; }

        /// <summary>
        /// MTF_HV_V ����_0.5F ������
        /// </summary>
        public ObjectiveTestItem MTF_HV_V_RightUp_0_4F { get; set; }

        /// <summary>
        /// MTF_HV_V ����_0.5F ������
        /// </summary>
        public ObjectiveTestItem MTF_HV_V_RightDown_0_4F { get; set; }

        /// <summary>
        /// MTF_HV_V ����_0.5F ������
        /// </summary>
        public ObjectiveTestItem MTF_HV_V_LeftDown_0_4F { get; set; }

        /// <summary>
        /// MTF_HV_V ����_0.8F ������
        /// </summary>
        public ObjectiveTestItem MTF_HV_V_LeftUp_0_8F { get; set; }

        /// <summary>
        /// MTF_HV_V ����_0.8F ������
        /// </summary>
        public ObjectiveTestItem MTF_HV_V_RightUp_0_8F { get; set; }

        /// <summary>
        /// MTF_HV_V ����_0.8F ������
        /// </summary>
        public ObjectiveTestItem MTF_HV_V_RightDown_0_8F { get; set; }

        /// <summary>
        /// MTF_HV_V ����_0.8F ������
        /// </summary>
        public ObjectiveTestItem MTF_HV_V_LeftDown_0_8F { get; set; }
    }
}
