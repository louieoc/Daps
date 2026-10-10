using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure.Tests;

public sealed class RemoteTeardownPlanBuilderTests
{
	[Fact]
	public void BuildPlan_NoProviderOnCommandLine_UsesTheProjectsOwnProvider()
	{
		var resolver = new FakeHostingProviderResolver(TestProvider());

		BuildPlan(cliProviderName: null, projectProvider: "ovhcloud", hostingResolver: resolver);

		Assert.Equal("ovhcloud", resolver.ObservedProjectProviderName);
		Assert.False(resolver.ResolvedExplicitly);
	}

	[Fact]
	public void BuildPlan_ProviderOnCommandLine_OverridesTheProjectsOwnProvider()
	{
		// Reported once as "daps.yaml overrides --provider". It does not, and this pins that:
		// teardown is destructive, so the flag silently losing to config would delete the wrong host.
		var config = ConfigWithTwoProviders(projectProvider: "ovhcloud");

		var plan = BuildPlan(
			cliProviderName: "ramnode",
			config: config,
			hostingResolver: new HostingProviderResolver(config));

		Assert.Equal("ramnode", plan.ProviderName);
		Assert.Equal("ramnode.example.com", plan.RemoteHost);
	}

	[Fact]
	public void BuildPlan_ProjectNotInDapsYaml_TearsDownTheNamedProviderByConvention()
	{
		var config = ConfigWithTwoProviders(includeProject: false);

		var plan = BuildPlan(
			cliProviderName: "ramnode",
			config: config,
			hostingResolver: new HostingProviderResolver(config));

		Assert.Equal("ramnode", plan.ProviderName);
		Assert.Equal("alpha", plan.ProjectName);
		Assert.Equal("/srv/projects/alpha", plan.RemoteProjectPath);
		Assert.Equal("alpha.prod.caddy", plan.CaddySiteFileName);

		// No local caddy file to read a domain from, so the remote site file is deleted
		// rather than replaced with a "site removed" page.
		Assert.Null(plan.RemovedCaddyContent);
	}

	[Fact]
	public void BuildPlan_ProjectNotInDapsYamlAndNoProviderNamed_RefusesToGuessAHost()
	{
		var config = ConfigWithTwoProviders(includeProject: false);

		var ex = Assert.Throws<InvalidOperationException>(() => BuildPlan(
			cliProviderName: null,
			config: config,
			hostingResolver: new HostingProviderResolver(config)));

		Assert.Contains("--provider is required", ex.Message);
		Assert.Contains("ramnode", ex.Message);
		Assert.Contains("ovhcloud", ex.Message);
	}

	[Fact]
	public void BuildPlan_ProjectNotInDapsYamlAndOnlyOneProvider_ResolvesItWithoutTheFlag()
	{
		var config = ConfigWithTwoProviders(includeProject: false, disableOvhcloud: true);

		var plan = BuildPlan(
			cliProviderName: null,
			config: config,
			hostingResolver: new HostingProviderResolver(config));

		Assert.Equal("ramnode", plan.ProviderName);
	}

	[Fact]
	public void BuildPlan_RegisteredProject_GeneratesTheRemovedPageFromTheLocalCaddyFile()
	{
		var plan = BuildPlan(cliProviderName: "ramnode");

		Assert.NotNull(plan.RemovedCaddyContent);
		Assert.Contains("alpha.example.com", plan.RemovedCaddyContent);
		Assert.Contains("410", plan.RemovedCaddyContent);
		Assert.Equal("alpha.prod.caddy", plan.CaddySiteFileName);
	}

	[Fact]
	public void BuildPlan_RegisteredProjectNamedInADifferentCase_UsesTheRegisteredSpelling()
	{
		// Remote paths are case-sensitive. The typed spelling would miss the project's files and
		// upload a second site file for the same domain, which Caddy refuses to load.
		var plan = BuildPlan(cliProviderName: "ramnode", projectName: "ALPHA");

		Assert.Equal("alpha", plan.ProjectName);
		Assert.Equal("alpha.prod.caddy", plan.CaddySiteFileName);
		Assert.Equal("/srv/projects/alpha", plan.RemoteProjectPath);
		Assert.NotNull(plan.RemovedCaddyContent);
	}

