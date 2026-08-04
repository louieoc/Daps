using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure.Tests;

/// <summary>
/// A freshly cloned Daps has no containers at all. Resolving the toolkit and caddy containers must
/// still succeed so that init and local build — the workflows that create them — can run.
/// </summary>
public sealed class FreshWorkstationResolverTests
{
	[Fact]
	public void ToolkitResolver_NoContainersRunning_FallsBackToConventionalName()
	{
		var toolkit = new ToolkitResolver(CreateConfig(), new FakeContainerManager([])).Resolve();

		Assert.Equal("daps-toolkit-1", toolkit.ContainerName);
		Assert.False(toolkit.IsRunning);
	}

	[Fact]
	public void ToolkitResolver_ContainerRunning_ReportsRunning()
	{
		var toolkit = new ToolkitResolver(CreateConfig(), new FakeContainerManager(null)).Resolve();

		Assert.Equal("daps-toolkit-1", toolkit.ContainerName);
		Assert.True(toolkit.IsRunning);
	}

	[Fact]
	public void CaddyResolver_NoContainersRunning_FallsBackToConventionalName()
	{
		var caddy = new CaddyResolver(CreateConfig(), new FakeContainerManager([])).Resolve();

		Assert.Equal("daps-caddy-1", caddy.ContainerName);
		Assert.False(caddy.IsRunning);
	}

	[Fact]
	public void CaddyResolver_ContainerRunning_ReportsRunning()
	{
		var caddy = new CaddyResolver(CreateConfig(), new FakeContainerManager(null)).Resolve();

		Assert.Equal("daps-caddy-1", caddy.ContainerName);
		Assert.True(caddy.IsRunning);
	}

	[Fact]
	public void LocalCaddyRestartPlan_NoContainersRunning_TargetsTheContainerLocalBuildWillCreate()
	{
		var config = CreateConfig();
		var containerManager = new FakeContainerManager([]);

		var plan = new LocalCaddyRestartPlanBuilder(config, new CaddyResolver(config, containerManager)).BuildPlan();

		Assert.Equal("docker exec daps-caddy-1 caddy reload --config /etc/caddy/Caddyfile", plan.ReloadCommand);
	}

	[Fact]
	public void LocalBuildPlan_NoContainersRunning_BuildsWithoutError()
	{
		var root = CreateDapsRoot();
		var config = CreateConfig(root);
		var containerManager = new FakeContainerManager([]);

		var plan = new LocalBuildPlanBuilder(
			config,
			new DockerResolver(config),
			new CaddyResolver(config, containerManager),
			new ProjectResolver(config),
			new FakeHostPortManager()).BuildLocalPlan(new LocalBuildOptions());

		Assert.NotEmpty(plan.DapsComposeFiles);
		Assert.Equal("daps_net", plan.SharedNetworkName);
		Assert.Equal(Path.Combine(root, "caddy_sites"), plan.CaddySync.RuntimeSitesPath);
		Assert.False(plan.HasProjectsConfigured);
	}

	private static DapsConfig CreateConfig(string? root = null)
	{
		root ??= Path.Combine(Path.GetTempPath(), "dapsman-tests", Guid.NewGuid().ToString("N"));

		return new DapsConfig
		{
			DapsRootPath = root,
			FullYamlPath = Path.Combine(root, "daps.yaml"),
			Projects = Array.Empty<ProjectDefinition>(),
		};
	}

	private static string CreateDapsRoot()
	{
		var root = Path.Combine(Path.GetTempPath(), "dapsman-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Path.Combine(root, "caddy"));
		Directory.CreateDirectory(Path.Combine(root, "caddy_sites"));
		Directory.CreateDirectory(Path.Combine(root, "docker"));
		File.WriteAllText(Path.Combine(root, "caddy", "Caddyfile"), "import /etc/caddy/sites/*.caddy");
		File.WriteAllText(Path.Combine(root, "docker", "compose_daps.yaml"), "services: {}");
		File.WriteAllText(Path.Combine(root, "docker", "compose_daps.dev.yaml"), "services: {}");
		return root;
	}
}
