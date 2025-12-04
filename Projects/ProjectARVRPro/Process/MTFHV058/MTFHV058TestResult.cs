using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.Jsons.MTF2;

namespace ProjectARVRPro.Process.MTFHV058
{
    public class MTFHV058ViewTestResult : MTFHV058TestResult
    {
        public MTFDetailViewReslut MTFDetailViewReslut { get; set; }

    }

    public class MTFHV058TestResult : ViewModelBase
    {

        /// <summary>
        /// MTF058_H 中心_0F 水平方向
        /// </summary>
        public ObjectiveTestItem MTF058_H_Center_0F { get; set; } = new ObjectiveTestItem() { Name = "MTF058_H_Center_0F", Unit = "%" };
        /// <summary>
        /// MTF058_V 中心_0F 垂直方向
        /// </summary>
        public ObjectiveTestItem MTF058_V_Center_0F { get; set; } = new ObjectiveTestItem() { Name = "MTF058_V_Center_0F", Unit = "%" };

        /// <summary>
        /// MTF058_H 左上_0.3F 水平方向
        /// </summary>
        public ObjectiveTestItem MTF058_H_LeftUp_0_3F { get; set; } = new ObjectiveTestItem() { Name = "MTF058_H_LeftUp_0_3F", Unit = "%" };


        /// <summary>
        /// MTF058_V 左上_0.3F 垂直方向
        /// </summary>
        public ObjectiveTestItem MTF058_V_LeftUp_0_3F { get; set; } = new ObjectiveTestItem() { Name = "MTF058_V_LeftUp_0_3F", Unit = "%" };

        /// <summary>
        /// MTF058_H 右上_0.3F 水平方向
        /// </summary>
        public ObjectiveTestItem MTF058_H_RightUp_0_3F { get; set; } = new ObjectiveTestItem() { Name = "MTF058_H_RightUp_0_3F", Unit = "%" };

        /// <summary>
        /// MTF058_V 右上_0.3F 垂直方向
        /// </summary>
        public ObjectiveTestItem MTF058_V_RightUp_0_3F { get; set; } = new ObjectiveTestItem() { Name = "MTF058_V_RightUp_0_3F", Unit = "%" };


        /// <summary>
        /// MTF058_H 右下_0.3F 水平方向
        /// </summary>
        public ObjectiveTestItem MTF058_H_RightDown_0_3F { get; set; } = new ObjectiveTestItem() { Name = "MTF058_H_RightDown_0_3F", Unit = "%" };

        /// <summary>
        /// MTF058_V 右下_0.3F 垂直方向
        /// </summary>
        public ObjectiveTestItem MTF058_V_RightDown_0_3F { get; set; } = new ObjectiveTestItem() { Name = "MTF058_V_RightDown_0_3F", Unit = "%" };

        /// <summary>
        /// MTF058_H 左下_0.3F 水平方向
        /// </summary>
        public ObjectiveTestItem MTF058_H_LeftDown_0_3F { get; set; } = new ObjectiveTestItem() { Name = "MTF058_H_LeftDown_0_3F", Unit = "%" };


        /// <summary>
        /// MTF058_V 左下_0.3F 垂直方向
        /// </summary>
        public ObjectiveTestItem MTF058_V_LeftDown_0_3F { get; set; } = new ObjectiveTestItem() { Name = "MTF058_V_LeftDown_0_3F", Unit = "%" };

        /// <summary>
        /// MTF058_H 左上_0.6F 水平方向
        /// </summary>
        public ObjectiveTestItem MTF058_H_LeftUp_0_6F { get; set; } = new ObjectiveTestItem() { Name = "MTF058_H_LeftUp_0_6F", Unit = "%" };

        /// <summary>
        /// MTF058_V 左上_0.6F 垂直方向
        /// </summary>
        public ObjectiveTestItem MTF058_V_LeftUp_0_6F { get; set; } = new ObjectiveTestItem() { Name = "MTF058_V_LeftUp_0_6F", Unit = "%" };

