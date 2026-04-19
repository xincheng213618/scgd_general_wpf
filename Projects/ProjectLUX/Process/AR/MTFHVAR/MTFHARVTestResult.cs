using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.Jsons.MTF2;

namespace ProjectLUX.Process.MTFHVAR
{
    public class MTFHVARViewTestResult : MTFHARVTestResult
    {
        public MTFDetailViewReslut MTFDetailViewReslut { get; set; }

    }

    public class MTFHARVTestResult : ViewModelBase
    {
     // 0F Center
        public ObjectiveTestItem MTF0F_Center_H1 { get; set; } = new ObjectiveTestItem() { Name = "P1_H1"};
        public ObjectiveTestItem MTF0F_Center_V1 { get; set; } = new ObjectiveTestItem() { Name = "P1_V1"};
        public ObjectiveTestItem MTF0F_Center_H2 { get; set; } = new ObjectiveTestItem() { Name = "P1_H2" };
        public ObjectiveTestItem MTF0F_Center_V2 { get; set; } = new ObjectiveTestItem() { Name = "P1_V2"};
        public ObjectiveTestItem MTF0F_Center_horizontal { get; set; } = new ObjectiveTestItem() { Name = "P1_horizontal" };
        public ObjectiveTestItem MTF0F_Center_Vertical { get; set; } = new ObjectiveTestItem() { Name = "P1_Vertical" };
        
        // 0.4F LeftUp
        public ObjectiveTestItem MTF0_4F_LeftUp_H1 { get; set; } = new ObjectiveTestItem() { Name = "P2_H1" };
        public ObjectiveTestItem MTF0_4F_LeftUp_V1 { get; set; } = new ObjectiveTestItem() { Name = "P2_V1" };
        public ObjectiveTestItem MTF0_4F_LeftUp_H2 { get; set; } = new ObjectiveTestItem() { Name = "P2_H2" };
        public ObjectiveTestItem MTF0_4F_LeftUp_V2 { get; set; } = new ObjectiveTestItem() { Name = "P2_V2" };
        public ObjectiveTestItem MTF0_4F_LeftUp_horizontal { get; set; } = new ObjectiveTestItem() { Name = "P2_horizontal" };
        public ObjectiveTestItem MTF0_4F_LeftUp_Vertical { get; set; } = new ObjectiveTestItem() { Name = "P2_Vertical" };
   
    // 0.4F RightUp
        public ObjectiveTestItem MTF0_4F_RightUp_H1 { get; set; } = new ObjectiveTestItem() { Name = "P3_H1" };
        public ObjectiveTestItem MTF0_4F_RightUp_V1 { get; set; } = new ObjectiveTestItem() { Name = "P3_V1" };
        public ObjectiveTestItem MTF0_4F_RightUp_H2 { get; set; } = new ObjectiveTestItem() { Name = "P3_H2" };
        public ObjectiveTestItem MTF0_4F_RightUp_V2 { get; set; } = new ObjectiveTestItem() { Name = "P3_V2" };
        public ObjectiveTestItem MTF0_4F_RightUp_horizontal { get; set; } = new ObjectiveTestItem() { Name = "P3_horizontal" };
        public ObjectiveTestItem MTF0_4F_RightUp_Vertical { get; set; } = new ObjectiveTestItem() { Name = "P3_Vertical" };
      
      // 0.4F RightDown
      public ObjectiveTestItem MTF0_4F_RightDown_H1 { get; set; } = new ObjectiveTestItem() { Name = "P4_H1" };
        public ObjectiveTestItem MTF0_4F_RightDown_V1 { get; set; } = new ObjectiveTestItem() { Name = "P4_V1" };
        public ObjectiveTestItem MTF0_4F_RightDown_H2 { get; set; } = new ObjectiveTestItem() { Name = "P4_H2" };
        public ObjectiveTestItem MTF0_4F_RightDown_V2 { get; set; } = new ObjectiveTestItem() { Name = "P4_V2" };
        public ObjectiveTestItem MTF0_4F_RightDown_horizontal { get; set; } = new ObjectiveTestItem() { Name = "P4_horizontal" };
        public ObjectiveTestItem MTF0_4F_RightDown_Vertical { get; set; } = new ObjectiveTestItem() { Name = "P4_Vertical" };

