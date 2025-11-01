namespace ColorVision.Engine.Batch
{
    public interface IBatchProcess
    {
        /// <summary>
        /// Processes the specified batch context and returns a value indicating whether the operation was successful.
        /// </summary>
        /// <param name="ctx">The batch context to process. Cannot be null.</param>
        /// <returns>true if the batch was processed successfully; otherwise, false.</returns>
        public bool Process(IBatchContext ctx);
    }

}
