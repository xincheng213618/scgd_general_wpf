using System;

namespace ColorVision.Engine.Batch.Examples
{
    /// <summary>
    /// Example: Simple batch process with basic metadata
    /// 示例：带有基本元数据的简单批处理
    /// </summary>
    [BatchProcess("示例处理", "这是一个示例批处理，用于演示元数据的使用")]
    public class ExampleBatchProcess : IBatchProcess
    {
        public bool Process(IBatchContext ctx)
        {
            if (ctx?.Batch == null)
                return false;

            try
            {
                // 示例：处理逻辑
                Console.WriteLine($"Processing batch: {ctx.Batch.Id}");
                
                // 在这里添加您的处理代码
                // Add your processing code here
                
                return true;
            }
            catch (Exception ex)
            {
                // 记录错误
                Console.WriteLine($"Error: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Example: Advanced batch process with all metadata properties
    /// 示例：带有完整元数据属性的高级批处理
    /// </summary>
    [BatchProcess(
        DisplayName = "高级数据导出",
        Description = "执行复杂的数据转换并导出到多种格式",
        Category = "数据导出",
        Order = 10
    )]
    public class AdvancedExportProcess : IBatchProcess
    {
        public bool Process(IBatchContext ctx)
        {
            if (ctx?.Batch == null)
                return false;

            try
            {
                // 示例：高级处理逻辑
                Console.WriteLine($"Advanced processing for batch: {ctx.Batch.Id}");
                
                // 步骤 1: 数据验证
                // Step 1: Data validation
                
                // 步骤 2: 数据转换
                // Step 2: Data transformation
                
                // 步骤 3: 导出结果
                // Step 3: Export results
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in advanced processing: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Example: Batch process without metadata (backward compatible)
    /// 示例：不带元数据的批处理（向后兼容）
    /// </summary>
    public class LegacyBatchProcess : IBatchProcess
    {
        // 注意：没有 BatchProcessAttribute
        // 仍然可以工作，但会使用类名 "LegacyBatchProcess" 作为显示名称
        // Note: No BatchProcessAttribute
        // Still works, but will use class name "LegacyBatchProcess" as display name
        
        public bool Process(IBatchContext ctx)
        {
            if (ctx?.Batch == null)
                return false;

            Console.WriteLine("Legacy processing without metadata");
            return true;
        }
    }
}