        /// <summary>
        /// MTF058_H 右上_0.6F 水平方向
        /// </summary>
        public ObjectiveTestItem MTF058_H_RightUp_0_6F { get; set; } = new ObjectiveTestItem() { Name = "MTF058_H_RightUp_0_6F", Unit = "%" };

        /// <summary>
        /// MTF058_V 右上_0.6F 垂直方向
        /// </summary>
        public ObjectiveTestItem MTF058_V_RightUp_0_6F { get; set; } = new ObjectiveTestItem() { Name = "MTF058_V_RightUp_0_6F", Unit = "%" };

        /// <summary>
        /// MTF058_H 右下_0.6F 水平方向
        /// </summary>
        public ObjectiveTestItem MTF058_H_RightDown_0_6F { get; set; } = new ObjectiveTestItem() { Name = "MTF058_H_RightDown_0_6F", Unit = "%" };

        /// <summary>
        /// MTF058_V 右下_0.6F 垂直方向
        /// </summary>
        public ObjectiveTestItem MTF058_V_RightDown_0_6F { get; set; } = new ObjectiveTestItem() { Name = "MTF058_V_RightDown_0_6F", Unit = "%" };

        /// <summary>
        /// MTF058_H 左下_0.6F 水平方向
        /// </summary>
        public ObjectiveTestItem MTF058_H_LeftDown_0_6F { get; set; } = new ObjectiveTestItem() { Name = "MTF058_H_LeftDown_0_6F", Unit = "%" };



        /// <summary>
        /// MTF058_V 左下_0.6F 垂直方向
        /// </summary>
        public ObjectiveTestItem MTF058_V_LeftDown_0_6F { get; set; } = new ObjectiveTestItem() { Name = "MTF058_V_LeftDown_0_6F", Unit = "%" };

        /// <summary>
        /// MTF058_H 左上_0.8F 水平方向
        /// </summary>
        public ObjectiveTestItem MTF058_H_LeftUp_0_8F { get; set; } = new ObjectiveTestItem() { Name = "MTF058_H_LeftUp_0_8F", Unit = "%" };

        /// <summary>
        /// MTF058_V 左上_0.8F 垂直方向
        /// </summary>
        public ObjectiveTestItem MTF058_V_LeftUp_0_8F { get; set; } = new ObjectiveTestItem() { Name = "MTF058_V_LeftUp_0_8F", Unit = "%" };

        /// <summary>
        /// MTF058_H 右上_0.8F 水平方向
        /// </summary>
        public ObjectiveTestItem MTF058_H_RightUp_0_8F { get; set; } = new ObjectiveTestItem() { Name = "MTF058_H_RightUp_0_8F", Unit = "%" };


        /// <summary>
        /// MTF058_V 右上_0.8F 垂直方向
        /// </summary>
        public ObjectiveTestItem MTF058_V_RightUp_0_8F { get; set; } = new ObjectiveTestItem() { Name = "MTF058_V_RightUp_0_8F", Unit = "%" };

        /// <summary>
        /// MTF058_H 右下_0.8F 水平方向
        /// </summary>
        public ObjectiveTestItem MTF058_H_RightDown_0_8F { get; set; } = new ObjectiveTestItem() { Name = "MTF058_H_RightDown_0_8F", Unit = "%" };


        /// <summary>
        /// MTF058_V 右下_0.8F 垂直方向
        /// </summary>
        public ObjectiveTestItem MTF058_V_RightDown_0_8F { get; set; } = new ObjectiveTestItem() { Name = "MTF058_V_RightDown_0_8F", Unit = "%" };

        /// <summary>
        /// MTF058_H 左下_0.8F 水平方向
        /// </summary>
        public ObjectiveTestItem MTF058_H_LeftDown_0_8F { get; set; } = new ObjectiveTestItem() { Name = "MTF058_H_LeftDown_0_8F", Unit = "%" };
        /// <summary>
        /// MTF058_V 左下_0.8F 垂直方向
        /// </summary>
        public ObjectiveTestItem MTF058_V_LeftDown_0_8F { get; set; } = new ObjectiveTestItem() { Name = "MTF058_V_LeftDown_0_8F", Unit = "%" };

    }

}
