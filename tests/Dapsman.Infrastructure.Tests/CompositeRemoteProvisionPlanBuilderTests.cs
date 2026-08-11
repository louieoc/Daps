using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure.Tests;

public sealed class CompositeRemoteProvisionPlanBuilderTests
{
	[Fact]
	public void BuildRemotePlan_DispatchesToOpenStackBuilder_ForOpenstackProvider()
	{
		var root = CreateTempDirectory();
		var hosting = Path.Combine(root, "hosting");
		var scripts = Path.Combine(root, "scripts");
		Directory.CreateDirectory(hosting);
		Directory.CreateDirectory(scripts);

		var openRc = Path.Combine(hosting, "dream_openrc.sh");
		var setVars = Path.Combine(hosting, "openstack_dream_instance_vars.sh");
		var create = Path.Combine(scripts, "openstack-create-instance.sh");
		File.WriteAllText(openRc, "#!/usr/bin/env bash\n");
		File.WriteAllText(setVars, "#!/usr/bin/env bash\n");
		File.WriteAllText(create, "#!/usr/bin/env bash\n");
		File.WriteAllText(Path.Combine(scripts, "configure-host.sh"), "#!/usr/bin/env bash\n");

		var config = new DapsConfig
		{
			DapsRootPath = root,
			FullYamlPath = Path.Combine(root, "daps.yaml"),
			Providers = [new OpenstackProviderDefinition { Name = "dream", OpenRcPath = openRc }],
			Projects = Array.Empty<ProjectDefinition>(),
		};

		var containerManager = new FakeContainerManager(["daps-toolkit-1"]);
		var providerResolver = new FakeHostingProviderResolver(new HostingProvider
		{
			ConfigDefinition = new OpenstackProviderDefinition { Name = "dream", OpenRcPath = openRc },
			KeyName = "daps-key-dream",
			RemoteHost = "1.2.3.4",
			RemoteUser = "ubuntu",
			Options = new OpenStackProviderOptions { InstanceVarsFilePath = setVars },
		});
		var toolkitResolver = new ToolkitResolver(config, containerManager);
		var openStackBuilder = new OpenStackRemoteProvisionPlanBuilder(toolkitResolver, providerResolver);
		var genericVpsBuilder = new GenericVpsRemoteProvisionPlanBuilder(toolkitResolver, providerResolver);
		var composite = new CompositeRemoteProvisionPlanBuilder(providerResolver, openStackBuilder, genericVpsBuilder);

		var plan = composite.BuildRemotePlan(config, new RemoteProvisionOptions
		{
			ProviderName = "dream",
			SetVarsScriptPath = "./hosting/openstack_dream_instance_vars.sh",
			CreateScriptPath = "./scripts/openstack-create-instance.sh",
		});

		Assert.Contains("openstack keypair show", plan.ToolkitCommand, StringComparison.Ordinal);
	}

	[Fact]
	public void BuildRemotePlan_DispatchesToGenericVpsBuilder_ForGenericVpsProvider()
	{
		var root = CreateTempDirectory();
		var scripts = Path.Combine(root, "scripts");
		Directory.CreateDirectory(scripts);
		File.WriteAllText(Path.Combine(scripts, "provision-generic-vps.sh"), "#!/usr/bin/env bash\n");
		File.WriteAllText(Path.Combine(scripts, "configure-host.sh"), "#!/usr/bin/env bash\n");

		var config = new DapsConfig
		{
			DapsRootPath = root,
			FullYamlPath = Path.Combine(root, "daps.yaml"),
			Providers = [new GenericVpsProviderDefinition { Name = "ovhcloud1", Hostname = "vps.example.com", User = "ubuntu" }],
			Projects = Array.Empty<ProjectDefinition>(),
		};

		var containerManager = new FakeContainerManager(["daps-toolkit-1"]);
		var providerResolver = new FakeHostingProviderResolver(new HostingProvider
		{
			ConfigDefinition = new GenericVpsProviderDefinition { Name = "ovhcloud1", Hostname = "vps.example.com", User = "ubuntu" },
			KeyName = "daps-key-ovhcloud1",
			RemoteHost = "vps.example.com",
			RemoteUser = "ubuntu",
			Options = new GenericVpsProviderOptions(),
		});
		var toolkitResolver = new ToolkitResolver(config, containerManager);
		var openStackBuilder = new OpenStackRemoteProvisionPlanBuilder(toolkitResolver, providerResolver);
		var genericVpsBuilder = new GenericVpsRemoteProvisionPlanBuilder(toolkitResolver, providerResolver);
		var composite = new CompositeRemoteProvisionPlanBuilder(providerResolver, openStackBuilder, genericVpsBuilder);

		var plan = composite.BuildRemotePlan(config, new RemoteProvisionOptions { ProviderName = "ovhcloud1" });

		Assert.Contains("ssh-copy-id", plan.ToolkitCommand, StringComparison.Ordinal);
	}

	private static string CreateTempDirectory()
	{
		var path = Path.Combine(Path.GetTempPath(), "dapsman-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(path);
		return path;
	}
}
