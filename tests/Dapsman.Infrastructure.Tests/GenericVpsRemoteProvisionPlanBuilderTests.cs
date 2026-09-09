using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure.Tests;

public sealed class GenericVpsRemoteProvisionPlanBuilderTests
{
	[Fact]
	public void BuildRemotePlan_UsesHostnameAndUser_AndBuildsSshChain()
	{
		var root = TestHelper.CreateTempDirectory();
		var scripts = Path.Combine(root, "scripts");
		Directory.CreateDirectory(scripts);
		File.WriteAllText(Path.Combine(scripts, "provision-generic-vps.sh"), "#!/usr/bin/env bash\n");
		File.WriteAllText(Path.Combine(scripts, "configure-host.sh"), "#!/usr/bin/env bash\n");

		var config = new DapsConfig
		{
			DapsRootPath = root,
			FullYamlPath = Path.Combine(root, "daps.yaml"),
			Providers =
			[
				new GenericVpsProviderDefinition { Name = "ovhcloud1", Hostname = "vps-12345678.vps.ovh.us", User = "ubuntu" },
			],
			Projects = Array.Empty<ProjectDefinition>(),
		};

		var containerManager = new FakeContainerManager(["daps-toolkit-1", "daps-caddy-1"]);
		var providerResolver = new FakeHostingProviderResolver(new HostingProvider
		{
			ConfigDefinition = new GenericVpsProviderDefinition { Name = "ovhcloud1", Hostname = "vps-12345678.vps.ovh.us", User = "ubuntu" },
			KeyName = "daps-key-ovhcloud1",
			RemoteHost = "vps-12345678.vps.ovh.us",
			RemoteUser = "ubuntu",
			Options = new GenericVpsProviderOptions(),
		});
		var toolkitResolver = new ToolkitResolver(config, containerManager);
		var planner = new GenericVpsRemoteProvisionPlanBuilder(toolkitResolver, providerResolver);

		var plan = planner.BuildRemotePlan(config, new RemoteProvisionOptions { ProviderName = "ovhcloud1" });

		Assert.Equal("ovhcloud1", plan.ProviderName);
		Assert.Equal("daps-key-ovhcloud1", plan.DefaultKeyName);
		Assert.Equal("daps-toolkit-1", plan.ToolkitContainerName);
		Assert.Equal("vps-12345678.vps.ovh.us", plan.ProviderDetails.Single(d => d.Key == "hostname").Value);
		Assert.Equal("ubuntu", plan.ProviderDetails.Single(d => d.Key == "user").Value);
		Assert.Contains("ssh-keygen -t rsa -b 4096", plan.ToolkitCommand, StringComparison.Ordinal);
		Assert.Contains("ssh-copy-id", plan.ToolkitCommand, StringComparison.Ordinal);
		Assert.Contains("ubuntu@vps-12345678.vps.ovh.us", plan.ToolkitCommand, StringComparison.Ordinal);
		Assert.Contains("/srv/daps/.dapsman/provision/provision-generic-vps.sh", plan.ToolkitCommand, StringComparison.Ordinal);
		Assert.DoesNotContain("--upgrade", plan.ToolkitCommand, StringComparison.Ordinal);
	}

	[Fact]
	public void BuildRemotePlan_UploadsAndRunsHostConfigurationScript()
	{
		var root = TestHelper.CreateTempDirectory();
		var scripts = Path.Combine(root, "scripts");
		Directory.CreateDirectory(scripts);
		File.WriteAllText(Path.Combine(scripts, "provision-generic-vps.sh"), "#!/usr/bin/env bash\n");
		File.WriteAllText(Path.Combine(scripts, "configure-host.sh"), "#!/usr/bin/env bash\n");

		var config = new DapsConfig
		{
			DapsRootPath = root,
			FullYamlPath = Path.Combine(root, "daps.yaml"),
			Providers =
			[
				new GenericVpsProviderDefinition { Name = "ovhcloud1", Hostname = "vps-12345678.vps.ovh.us", User = "ubuntu" },
			],
			Projects = Array.Empty<ProjectDefinition>(),
		};

		var containerManager = new FakeContainerManager(["daps-toolkit-1"]);
		var providerResolver = new FakeHostingProviderResolver(new HostingProvider
		{
			ConfigDefinition = new GenericVpsProviderDefinition { Name = "ovhcloud1", Hostname = "vps-12345678.vps.ovh.us", User = "ubuntu" },
			KeyName = "daps-key-ovhcloud1",
			RemoteHost = "vps-12345678.vps.ovh.us",
			RemoteUser = "ubuntu",
			Options = new GenericVpsProviderOptions(),
		});
		var toolkitResolver = new ToolkitResolver(config, containerManager);
		var planner = new GenericVpsRemoteProvisionPlanBuilder(toolkitResolver, providerResolver);

		var plan = planner.BuildRemotePlan(config, new RemoteProvisionOptions { ProviderName = "ovhcloud1" });

		Assert.Contains("/srv/daps/.dapsman/provision/configure-host.sh", plan.ToolkitCommand, StringComparison.Ordinal);
		Assert.Contains("bash /tmp/configure-host.sh 04:00", plan.ToolkitCommand, StringComparison.Ordinal);
	}

	[Fact]
	public void BuildRemotePlan_WithUpgrade_PassesUpgradeFlagToRemoteScript()
	{
		var root = TestHelper.CreateTempDirectory();
		var scripts = Path.Combine(root, "scripts");
		Directory.CreateDirectory(scripts);
		File.WriteAllText(Path.Combine(scripts, "provision-generic-vps.sh"), "#!/usr/bin/env bash\n");
		File.WriteAllText(Path.Combine(scripts, "configure-host.sh"), "#!/usr/bin/env bash\n");

		var config = new DapsConfig
		{
			DapsRootPath = root,
			FullYamlPath = Path.Combine(root, "daps.yaml"),
			Providers =
			[
				new GenericVpsProviderDefinition { Name = "ovhcloud1", Hostname = "vps-12345678.vps.ovh.us", User = "ubuntu" },
			],
			Projects = Array.Empty<ProjectDefinition>(),
		};

		var containerManager = new FakeContainerManager(["daps-toolkit-1"]);
		var providerResolver = new FakeHostingProviderResolver(new HostingProvider
		{
			ConfigDefinition = new GenericVpsProviderDefinition { Name = "ovhcloud1", Hostname = "vps-12345678.vps.ovh.us", User = "ubuntu" },
			KeyName = "daps-key-ovhcloud1",
			RemoteHost = "vps-12345678.vps.ovh.us",
			RemoteUser = "ubuntu",
			Options = new GenericVpsProviderOptions(),
		});
		var toolkitResolver = new ToolkitResolver(config, containerManager);
		var planner = new GenericVpsRemoteProvisionPlanBuilder(toolkitResolver, providerResolver);

		var plan = planner.BuildRemotePlan(config, new RemoteProvisionOptions { ProviderName = "ovhcloud1", Upgrade = true });

		Assert.Contains("--upgrade", plan.ToolkitCommand, StringComparison.Ordinal);
	}
}
