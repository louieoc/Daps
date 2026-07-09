using Dapsman.Domain;

namespace Dapsman.Infrastructure.Tests;

public sealed class DockerResolver_IntegrationTests
{
	private static (DapsConfig config, DapsProject project) BuildWordpressContext()
	{
		var dapsPath = "../../../../..";
		var wordpressPath = Path.Combine(dapsPath, "templates/wordpress");
		var projectDef = new ProjectDefinition { Name = "mywpsite", Path = wordpressPath };
		var config = new DapsConfig
		{
			DapsRootPath = dapsPath,
			FullYamlPath = Path.Combine(dapsPath, "daps.yaml"),
			Projects = [projectDef],
		};
		var project = new ProjectResolver(config).Resolve("mywpsite")!;
		return (config, project);
	}

	[Fact]
	[Trait("Category", "Integration")]
	public void ResolveForProject_LocalProjectComposeFiles_ContainsSharedAndDevFiles()
	{
		var (config, project) = BuildWordpressContext();
		var resolver = new DockerResolver(config);

		var def = resolver.ResolveForProject(project);

		// Should contain both the shared base compose file AND the dev overlay.
		// This confirms LocalProjectComposeFiles is NOT dev-only — the .Where filter
		// in LocalBuildPlanBuilder is load-bearing, not redundant.
		Assert.Contains(def.LocalProjectComposeFiles, f => f.EndsWith("compose_mywpsite.yaml", StringComparison.OrdinalIgnoreCase));
		Assert.Contains(def.LocalProjectComposeFiles, f => f.EndsWith("compose_mywpsite.dev.yaml", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	[Trait("Category", "Integration")]
	public void ResolveForProject_LocalProjectComposeFiles_DoesNotContainProdFile()
	{
		var (config, project) = BuildWordpressContext();
		var resolver = new DockerResolver(config);

		var def = resolver.ResolveForProject(project);

		Assert.DoesNotContain(def.LocalProjectComposeFiles, f => f.EndsWith(".prod.yaml", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	[Trait("Category", "Integration")]
	public void ResolveForProject_LocalDevComposeFiles_DoesNotContainProdOrSharedOrExtensionFile()
	{
		var (config, project) = BuildWordpressContext();
		var resolver = new DockerResolver(config);

		var def = resolver.ResolveForProject(project);

		Assert.All(def.LocalDevComposeFiles, f => f.Contains(".dev.yaml", StringComparison.OrdinalIgnoreCase));
		Assert.DoesNotContain(def.LocalDevComposeFiles, f => f.Contains("_daps", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	[Trait("Category", "Integration")]
	public void ResolveForProject_ProdProjectComposeFiles_ContainsSharedAndProdFiles()
	{
		var (config, project) = BuildWordpressContext();
		var resolver = new DockerResolver(config);

		var def = resolver.ResolveForProject(project);

		Assert.Contains(def.ProdProjectComposeFiles, f => f.EndsWith("compose_mywpsite.yaml", StringComparison.OrdinalIgnoreCase));
		Assert.Contains(def.ProdProjectComposeFiles, f => f.EndsWith("compose_mywpsite.prod.yaml", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	[Trait("Category", "Integration")]
	public void ResolveForProject_ProdProjectComposeFiles_DoesNotContainDevFile()
	{
		var (config, project) = BuildWordpressContext();
		var resolver = new DockerResolver(config);

		var def = resolver.ResolveForProject(project);

		Assert.DoesNotContain(def.ProdProjectComposeFiles, f => f.EndsWith(".dev.yaml", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	[Trait("Category", "Integration")]
	public void ResolveForProject_LocalDapsExtensionComposeFiles_ContainsDevToolkitMount()
	{
		var (config, project) = BuildWordpressContext();
		var resolver = new DockerResolver(config);

		var def = resolver.ResolveForProject(project);

		Assert.All(def.LocalDapsExtensionComposeFiles, f => f.Contains("_daps", StringComparison.OrdinalIgnoreCase));
		Assert.Contains(def.LocalDapsExtensionComposeFiles, f => f.EndsWith(".dev.yaml", StringComparison.OrdinalIgnoreCase));
	}
}
