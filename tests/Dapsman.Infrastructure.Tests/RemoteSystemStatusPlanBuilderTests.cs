using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure.Tests;

public sealed class RemoteSystemStatusPlanBuilderTests
{
	[Fact]
	public void BuildPlan_ResolvesProviderExplicitly_SoAnOmittedProviderCannotReportOnTheWrongHost()
	{
		var providerResolver = CreateProviderResolver(remoteUser: "ubuntu");
		var (config, builder) = CreateBuilder(providerResolver);

		builder.BuildPlan(config, new SystemStatusOptions { ProviderName = null });

		// There is no project to infer a provider from, and answering "how is my server doing?"
		// about a different server than the user meant is worse than refusing to guess.
		Assert.True(providerResolver.ResolvedExplicitly);
	}

	[Fact]
	public void BuildPlan_NonRootRemoteUser_PrefixesDockerWithSudo()
	{
		var (config, builder) = CreateBuilder(CreateProviderResolver(remoteUser: "ubuntu"));

		var plan = builder.BuildPlan(config, new SystemStatusOptions());

		// docker stats and system df need root; a non-root SSH user reaches them through sudo.
		Assert.Equal("sudo docker", plan.DockerCommandPrefix);
	}

	[Fact]
	public void BuildPlan_RootRemoteUser_CallsDockerDirectly()
	{
		var (config, builder) = CreateBuilder(CreateProviderResolver(remoteUser: "root"));

		var plan = builder.BuildPlan(config, new SystemStatusOptions());

		// RamNode instances log in as root, where sudo may not even be installed.
		Assert.Equal("docker", plan.DockerCommandPrefix);
	}

	[Fact]
	public void BuildPlan_PointsAtTheCheckedInStatusScript()
	{
		var (config, builder) = CreateBuilder(CreateProviderResolver(remoteUser: "root"));

		var plan = builder.BuildPlan(config, new SystemStatusOptions());

		Assert.Equal(Path.Combine(config.DapsRootPath, "scripts", "remote-system-status.sh"), plan.StatusScriptHostPath);
	}

	[Fact]
	public void BuildPlan_CarriesVerboseThrough_SoTheRemoteScriptKnowsWhetherToCollectDetail()
	{
		var (config, builder) = CreateBuilder(CreateProviderResolver(remoteUser: "root"));

		var plan = builder.BuildPlan(config, new SystemStatusOptions { Verbose = true });

		Assert.True(plan.Verbose);
	}

	[Fact]
	public void BuildPlanForHost_IsNeverVerbose_SoACallerAfterOneFieldDoesNotPayForTheDuWalk()
	{
		var plan = BuildPlanForTestHost(remoteUser: "ubuntu");

		// prod deploy calls this for the docker platform alone. The verbose pass adds a `du -sb`
		// over every project directory, which is unbounded and grows with the size of the sites.
		Assert.False(plan.Verbose);
	}

	[Fact]
	public void BuildPlanForHost_NonRootRemoteUser_PrefixesDockerWithSudo()
	{
		var plan = BuildPlanForTestHost(remoteUser: "ubuntu");

		Assert.Equal("sudo docker", plan.DockerCommandPrefix);
	}

	[Fact]
	public void BuildPlanForHost_RootRemoteUser_CallsDockerDirectly()
	{
		var plan = BuildPlanForTestHost(remoteUser: "root");

		Assert.Equal("docker", plan.DockerCommandPrefix);
	}

	[Fact]
	public void BuildPlanForHost_PointsAtTheCheckedInStatusScript()
	{
		var plan = BuildPlanForTestHost(remoteUser: "root");

		// Same script as BuildPlan resolves; the two must not drift onto different payloads.
		Assert.Equal(Path.Combine("/daps", "scripts", "remote-system-status.sh"), plan.StatusScriptHostPath);
	}

	private static SystemStatusPlan BuildPlanForTestHost(string remoteUser) =>
		RemoteSystemStatusPlanBuilder.BuildPlanForHost(
			providerName: "ovhcloud",
			dapsRootPath: "/daps",
			toolkitContainerName: "daps-toolkit-1",
			remoteHost: "vps-12345678.vps.ovh.us",
			remoteUser: remoteUser,
			sshKeyName: "daps-key-ovhcloud");

	private static FakeHostingProviderResolver CreateProviderResolver(string remoteUser) =>
		new(new HostingProvider
		{
			ConfigDefinition = new GenericVpsProviderDefinition
			{
				Name = "ovhcloud",
				Hostname = "vps-12345678.vps.ovh.us",
				User = remoteUser,
			},
			KeyName = "daps-key-ovhcloud",
			RemoteHost = "vps-12345678.vps.ovh.us",
			RemoteUser = remoteUser,
			Options = new GenericVpsProviderOptions(),
		});

	private static (DapsConfig Config, RemoteSystemStatusPlanBuilder Builder) CreateBuilder(
		FakeHostingProviderResolver providerResolver)
	{
		var root = TestHelper.CreateTempDirectory();

		var config = new DapsConfig
		{
			DapsRootPath = root,
			FullYamlPath = Path.Combine(root, "daps.yaml"),
			Providers = [],
			Projects = Array.Empty<ProjectDefinition>(),
		};

		var toolkitResolver = new ToolkitResolver(config, new FakeContainerManager(["daps-toolkit-1"]));

		return (config, new RemoteSystemStatusPlanBuilder(toolkitResolver, providerResolver));
	}
}
