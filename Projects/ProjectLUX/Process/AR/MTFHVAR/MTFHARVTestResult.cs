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
        public ObjectiveTestItem MTF0F_Center_H1 { get; set; } = new ObjectiveTestItem() { Name = "0F_Center_H1", Unit = "%" };
        public ObjectiveTestItem MTF0F_Center_V1 { get; set; } = new ObjectiveTestItem() { Name = "0F_Center_V1", Unit = "%" };
        public ObjectiveTestItem MTF0F_Center_H2 { get; set; } = new ObjectiveTestItem() { Name = "0F_Center_H2", Unit = "%" };
        public ObjectiveTestItem MTF0F_Center_V2 { get; set; } = new ObjectiveTestItem() { Name = "0F_Center_V2", Unit = "%" };
        public ObjectiveTestItem MTF0F_Center_horizontal { get; set; } = new ObjectiveTestItem() { Name = "0F_Center_horizontal", Unit = "%" };
        public ObjectiveTestItem MTF0F_Center_Vertical { get; set; } = new ObjectiveTestItem() { Name = "0F_Center_Vertical", Unit = "%" };
        
        // 0.4F LeftUp
        public ObjectiveTestItem MTF0_4F_LeftUp_H1 { get; set; } = new ObjectiveTestItem() { Name = "0.4F_LeftUp_H1", Unit = "%" };
        public ObjectiveTestItem MTF0_4F_LeftUp_V1 { get; set; } = new ObjectiveTestItem() { Name = "0.4F_LeftUp_V1", Unit = "%" };
        public ObjectiveTestItem MTF0_4F_LeftUp_H2 { get; set; } = new ObjectiveTestItem() { Name = "0.4F_LeftUp_H2", Unit = "%" };
        public ObjectiveTestItem MTF0_4F_LeftUp_V2 { get; set; } = new ObjectiveTestItem() { Name = "0.4F_LeftUp_V2", Unit = "%" };
        public ObjectiveTestItem MTF0_4F_LeftUp_horizontal { get; set; } = new ObjectiveTestItem() { Name = "0.4F_LeftUp_horizontal", Unit = "%" };
        public ObjectiveTestItem MTF0_4F_LeftUp_Vertical { get; set; } = new ObjectiveTestItem() { Name = "0.4F_LeftUp_Vertical", Unit = "%" };
   
    // 0.4F RightUp
        public ObjectiveTestItem MTF0_4F_RightUp_H1 { get; set; } = new ObjectiveTestItem() { Name = "0.4F_RightUp_H1", Unit = "%" };
        public ObjectiveTestItem MTF0_4F_RightUp_V1 { get; set; } = new ObjectiveTestItem() { Name = "0.4F_RightUp_V1", Unit = "%" };
        public ObjectiveTestItem MTF0_4F_RightUp_H2 { get; set; } = new ObjectiveTestItem() { Name = "0.4F_RightUp_H2", Unit = "%" };
        public ObjectiveTestItem MTF0_4F_RightUp_V2 { get; set; } = new ObjectiveTestItem() { Name = "0.4F_RightUp_V2", Unit = "%" };
        public ObjectiveTestItem MTF0_4F_RightUp_horizontal { get; set; } = new ObjectiveTestItem() { Name = "0.4F_RightUp_horizontal", Unit = "%" };
        public ObjectiveTestItem MTF0_4F_RightUp_Vertical { get; set; } = new ObjectiveTestItem() { Name = "0.4F_RightUp_Vertical", Unit = "%" };
      
      // 0.4F RightDown
      public ObjectiveTestItem MTF0_4F_RightDown_H1 { get; set; } = new ObjectiveTestItem() { Name = "0.4F_RightDown_H1", Unit = "%" };
        public ObjectiveTestItem MTF0_4F_RightDown_V1 { get; set; } = new ObjectiveTestItem() { Name = "0.4F_RightDown_V1", Unit = "%" };
        public ObjectiveTestItem MTF0_4F_RightDown_H2 { get; set; } = new ObjectiveTestItem() { Name = "0.4F_RightDown_H2", Unit = "%" };
        public ObjectiveTestItem MTF0_4F_RightDown_V2 { get; set; } = new ObjectiveTestItem() { Name = "0.4F_RightDown_V2", Unit = "%" };
        public ObjectiveTestItem MTF0_4F_RightDown_horizontal { get; set; } = new ObjectiveTestItem() { Name = "0.4F_RightDown_horizontal", Unit = "%" };
        public ObjectiveTestItem MTF0_4F_RightDown_Vertical { get; set; } = new ObjectiveTestItem() { Name = "0.4F_RightDown_Vertical", Unit = "%" };
        
        // 0.4F LeftDown
      public ObjectiveTestItem MTF0_4F_LeftDown_H1 { get; set; } = new ObjectiveTestItem() { Name = "0.4F_LeftDown_H1", Unit = "%" };
     public ObjectiveTestItem MTF0_4F_LeftDown_V1 { get; set; } = new ObjectiveTestItem() { Name = "0.4F_LeftDown_V1", Unit = "%" };
    public ObjectiveTestItem MTF0_4F_LeftDown_H2 { get; set; } = new ObjectiveTestItem() { Name = "0.4F_LeftDown_H2", Unit = "%" };
     public ObjectiveTestItem MTF0_4F_LeftDown_V2 { get; set; } = new ObjectiveTestItem() { Name = "0.4F_LeftDown_V2", Unit = "%" };
        public ObjectiveTestItem MTF0_4F_LeftDown_horizontal { get; set; } = new ObjectiveTestItem() { Name = "0.4F_LeftDown_horizontal", Unit = "%" };
