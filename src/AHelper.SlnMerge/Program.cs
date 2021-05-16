using System.Threading.Tasks;
using AHelper.SlnMerge.Core;

namespace AHelper.SlnMerge
{
    internal class Program
    {
        static Task Main(string[] args)
            => new Runner(new SpectreConsoleOutputWriter()).RunAsync(args);
    }
}
