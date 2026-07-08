using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure.Tests;

public sealed class OpenStackRemoteProvisionPlanBuilderTests
{
	[Fact]
	public void BuildRemotePlan_UsesConfiguredScriptsAndMapsToToolkitPaths()
	{
		var root = CreateTempDirectory();
		var scripts = Path.Combine(root, "scripts");
		var hosting = Path.Combine(root, "hosting");
		Directory.CreateDirectory(scripts);
		Directory.CreateDirectory(hosting);

		var openRc = Path.Combine(hosting, "dream_openrc.sh");
		var setVars = Path.Combine(hosting, "openstack_dream_instance_vars.sh");
		var create = Path.Combine(scripts, "openstack-create-instance.sh");
		File.WriteAllText(openRc, "#!/usr/bin/env bash\n");
		File.WriteAllText(setVars, "#!/usr/bin/env bash\n");
		File.WriteAllText(create, "#!/usr/bin/env bash\n");

		var config = new DapsConfig
		{
			DapsRootPath = root,
			FullYamlPath = Path.Combine(root, "daps.yaml"),
			Providers =
			[
				new OpenstackProviderDefinition { Name = "dream", OpenRcPath = openRc },
			],
			Projects = Array.Empty<ProjectDefinition>(),
		};

		var containerManager = new FakeContainerManager(["daps-toolkit-1", "daps-caddy-1"]);
		var providerResolver = new FakeHostingProviderResolver(new HostingProvider
		{
			ConfigDefinition = new OpenstackProviderDefinition { Name = "dream", OpenRcPath = openRc },
			KeyName = "daps-key-dream",
			RemoteHost = "",
			RemoteUser = "ubuntu",
			Options = new OpenStackProviderOptions { InstanceVarsFilePath = setVars },
		});
		var toolkitResolver = new ToolkitResolver(config, containerManager);
		var planner = new OpenStackRemoteProvisionPlanBuilder(toolkitResolver, providerResolver);
		var plan = planner.BuildRemotePlan(config, new RemoteProvisionOptions
		{
			ProviderName = "dream",
			SetVarsScriptPath = "./hosting/openstack_dream_instance_vars.sh",
			CreateScriptPath = "./scripts/openstack-create-instance.sh",
		});

		Assert.Equal("dream", plan.ProviderName);
		Assert.Equal("daps-key-dream", plan.DefaultKeyName);
		Assert.Equal("daps-toolkit-1", plan.ToolkitContainerName);
		Assert.Equal(openRc, plan.ProviderDetails.Single(d => d.Key == "openrc script").Value);
		Assert.Equal(setVars, plan.ProviderDetails.Single(d => d.Key == "set-vars script").Value);
		Assert.Equal(create, plan.ProviderDetails.Single(d => d.Key == "create script").Value);
		Assert.Contains("openstack keypair show", plan.ToolkitCommand, StringComparison.Ordinal);
		Assert.Contains("ssh-keygen -t rsa -b 4096", plan.ToolkitCommand, StringComparison.Ordinal);
	}

	private static string CreateTempDirectory()
	{
		var path = Path.Combine(Path.GetTempPath(), "dapsman-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(path);
		return path;
	}
}