public ObjectiveTestItem MTF0_4F_LeftDown_Vertical { get; set; } = new ObjectiveTestItem() { Name = "0.4F_LeftDown_Vertical", Unit = "%" };
        
        // 0.8F LeftUp
        public ObjectiveTestItem MTF0_8F_LeftUp_H1 { get; set; } = new ObjectiveTestItem() { Name = "0.8F_LeftUp_H1", Unit = "%" };
        public ObjectiveTestItem MTF0_8F_LeftUp_V1 { get; set; } = new ObjectiveTestItem() { Name = "0.8F_LeftUp_V1", Unit = "%" };
        public ObjectiveTestItem MTF0_8F_LeftUp_H2 { get; set; } = new ObjectiveTestItem() { Name = "0.8F_LeftUp_H2", Unit = "%" };
    public ObjectiveTestItem MTF0_8F_LeftUp_V2 { get; set; } = new ObjectiveTestItem() { Name = "0.8F_LeftUp_V2", Unit = "%" };
        public ObjectiveTestItem MTF0_8F_LeftUp_horizontal { get; set; } = new ObjectiveTestItem() { Name = "0.8F_LeftUp_horizontal", Unit = "%" };
        public ObjectiveTestItem MTF0_8F_LeftUp_Vertical { get; set; } = new ObjectiveTestItem() { Name = "0.8F_LeftUp_Vertical", Unit = "%" };
  
    // 0.8F RightUp
        public ObjectiveTestItem MTF0_8F_RightUp_H1 { get; set; } = new ObjectiveTestItem() { Name = "0.8F_RightUp_H1", Unit = "%" };
        public ObjectiveTestItem MTF0_8F_RightUp_V1 { get; set; } = new ObjectiveTestItem() { Name = "0.8F_RightUp_V1", Unit = "%" };
        public ObjectiveTestItem MTF0_8F_RightUp_H2 { get; set; } = new ObjectiveTestItem() { Name = "0.8F_RightUp_H2", Unit = "%" };
        public ObjectiveTestItem MTF0_8F_RightUp_V2 { get; set; } = new ObjectiveTestItem() { Name = "0.8F_RightUp_V2", Unit = "%" };
        public ObjectiveTestItem MTF0_8F_RightUp_horizontal { get; set; } = new ObjectiveTestItem() { Name = "0.8F_RightUp_horizontal", Unit = "%" };
        public ObjectiveTestItem MTF0_8F_RightUp_Vertical { get; set; } = new ObjectiveTestItem() { Name = "0.8F_RightUp_Vertical", Unit = "%" };
  
        // 0.8F RightDown
        public ObjectiveTestItem MTF0_8F_RightDown_H1 { get; set; } = new ObjectiveTestItem() { Name = "0.8F_RightDown_H1", Unit = "%" };
        public ObjectiveTestItem MTF0_8F_RightDown_V1 { get; set; } = new ObjectiveTestItem() { Name = "0.8F_RightDown_V1", Unit = "%" };
public ObjectiveTestItem MTF0_8F_RightDown_H2 { get; set; } = new ObjectiveTestItem() { Name = "0.8F_RightDown_H2", Unit = "%" };
        public ObjectiveTestItem MTF0_8F_RightDown_V2 { get; set; } = new ObjectiveTestItem() { Name = "0.8F_RightDown_V2", Unit = "%" };
      public ObjectiveTestItem MTF0_8F_RightDown_horizontal { get; set; } = new ObjectiveTestItem() { Name = "0.8F_RightDown_horizontal", Unit = "%" };
        public ObjectiveTestItem MTF0_8F_RightDown_Vertical { get; set; } = new ObjectiveTestItem() { Name = "0.8F_RightDown_Vertical", Unit = "%" };
        
        // 0.8F LeftDown
        public ObjectiveTestItem MTF0_8F_LeftDown_H1 { get; set; } = new ObjectiveTestItem() { Name = "0.8F_LeftDown_H1", Unit = "%" };
        public ObjectiveTestItem MTF0_8F_LeftDown_V1 { get; set; } = new ObjectiveTestItem() { Name = "0.8F_LeftDown_V1", Unit = "%" };
        public ObjectiveTestItem MTF0_8F_LeftDown_H2 { get; set; } = new ObjectiveTestItem() { Name = "0.8F_LeftDown_H2", Unit = "%" };
        public ObjectiveTestItem MTF0_8F_LeftDown_V2 { get; set; } = new ObjectiveTestItem() { Name = "0.8F_LeftDown_V2", Unit = "%" };
   public ObjectiveTestItem MTF0_8F_LeftDown_horizontal { get; set; } = new ObjectiveTestItem() { Name = "0.8F_LeftDown_horizontal", Unit = "%" };
      public ObjectiveTestItem MTF0_8F_LeftDown_Vertical { get; set; } = new ObjectiveTestItem() { Name = "0.8F_LeftDown_Vertical", Unit = "%" };
    }
}