	[Theory]
	[InlineData("../etc")]
	[InlineData("..")]
	[InlineData(".")]
	[InlineData("a b")]
	[InlineData("x;rm -rf /")]
	[InlineData("alpha/beta")]
	[InlineData("$(whoami)")]
	[InlineData("")]
	public void BuildPlan_UnsafeProjectName_IsRejectedBeforeAnythingResolves(string projectName)
	{
		// The name is interpolated into `rm -rf /srv/projects/<name>` on the remote host.
		var resolver = new FakeHostingProviderResolver(TestProvider());

		Assert.Throws<ArgumentException>(() =>
			BuildPlan(cliProviderName: "ramnode", projectName: projectName, hostingResolver: resolver));

		Assert.Null(resolver.ObservedProjectProviderName);
		Assert.False(resolver.ResolvedExplicitly);
	}

	private static HostingProvider TestProvider() => new()
	{
		ConfigDefinition = new GenericVpsProviderDefinition { Name = "ovhcloud", Hostname = "vps.example.com", User = "ubuntu" },
		Options = new GenericVpsProviderOptions(),
		RemoteHost = "vps.example.com",
		RemoteUser = "ubuntu",
		KeyName = "daps-key-ovhcloud",
	};

	/// <summary>
	/// Two generic-vps providers, which the real resolver can resolve without any files on disk.
	/// </summary>
	private static DapsConfig ConfigWithTwoProviders(
		string? projectProvider = null,
		bool includeProject = true,
		bool disableOvhcloud = false)
	{
		var root = CreateProjectTree(out var projectRoot);

		return new DapsConfig
		{
			DapsRootPath = root,
			FullYamlPath = Path.Combine(root, "daps.yaml"),
			Projects = includeProject
				? [new ProjectDefinition { Name = "alpha", Path = projectRoot, Provider = projectProvider }]
				: [],
			Providers =
			[
				new GenericVpsProviderDefinition { Name = "ramnode", Hostname = "ramnode.example.com", User = "root" },
				new GenericVpsProviderDefinition { Name = "ovhcloud", Hostname = "vps.example.com", User = "ubuntu", Disabled = disableOvhcloud },
			],
		};
	}

	/// <summary>
	/// A project folder complete enough for ProjectResolver and CaddyResolver: the toolkit
	/// compose file they require, plus a prod caddy file to read a domain from.
	/// </summary>
	private static string CreateProjectTree(out string projectRoot)
	{
		var root = Path.Combine(Path.GetTempPath(), "dapsman-tests", Guid.NewGuid().ToString("N"));
		projectRoot = Path.Combine(root, "alpha-project");
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
		File.WriteAllText(Path.Combine(caddyRoot, "alpha.prod.caddy"), """
alpha.example.com {
	reverse_proxy alpha:80
}
""");

		return root;
	}

	private static RemoteTeardownPlan BuildPlan(
		string? cliProviderName,
		string? projectProvider = null,
		string? projectName = "alpha",
		DapsConfig? config = null,
		IHostingProviderResolver? hostingResolver = null)
	{
		if (config is null)
		{
			var root = CreateProjectTree(out var projectRoot);
			config = new DapsConfig
			{
				DapsRootPath = root,
				FullYamlPath = Path.Combine(root, "daps.yaml"),
				Projects = [new ProjectDefinition { Name = "alpha", Path = projectRoot, Provider = projectProvider }],
			};
		}

		var containerManager = new FakeContainerManager(null);

		var builder = new RemoteTeardownPlanBuilder(
			cliProviderName,
			new ToolkitResolver(config, containerManager),
			new ProjectResolver(config),
			new CaddyResolver(config, containerManager),
			hostingResolver ?? new FakeHostingProviderResolver(TestProvider()));

		return builder.BuildPlan(config, new TeardownOptions { ProjectName = projectName });
	}
}