        // 0.4F LeftDown
        public ObjectiveTestItem MTF0_4F_LeftDown_H1 { get; set; } = new ObjectiveTestItem() { Name = "P5_H1" };
        public ObjectiveTestItem MTF0_4F_LeftDown_V1 { get; set; } = new ObjectiveTestItem() { Name = "P5_V1" };
        public ObjectiveTestItem MTF0_4F_LeftDown_H2 { get; set; } = new ObjectiveTestItem() { Name = "P5_H2" };
        public ObjectiveTestItem MTF0_4F_LeftDown_V2 { get; set; } = new ObjectiveTestItem() { Name = "P5_V2" };
        public ObjectiveTestItem MTF0_4F_LeftDown_horizontal { get; set; } = new ObjectiveTestItem() { Name = "P5_horizontal" };
        public ObjectiveTestItem MTF0_4F_LeftDown_Vertical { get; set; } = new ObjectiveTestItem() { Name = "P5_Vertical" };


        // 0.8F LeftUp
        public ObjectiveTestItem MTF0_8F_LeftUp_H1 { get; set; } = new ObjectiveTestItem() { Name = "P6_H1" };
        public ObjectiveTestItem MTF0_8F_LeftUp_V1 { get; set; } = new ObjectiveTestItem() { Name = "P6_V1" };
        public ObjectiveTestItem MTF0_8F_LeftUp_H2 { get; set; } = new ObjectiveTestItem() { Name = "P6_H2" };
        public ObjectiveTestItem MTF0_8F_LeftUp_V2 { get; set; } = new ObjectiveTestItem() { Name = "P6_V2" };
        public ObjectiveTestItem MTF0_8F_LeftUp_horizontal { get; set; } = new ObjectiveTestItem() { Name = "P6_horizontal" };
        public ObjectiveTestItem MTF0_8F_LeftUp_Vertical { get; set; } = new ObjectiveTestItem() { Name = "P6_Vertical" };

        // 0.8F RightUp
        public ObjectiveTestItem MTF0_8F_RightUp_H1 { get; set; } = new ObjectiveTestItem() { Name = "P7_H1" };
        public ObjectiveTestItem MTF0_8F_RightUp_V1 { get; set; } = new ObjectiveTestItem() { Name = "P7_V1" };
        public ObjectiveTestItem MTF0_8F_RightUp_H2 { get; set; } = new ObjectiveTestItem() { Name = "P7_H2" };
        public ObjectiveTestItem MTF0_8F_RightUp_V2 { get; set; } = new ObjectiveTestItem() { Name = "P7_V2" };
        public ObjectiveTestItem MTF0_8F_RightUp_horizontal { get; set; } = new ObjectiveTestItem() { Name = "P7_horizontal" };
        public ObjectiveTestItem MTF0_8F_RightUp_Vertical { get; set; } = new ObjectiveTestItem() { Name = "P7_Vertical" };

        // 0.8F RightDown
        public ObjectiveTestItem MTF0_8F_RightDown_H1 { get; set; } = new ObjectiveTestItem() { Name = "P8_H1" };
        public ObjectiveTestItem MTF0_8F_RightDown_V1 { get; set; } = new ObjectiveTestItem() { Name = "P8_V1" };
        public ObjectiveTestItem MTF0_8F_RightDown_H2 { get; set; } = new ObjectiveTestItem() { Name = "P8_H2" };
        public ObjectiveTestItem MTF0_8F_RightDown_V2 { get; set; } = new ObjectiveTestItem() { Name = "P8_V2" };
        public ObjectiveTestItem MTF0_8F_RightDown_horizontal { get; set; } = new ObjectiveTestItem() { Name = "P8_horizontal" };
        public ObjectiveTestItem MTF0_8F_RightDown_Vertical { get; set; } = new ObjectiveTestItem() { Name = "P8_Vertical" };

        // 0.8F LeftDown
        public ObjectiveTestItem MTF0_8F_LeftDown_H1 { get; set; } = new ObjectiveTestItem() { Name = "P9_H1" };
        public ObjectiveTestItem MTF0_8F_LeftDown_V1 { get; set; } = new ObjectiveTestItem() { Name = "P9_V1" };
        public ObjectiveTestItem MTF0_8F_LeftDown_H2 { get; set; } = new ObjectiveTestItem() { Name = "P9_H2" };
        public ObjectiveTestItem MTF0_8F_LeftDown_V2 { get; set; } = new ObjectiveTestItem() { Name = "P9_V2" };
        public ObjectiveTestItem MTF0_8F_LeftDown_horizontal { get; set; } = new ObjectiveTestItem() { Name = "P9_horizontal" };
        public ObjectiveTestItem MTF0_8F_LeftDown_Vertical { get; set; } = new ObjectiveTestItem() { Name = "P9_Vertical" };

    }
}
