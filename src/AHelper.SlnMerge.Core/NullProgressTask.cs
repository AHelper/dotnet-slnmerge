namespace AHelper.SlnMerge.Core
{
    public class NullProgressTask : IProgressTask
    {
        public void Increment(double value)
        {
        }

        public void StopTask()
        {
        }
    }
}
