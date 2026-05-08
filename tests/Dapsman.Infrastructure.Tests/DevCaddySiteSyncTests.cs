using Dapsman.Domain;
using Dapsman.Infrastructure;

namespace Dapsman.Infrastructure.Tests;

public sealed class DevCaddySiteSyncTests
{
    [Fact]
    public void SyncDevSites_NoFiles_CreatesPlaceholder()
    {
        var root = CreateTempDirectory();
        var runtimeSites = Path.Combine(root, "caddy_sites");
        var plan = new CaddySyncPlan
        {
            RuntimeSitesPath = runtimeSites,
            FilesToCopy = Array.Empty<CaddySiteCopyPlan>(),
            ShouldCreatePlaceholder = true,
            PlaceholderFilePath = Path.Combine(runtimeSites, "000-empty.dev.caddy"),
        };

        var sync = new DevCaddySiteSync();
        sync.SyncDevSites(plan);

        Assert.True(File.Exists(plan.PlaceholderFilePath));
    }

    [Fact]
    public void SyncDevSites_WithFiles_CopiesFilesAndSkipsPlaceholder()
    {
        var root = CreateTempDirectory();
        var sourceDir = Path.Combine(root, "project", "_caddy_sites");
        var runtimeSites = Path.Combine(root, "caddy_sites");
        Directory.CreateDirectory(sourceDir);

        var sourceFile = Path.Combine(sourceDir, "site.dev.caddy");
        File.WriteAllText(sourceFile, "example.test\n");

        var plan = new CaddySyncPlan
        {
            RuntimeSitesPath = runtimeSites,
            FilesToCopy = new[]
            {
                new CaddySiteCopyPlan
                {
                    ProjectName = "project",
                    SourcePath = sourceFile,
                    DestinationPath = Path.Combine(runtimeSites, "site.dev.caddy"),
                },
            },
            ShouldCreatePlaceholder = false,
            PlaceholderFilePath = Path.Combine(runtimeSites, "000-empty.dev.caddy"),
        };

        var sync = new DevCaddySiteSync();
        sync.SyncDevSites(plan);

        Assert.True(File.Exists(Path.Combine(runtimeSites, "site.dev.caddy")));
        Assert.False(File.Exists(plan.PlaceholderFilePath));
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "dapsman-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
