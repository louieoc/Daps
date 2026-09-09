using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure.Tests;

public sealed class RemoteOfflineStatusPlanBuilderTests
{
	[Fact]
	public void BuildPlan_Offline_IncludesEveryAddressFromEverySiteBlock()
	{
		var plan = BuildOfflinePlan("""
# comment line
j-shirt.com, www.j-shirt.com, test.j-shirt.com {
	encode gzip zstd
	reverse_proxy jshirt-site:8080
}

api.j-shirt.com {
	encode gzip zstd
	reverse_proxy jshirt-api:8081
}
""");

		Assert.StartsWith("j-shirt.com, www.j-shirt.com, test.j-shirt.com, api.j-shirt.com {", plan.CaddySiteContent);
		Assert.DoesNotContain(", {", plan.CaddySiteContent);
		Assert.Contains("503", plan.CaddySiteContent);
	}

	[Fact]
	public void BuildPlan_Offline_IgnoresNestedBlocksAndPlaceholders()
	{
		var plan = BuildOfflinePlan("""
louisocallaghan.com {
	encode gzip zstd
	basicauth {
		admin hash
	}
	reverse_proxy loccom:80
}

www.louisocallaghan.com {
	redir https://louisocallaghan.com{uri} permanent
}
""");

		Assert.StartsWith("louisocallaghan.com, www.louisocallaghan.com {", plan.CaddySiteContent);
		Assert.DoesNotContain("basicauth", plan.CaddySiteContent);
		Assert.DoesNotContain("reverse_proxy", plan.CaddySiteContent);
	}

	[Fact]
	public void BuildPlan_Offline_PrefersHandWrittenOfflineFile()
	{
		var plan = BuildOfflinePlan(
			"""
example.com {
	reverse_proxy alpha:80
}
""",
			offlineContent: "example.com {\n\trespond \"custom\" 503\n}\n");

		Assert.Contains("custom", plan.CaddySiteContent);
	}

	[Fact]
	public void BuildPlan_Online_RestoresProdContentVerbatim()
	{
		var prodContent = """
example.com, www.example.com {
	reverse_proxy alpha:80
}
""";

		var plan = BuildOfflinePlan(prodContent, offline: false);

		Assert.Equal(prodContent, plan.CaddySiteContent);
		Assert.False(plan.IsOffline);
	}

	[Fact]
	public void BuildPlan_NoProviderOnCommandLine_UsesTheProjectsOwnProvider()
	{
		var resolver = new FakeHostingProviderResolver(TestProvider());

		BuildOfflinePlan(
			"""
example.com {
	reverse_proxy alpha:80
}
""",
			cliProviderName: null,
			projectProvider: "ovhcloud",
			hostingResolver: resolver);

		Assert.Equal("ovhcloud", resolver.ObservedProjectProviderName);
	}

	private static HostingProvider TestProvider() => new()
	{
		ConfigDefinition = new GenericVpsProviderDefinition { Name = "ovhcloud1", Hostname = "vps.example.com", User = "ubuntu" },
		Options = new GenericVpsProviderOptions(),
		RemoteHost = "vps.example.com",
		RemoteUser = "ubuntu",
		KeyName = "daps-key-ovhcloud1",
	};

	private static OfflineStatusPlan BuildOfflinePlan(
		string prodCaddyContent,
		string? offlineContent = null,
		bool offline = true,
		string? cliProviderName = "ramnode",
		string? projectProvider = null,
		FakeHostingProviderResolver? hostingResolver = null)
	{
		var root = Path.Combine(Path.GetTempPath(), "dapsman-tests", Guid.NewGuid().ToString("N"));
		var projectRoot = Path.Combine(root, "alpha-project");
		var dockerRoot = Path.Combine(projectRoot, "_docker");
		Directory.CreateDirectory(dockerRoot);
		File.WriteAllText(Path.Combine(dockerRoot, "compose_alpha.yaml"), "services: {}");
		File.WriteAllText(Path.Combine(dockerRoot, "compose_daps_alpha.dev.yaml"), """
services:
  toolkit:
    volumes:
      - ../../alpha-project:/srv/projects/alpha
""");

		var caddyRoot = Path.Combine(projectRoot, "_caddy_sites");
		Directory.CreateDirectory(caddyRoot);
		File.WriteAllText(Path.Combine(caddyRoot, "alpha.prod.caddy"), prodCaddyContent);
		if (offlineContent is not null)
		{
			File.WriteAllText(Path.Combine(caddyRoot, "alpha.offline.caddy"), offlineContent);
		}

		var config = new DapsConfig
		{
			DapsRootPath = root,
			FullYamlPath = Path.Combine(root, "daps.yaml"),
			Projects = new[] { new ProjectDefinition { Name = "alpha", Path = projectRoot, Provider = projectProvider } },
		};

		var containerManager = new FakeContainerManager(null);

		var builder = new RemoteOfflineStatusPlanBuilder(
			offline,
			cliProviderName,
			new ToolkitResolver(config, containerManager),
			new ProjectResolver(config),
			new CaddyResolver(config, containerManager),
			hostingResolver ?? new FakeHostingProviderResolver(TestProvider()));

		var result = builder.BuildPlan(config, new OfflineStatusOptions { ProjectName = "alpha" });
		Assert.True(result.IsSupported, string.Join("; ", result.Warnings));
		return result.Plan!;
	}
}
