using Dapsman.Domain;

namespace Dapsman.Infrastructure.Tests;

public sealed class HostingProviderResolver_IntegrationTests
{
	[Fact]
	[Trait("Category", "Integration")]
	public void Resolve_GivenRamnodeHost_ReturnsExpectedValues()
	{
		var dapsPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
		var openRcPath = Path.Combine(dapsPath, "hosting", "ramnode_openrc");

		var config = new DapsConfig
		{
			DapsRootPath = dapsPath,
			FullYamlPath = Path.Combine(dapsPath, "daps.yaml"),
			Providers =
			[
				new OpenstackProviderDefinition
				{
					Name = "ramnode",
					OpenRcPath = openRcPath
				}
			]
		};

		var resolver = new HostingProviderResolver(config);
		var provider = resolver.Resolve("ramnode");

		Assert.NotNull(provider);
		Assert.Equal("root", provider.RemoteUser);
		Assert.Equal("107.191.113.26", provider.RemoteHost);
		Assert.Equal("daps-key-ramnode", provider.KeyName);
	}
}
