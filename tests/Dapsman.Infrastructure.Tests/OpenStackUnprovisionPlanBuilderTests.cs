using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure.Tests;

public sealed class OpenStackUnprovisionPlanBuilderTests
{
	[Fact]
	public void BuildPlan_ResolvesProviderExplicitly_SoAnOmittedProviderCannotDestroyTheWrongHost()
	{
		var root = CreateTempDirectory();
		var hosting = Path.Combine(root, "hosting");
		Directory.CreateDirectory(hosting);

		var openRc = Path.Combine(hosting, "dream_openrc.sh");
		var instanceVars = Path.Combine(hosting, "openstack_dream_instance_vars.sh");
		File.WriteAllText(openRc, "#!/usr/bin/env bash\n");
		File.WriteAllText(instanceVars, "#!/usr/bin/env bash\n");

		var config = new DapsConfig
		{
			DapsRootPath = root,
			FullYamlPath = Path.Combine(root, "daps.yaml"),
			Providers = [new OpenstackProviderDefinition { Name = "dream", OpenRcPath = openRc }],
			Projects = Array.Empty<ProjectDefinition>(),
		};

		var providerResolver = new FakeHostingProviderResolver(new HostingProvider
		{
			ConfigDefinition = new OpenstackProviderDefinition { Name = "dream", OpenRcPath = openRc },
			KeyName = "daps-key-dream",
			RemoteHost = "",
			RemoteUser = "ubuntu",
			Options = new OpenStackProviderOptions { InstanceVarsFilePath = instanceVars },
		});

		var toolkitResolver = new ToolkitResolver(config, new FakeContainerManager(["daps-toolkit-1"]));
		var builder = new OpenStackUnprovisionPlanBuilder(toolkitResolver, providerResolver);

		var plan = builder.BuildPlan(config, new UnprovisionOptions { ProviderName = null });

		// Unprovision destroys a VM and has no project to infer a provider from, so it must go
		// through the overload that throws on ambiguity rather than defaulting to the first provider.
		Assert.True(providerResolver.ResolvedExplicitly);
		Assert.False(providerResolver.ResolvedWithoutProjectProvider);
		Assert.Equal("daps-key-dream", plan.DefaultKeyName);
	}

	private static string CreateTempDirectory()
	{
		var path = Path.Combine(Path.GetTempPath(), "dapsman-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(path);
		return path;
	}
}
