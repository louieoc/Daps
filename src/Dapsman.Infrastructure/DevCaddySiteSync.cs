using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure;

public sealed class DevCaddySiteSync : ICaddySiteSync
{
    public void SyncDevSites(CaddySyncPlan plan)
    {
        Directory.CreateDirectory(plan.RuntimeSitesPath);

        foreach (var file in plan.FilesToCopy)
        {
            File.Copy(file.SourcePath, file.DestinationPath, overwrite: true);
        }

        if (plan.ShouldCreatePlaceholder)
        {
            var hasAnyCaddyFiles = Directory.EnumerateFiles(plan.RuntimeSitesPath, "*.caddy").Any();
            if (!hasAnyCaddyFiles)
            {
                var content = "# placeholder file to keep Caddy import glob non-empty\n";
                File.WriteAllText(plan.PlaceholderFilePath, content);
            }
        }
    }
}
