using Dapsman.Application;
using Dapsman.Domain;
using Dapsman.Infrastructure;

namespace Dapsman.Infrastructure.Tests;

public sealed class LocalBuildPlanBuilderTests
{
	[Fact]
	public void BuildLocalPlan_NoProjectsConfigured_UsesDapsOnlyAndWarns()
	{
		var root = CreateTempDirectory();
		var dockerDir = Path.Combine(root, "docker");
		Directory.CreateDirectory(dockerDir);
		File.WriteAllText(Path.Combine(dockerDir, "compose_daps.yaml"), "services: {}\n");
		File.WriteAllText(Path.Combine(dockerDir, "compose_daps.dev.yaml"), "services: {}\n");

		var config = new DapsConfig
		{
			DapsRootPath = root,
			FullYamlPath = Path.Combine(".", "daps.yaml"),
			Providers = [],
			Projects = [],
		};

		var dockerResolver = new DockerResolver(config);
		var projectResolver = new ProjectResolver(config);
		var caddyResolver = new CaddyResolver(config, new FakeContainerManager(null));
		var builder = new LocalBuildPlanBuilder(config, dockerResolver, caddyResolver, projectResolver, new FakeHostPortManager());
		var plan = builder.BuildLocalPlan(new LocalBuildOptions());

		Assert.Equal(2, plan.DapsComposeFiles.Count);
		Assert.Empty(plan.ProjectComposePlans);
		Assert.Contains(plan.Warnings, w => w.Contains("No projects configured", StringComparison.OrdinalIgnoreCase));
		Assert.True(plan.CaddySync.ShouldCreatePlaceholder);
		Assert.Empty(plan.CaddySync.FilesToCopy);
	}

