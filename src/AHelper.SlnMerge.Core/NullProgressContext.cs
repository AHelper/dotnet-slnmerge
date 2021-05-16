namespace AHelper.SlnMerge.Core
{
    public class NullProgressContext : IProgressContext
    {
        public IProgressTask AddTask(string description) => new NullProgressTask();
    }
}
