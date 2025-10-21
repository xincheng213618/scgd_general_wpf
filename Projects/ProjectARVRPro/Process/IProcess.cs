namespace ProjectARVRPro.Process
{
    public interface IProcess
    {
        public bool Execute(IProcessExecutionContext ctx);

        public void Render(IProcessExecutionContext ctx);

        public string GenText(IProcessExecutionContext ctx);

    }
}
