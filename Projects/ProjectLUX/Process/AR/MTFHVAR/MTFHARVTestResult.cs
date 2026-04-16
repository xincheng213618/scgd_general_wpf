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
        public ObjectiveTestItem MTF0F_Center_H1 { get; set; } = new ObjectiveTestItem() { Name = "Point-1_H1"};
        public ObjectiveTestItem MTF0F_Center_V1 { get; set; } = new ObjectiveTestItem() { Name = "Point-1_V1"};
        public ObjectiveTestItem MTF0F_Center_H2 { get; set; } = new ObjectiveTestItem() { Name = "Point-1_H2" };
        public ObjectiveTestItem MTF0F_Center_V2 { get; set; } = new ObjectiveTestItem() { Name = "Point-1_V2"};
        public ObjectiveTestItem MTF0F_Center_horizontal { get; set; } = new ObjectiveTestItem() { Name = "Point-1_horizontal" };
        public ObjectiveTestItem MTF0F_Center_Vertical { get; set; } = new ObjectiveTestItem() { Name = "Point-1_Vertical" };
        
        // 0.4F LeftUp
        public ObjectiveTestItem MTF0_4F_LeftUp_H1 { get; set; } = new ObjectiveTestItem() { Name = "Point-2_H1" };
        public ObjectiveTestItem MTF0_4F_LeftUp_V1 { get; set; } = new ObjectiveTestItem() { Name = "Point-2_V1" };
        public ObjectiveTestItem MTF0_4F_LeftUp_H2 { get; set; } = new ObjectiveTestItem() { Name = "Point-2_H2" };
        public ObjectiveTestItem MTF0_4F_LeftUp_V2 { get; set; } = new ObjectiveTestItem() { Name = "Point-2_V2" };
        public ObjectiveTestItem MTF0_4F_LeftUp_horizontal { get; set; } = new ObjectiveTestItem() { Name = "Point-2_horizontal" };
        public ObjectiveTestItem MTF0_4F_LeftUp_Vertical { get; set; } = new ObjectiveTestItem() { Name = "Point-2_Vertical" };
   
    // 0.4F RightUp
        public ObjectiveTestItem MTF0_4F_RightUp_H1 { get; set; } = new ObjectiveTestItem() { Name = "Point-3_H1" };
        public ObjectiveTestItem MTF0_4F_RightUp_V1 { get; set; } = new ObjectiveTestItem() { Name = "Point-3_V1" };
        public ObjectiveTestItem MTF0_4F_RightUp_H2 { get; set; } = new ObjectiveTestItem() { Name = "Point-3_H2" };
        public ObjectiveTestItem MTF0_4F_RightUp_V2 { get; set; } = new ObjectiveTestItem() { Name = "Point-3_V2" };
        public ObjectiveTestItem MTF0_4F_RightUp_horizontal { get; set; } = new ObjectiveTestItem() { Name = "Point-3_horizontal" };
        public ObjectiveTestItem MTF0_4F_RightUp_Vertical { get; set; } = new ObjectiveTestItem() { Name = "Point-3_Vertical" };
      
      // 0.4F RightDown
      public ObjectiveTestItem MTF0_4F_RightDown_H1 { get; set; } = new ObjectiveTestItem() { Name = "Point-4_H1" };
        public ObjectiveTestItem MTF0_4F_RightDown_V1 { get; set; } = new ObjectiveTestItem() { Name = "Point-4_V1" };
        public ObjectiveTestItem MTF0_4F_RightDown_H2 { get; set; } = new ObjectiveTestItem() { Name = "Point-4_H2" };
        public ObjectiveTestItem MTF0_4F_RightDown_V2 { get; set; } = new ObjectiveTestItem() { Name = "Point-4_V2" };
        public ObjectiveTestItem MTF0_4F_RightDown_horizontal { get; set; } = new ObjectiveTestItem() { Name = "Point-4_horizontal" };
        public ObjectiveTestItem MTF0_4F_RightDown_Vertical { get; set; } = new ObjectiveTestItem() { Name = "Point-4_Vertical" };

        // 0.4F LeftDown
        public ObjectiveTestItem MTF0_4F_LeftDown_H1 { get; set; } = new ObjectiveTestItem() { Name = "Point-5_H1" };
        public ObjectiveTestItem MTF0_4F_LeftDown_V1 { get; set; } = new ObjectiveTestItem() { Name = "Point-5_V1" };
        public ObjectiveTestItem MTF0_4F_LeftDown_H2 { get; set; } = new ObjectiveTestItem() { Name = "Point-5_H2" };
        public ObjectiveTestItem MTF0_4F_LeftDown_V2 { get; set; } = new ObjectiveTestItem() { Name = "Point-5_V2" };
        public ObjectiveTestItem MTF0_4F_LeftDown_horizontal { get; set; } = new ObjectiveTestItem() { Name = "Point-5_horizontal" };
        public ObjectiveTestItem MTF0_4F_LeftDown_Vertical { get; set; } = new ObjectiveTestItem() { Name = "Point-5_Vertical" };


        // 0.8F LeftUp
        public ObjectiveTestItem MTF0_8F_LeftUp_H1 { get; set; } = new ObjectiveTestItem() { Name = "Point-6_H1" };
        public ObjectiveTestItem MTF0_8F_LeftUp_V1 { get; set; } = new ObjectiveTestItem() { Name = "Point-6_V1" };
        public ObjectiveTestItem MTF0_8F_LeftUp_H2 { get; set; } = new ObjectiveTestItem() { Name = "Point-6_H2" };
        public ObjectiveTestItem MTF0_8F_LeftUp_V2 { get; set; } = new ObjectiveTestItem() { Name = "Point-6_V2" };
        public ObjectiveTestItem MTF0_8F_LeftUp_horizontal { get; set; } = new ObjectiveTestItem() { Name = "Point-6_horizontal" };
        public ObjectiveTestItem MTF0_8F_LeftUp_Vertical { get; set; } = new ObjectiveTestItem() { Name = "Point-6_Vertical" };

        // 0.8F RightUp
        public ObjectiveTestItem MTF0_8F_RightUp_H1 { get; set; } = new ObjectiveTestItem() { Name = "Point-7_H1" };
        public ObjectiveTestItem MTF0_8F_RightUp_V1 { get; set; } = new ObjectiveTestItem() { Name = "Point-7_V1" };
        public ObjectiveTestItem MTF0_8F_RightUp_H2 { get; set; } = new ObjectiveTestItem() { Name = "Point-7_H2" };
        public ObjectiveTestItem MTF0_8F_RightUp_V2 { get; set; } = new ObjectiveTestItem() { Name = "Point-7_V2" };
        public ObjectiveTestItem MTF0_8F_RightUp_horizontal { get; set; } = new ObjectiveTestItem() { Name = "Point-7_horizontal" };
        public ObjectiveTestItem MTF0_8F_RightUp_Vertical { get; set; } = new ObjectiveTestItem() { Name = "Point-7_Vertical" };

        // 0.8F RightDown
        public ObjectiveTestItem MTF0_8F_RightDown_H1 { get; set; } = new ObjectiveTestItem() { Name = "Point-8_H1" };
        public ObjectiveTestItem MTF0_8F_RightDown_V1 { get; set; } = new ObjectiveTestItem() { Name = "Point-8_V1" };
        public ObjectiveTestItem MTF0_8F_RightDown_H2 { get; set; } = new ObjectiveTestItem() { Name = "Point-8_H2" };
        public ObjectiveTestItem MTF0_8F_RightDown_V2 { get; set; } = new ObjectiveTestItem() { Name = "Point-8_V2" };
        public ObjectiveTestItem MTF0_8F_RightDown_horizontal { get; set; } = new ObjectiveTestItem() { Name = "Point-8_horizontal" };
        public ObjectiveTestItem MTF0_8F_RightDown_Vertical { get; set; } = new ObjectiveTestItem() { Name = "Point-8_Vertical" };

        // 0.8F LeftDown
        public ObjectiveTestItem MTF0_8F_LeftDown_H1 { get; set; } = new ObjectiveTestItem() { Name = "Point-9_H1" };
        public ObjectiveTestItem MTF0_8F_LeftDown_V1 { get; set; } = new ObjectiveTestItem() { Name = "Point-9_V1" };
        public ObjectiveTestItem MTF0_8F_LeftDown_H2 { get; set; } = new ObjectiveTestItem() { Name = "Point-9_H2" };
        public ObjectiveTestItem MTF0_8F_LeftDown_V2 { get; set; } = new ObjectiveTestItem() { Name = "Point-9_V2" };
        public ObjectiveTestItem MTF0_8F_LeftDown_horizontal { get; set; } = new ObjectiveTestItem() { Name = "Point-9_horizontal" };
        public ObjectiveTestItem MTF0_8F_LeftDown_Vertical { get; set; } = new ObjectiveTestItem() { Name = "Point-9_Vertical" };

    }
}
