using Dapsman.Domain;

namespace Dapsman.Infrastructure.Tests;

public sealed class LocalCaddySiteSyncTests
{
	[Fact]
	public void SyncDevSites_NoFiles_CreatesPlaceholder()
	{
		var root = CreateTempDirectory();
		var runtimeSites = Path.Combine(root, "caddy_sites");
		var plan = new LocalCaddySyncPlan
		{
			RuntimeSitesPath = runtimeSites,
			FilesToCopy = Array.Empty<LocalCaddySiteCopyPlan>(),
			ShouldCreatePlaceholder = true,
			PlaceholderFilePath = Path.Combine(runtimeSites, "000-empty.dev.caddy"),
		};

		var sync = new LocalCaddySiteSync();
		sync.SyncLocalSites(plan);

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

		var plan = new LocalCaddySyncPlan
		{
			RuntimeSitesPath = runtimeSites,
			FilesToCopy = new[]
			{
				new LocalCaddySiteCopyPlan
				{
					ProjectName = "project",
					SourcePath = sourceFile,
					DestinationPath = Path.Combine(runtimeSites, "site.dev.caddy"),
				},
			},
			ShouldCreatePlaceholder = false,
			PlaceholderFilePath = Path.Combine(runtimeSites, "000-empty.dev.caddy"),
		};

		var sync = new LocalCaddySiteSync();
		sync.SyncLocalSites(plan);

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
