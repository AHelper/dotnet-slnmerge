using System.Threading.Tasks;
using Microsoft.Build.Locator;

namespace AHelper.SlnMerge
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            MSBuildLocator.RegisterDefaults();
            var workspace = await Workspace.CreateAsync(args);

            await workspace.AddReferencesAsync();
            await workspace.PopulateSolutionsAsync();
            await workspace.CommitChangesAsync(false);
        }
    }
}
