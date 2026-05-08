using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure;

public sealed class DevCaddySiteSync : ICaddySiteSync
{
    public void SyncDevSites(CaddySyncPlan plan)
    {
        if (Directory.Exists(plan.RuntimeSitesPath))
        {
            Directory.Delete(plan.RuntimeSitesPath, recursive: true);
        }

        Directory.CreateDirectory(plan.RuntimeSitesPath);

        foreach (var file in plan.FilesToCopy)
        {
            File.Copy(file.SourcePath, file.DestinationPath, overwrite: true);
        }

        if (plan.ShouldCreatePlaceholder)
        {
            var content = "# placeholder file to keep Caddy import glob non-empty\n";
            File.WriteAllText(plan.PlaceholderFilePath, content);
        }
    }
}