	[Fact]
	public void BuildLocalPlan_ProjectInSplitMode_UsesDevFileForLocal()
	{
		var root = CreateTempDirectory();
		var dockerDir = Path.Combine(root, "docker");
		Directory.CreateDirectory(dockerDir);
		File.WriteAllText(Path.Combine(dockerDir, "compose_daps.yaml"), "services: {}\n");
		File.WriteAllText(Path.Combine(dockerDir, "compose_daps.dev.yaml"), "services: {}\n");

		var projectRoot = Path.Combine(root, "projects", "j-shirt");
		var projectDocker = Path.Combine(projectRoot, "_docker");
		Directory.CreateDirectory(projectDocker);
		File.WriteAllText(Path.Combine(projectDocker, "compose_daps_jshirt.dev.yaml"), "services: {}\n");
		File.WriteAllText(Path.Combine(projectDocker, "compose_daps_jshirt.prod.yaml"), "services: {}\n");
		File.WriteAllText(Path.Combine(projectDocker, "compose_jshirt.dev.yaml"), "services: {}\n");
		File.WriteAllText(Path.Combine(projectDocker, "compose_jshirt.prod.yaml"), "services: {}\n");

		var config = new DapsConfig
		{
			DapsRootPath = root,
			FullYamlPath = Path.Combine(root, "daps.yaml"),
			Projects = new[]
			{
				new ProjectDefinition { Name = "j-shirt", Path = projectRoot },
			},
		};

		var dockerResolver = new DockerResolver(config);
		var caddyResolver = new CaddyResolver(config, new FakeContainerManager(null));
		var projectResolver = new ProjectResolver(config);
		var builder = new LocalBuildPlanBuilder(config, dockerResolver, caddyResolver, projectResolver, new FakeHostPortManager());
		var plan = builder.BuildLocalPlan(new LocalBuildOptions());

		Assert.Single(plan.ProjectComposePlans);
		Assert.Contains("compose_daps_jshirt.dev.yaml", plan.DapsComposeFiles[^1], StringComparison.OrdinalIgnoreCase);
		Assert.Contains("compose_jshirt.dev.yaml", plan.ProjectComposePlans[0].ComposeFiles[0], StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void BuildLocalPlan_ProjectInLayeredMode_UsesBaseAndDevForLocal()
	{
		var root = CreateTempDirectory();
		var dockerDir = Path.Combine(root, "docker");
		Directory.CreateDirectory(dockerDir);
		File.WriteAllText(Path.Combine(dockerDir, "compose_daps.yaml"), "services: {}\n");
		File.WriteAllText(Path.Combine(dockerDir, "compose_daps.dev.yaml"), "services: {}\n");

		var projectRoot = Path.Combine(root, "projects", "j-shirt");
		var projectDocker = Path.Combine(projectRoot, "_docker");
		Directory.CreateDirectory(projectDocker);
		File.WriteAllText(Path.Combine(projectDocker, "compose_daps_jshirt.yaml"), "services: {}\n");
		File.WriteAllText(Path.Combine(projectDocker, "compose_daps_jshirt.dev.yaml"), "services: {}\n");
		File.WriteAllText(Path.Combine(projectDocker, "compose_daps_jshirt.prod.yaml"), "services: {}\n");
		File.WriteAllText(Path.Combine(projectDocker, "compose_jshirt.yaml"), "services: {}\n");
		File.WriteAllText(Path.Combine(projectDocker, "compose_jshirt.dev.yaml"), "services: {}\n");
		File.WriteAllText(Path.Combine(projectDocker, "compose_jshirt.prod.yaml"), "services: {}\n");

		var config = new DapsConfig
		{
			DapsRootPath = root,
			FullYamlPath = Path.Combine(root, "daps.yaml"),
			Projects = new[]
			{
				new ProjectDefinition { Name = "j-shirt", Path = projectRoot },
			},
		};

		var dockerResolver = new DockerResolver(config);
		var caddyResolver = new CaddyResolver(config, new FakeContainerManager(null));
		var projectResolver = new ProjectResolver(config);
		var builder = new LocalBuildPlanBuilder(config, dockerResolver, caddyResolver, projectResolver, new FakeHostPortManager());
		var plan = builder.BuildLocalPlan(new LocalBuildOptions());

		Assert.Single(plan.ProjectComposePlans);
		Assert.Contains(plan.DapsComposeFiles, p => p.EndsWith("compose_daps_jshirt.yaml", StringComparison.OrdinalIgnoreCase));
		Assert.Contains(plan.DapsComposeFiles, p => p.EndsWith("compose_daps_jshirt.dev.yaml", StringComparison.OrdinalIgnoreCase));
		Assert.Contains(plan.ProjectComposePlans[0].ComposeFiles, p => p.EndsWith("compose_jshirt.yaml", StringComparison.OrdinalIgnoreCase));
		Assert.Contains(plan.ProjectComposePlans[0].ComposeFiles, p => p.EndsWith("compose_jshirt.dev.yaml", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public void BuildLocalPlan_BaseAndProdOnly_IsValidAndUsesBaseForLocal()
	{
		var root = CreateTempDirectory();
		var dockerDir = Path.Combine(root, "docker");
		Directory.CreateDirectory(dockerDir);
		File.WriteAllText(Path.Combine(dockerDir, "compose_daps.yaml"), "services: {}\n");
		File.WriteAllText(Path.Combine(dockerDir, "compose_daps.dev.yaml"), "services: {}\n");

		var projectRoot = Path.Combine(root, "projects", "j-shirt");
		var projectDocker = Path.Combine(projectRoot, "_docker");
		Directory.CreateDirectory(projectDocker);
		File.WriteAllText(Path.Combine(projectDocker, "compose_daps_jshirt.yaml"), "services: {}\n");
		File.WriteAllText(Path.Combine(projectDocker, "compose_daps_jshirt.prod.yaml"), "services: {}\n");
		File.WriteAllText(Path.Combine(projectDocker, "compose_jshirt.yaml"), "services: {}\n");
		File.WriteAllText(Path.Combine(projectDocker, "compose_jshirt.prod.yaml"), "services: {}\n");

		var config = new DapsConfig
		{
			DapsRootPath = root,
			FullYamlPath = Path.Combine(root, "daps.yaml"),
			Projects = new[]
			{
				new ProjectDefinition { Name = "j-shirt", Path = projectRoot },
			},
		};

		var dockerResolver = new DockerResolver(config);
		var caddyResolver = new CaddyResolver(config, new FakeContainerManager(null));
		var projectResolver = new ProjectResolver(config);
		var builder = new LocalBuildPlanBuilder(config, dockerResolver, caddyResolver, projectResolver, new FakeHostPortManager());
		var plan = builder.BuildLocalPlan(new LocalBuildOptions());

		Assert.Single(plan.ProjectComposePlans);
		Assert.Contains(plan.DapsComposeFiles, p => p.EndsWith("compose_daps_jshirt.yaml", StringComparison.OrdinalIgnoreCase));
		Assert.DoesNotContain(plan.DapsComposeFiles, p => p.EndsWith("compose_daps_jshirt.dev.yaml", StringComparison.OrdinalIgnoreCase));
		Assert.Contains(plan.ProjectComposePlans[0].ComposeFiles, p => p.EndsWith("compose_jshirt.yaml", StringComparison.OrdinalIgnoreCase));
		Assert.DoesNotContain(plan.ProjectComposePlans[0].ComposeFiles, p => p.EndsWith("compose_jshirt.dev.yaml", StringComparison.OrdinalIgnoreCase));
		Assert.True(plan.CaddySync.ShouldCreatePlaceholder);
	}

	[Fact]
	public void BuildLocalPlan_DapsLinkedDevOnlyWithoutShared_IsValidForLocalBuild()
	{
		var root = CreateTempDirectory();
		var dockerDir = Path.Combine(root, "docker");
		Directory.CreateDirectory(dockerDir);
		File.WriteAllText(Path.Combine(dockerDir, "compose_daps.yaml"), "services: {}\n");
		File.WriteAllText(Path.Combine(dockerDir, "compose_daps.dev.yaml"), "services: {}\n");

		var projectRoot = Path.Combine(root, "projects", "j-shirt");
		var projectDocker = Path.Combine(projectRoot, "_docker");
		Directory.CreateDirectory(projectDocker);
		File.WriteAllText(Path.Combine(projectDocker, "compose_daps_jshirt.dev.yaml"), "services: {}\n");
		File.WriteAllText(Path.Combine(projectDocker, "compose_jshirt.yaml"), "services: {}\n");

		var config = new DapsConfig
		{
			DapsRootPath = root,
			FullYamlPath = Path.Combine(root, "daps.yaml"),
			Projects = new[]
			{
				new ProjectDefinition { Name = "j-shirt", Path = projectRoot },
			},
		};

		var dockerResolver = new DockerResolver(config);
		var caddyResolver = new CaddyResolver(config, new FakeContainerManager(null));
		var projectResolver = new ProjectResolver(config);
		var builder = new LocalBuildPlanBuilder(config, dockerResolver, caddyResolver, projectResolver, new FakeHostPortManager());
		var plan = builder.BuildLocalPlan(new LocalBuildOptions());

		Assert.Contains(plan.DapsComposeFiles, p => p.EndsWith("compose_daps_jshirt.dev.yaml", StringComparison.OrdinalIgnoreCase));
		Assert.Single(plan.ProjectComposePlans);
		Assert.Contains(plan.ProjectComposePlans[0].ComposeFiles, p => p.EndsWith("compose_jshirt.yaml", StringComparison.OrdinalIgnoreCase));
	}

	private static string CreateTempDirectory()
	{
		var path = Path.Combine(Path.GetTempPath(), "dapsman-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(path);
		return path;
	}
}
