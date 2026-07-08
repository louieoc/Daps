using Dapsman.Domain;

namespace Dapsman.Infrastructure.Tests;

public sealed class HostingProviderResolverTests
{
	// HostingProviderResolver reads instance vars and openrc files from disk.
	// These tests use a minimal config with real temp files to satisfy those reads.

	private static (DapsConfig config, string instanceVarsPath) BuildConfig(
		params (string name, bool disabled)[] providers)
	{
		var root = Path.Combine(Path.GetTempPath(), "dapsman-tests", Guid.NewGuid().ToString("N"));
		var hostingDir = Path.Combine(root, "hosting");
		Directory.CreateDirectory(hostingDir);

		var providerDefs = new List<ProviderDefinition>();
		foreach (var (name, disabled) in providers)
		{
			var openrcPath = Path.Combine(hostingDir, $"{name}_openrc.sh");
			File.WriteAllText(openrcPath, $"export OS_AUTH_URL=http://example.com\n");

			var instanceVarsPath = Path.Combine(hostingDir, $"openstack_{name}_instance_vars.sh");
			File.WriteAllText(instanceVarsPath,
				$"export OS_SERVER_IP=\"10.0.0.{providerDefs.Count + 1}\"\nexport OS_KEY_NAME=\"key-{name}\"\n");

			providerDefs.Add(new OpenstackProviderDefinition
			{
				Name = name,
				OpenRcPath = openrcPath,
				Disabled = disabled,
			});
		}

		var config = new DapsConfig
		{
			DapsRootPath = root,
			FullYamlPath = Path.Combine(root, "daps.yaml"),
			Providers = providerDefs,
		};

		// Return path for the first provider's instance vars (used in single-provider tests)
		var firstVarsPath = Path.Combine(hostingDir, $"openstack_{providers[0].name}_instance_vars.sh");
		return (config, firstVarsPath);
	}

	[Fact]
	public void Resolve_TwoParam_CliOverridesProjectProvider()
	{
		var (config, _) = BuildConfig(("ramnode", false), ("dreamcompute", false));
		// Write a second instance vars for dreamcompute
		var resolver = new HostingProviderResolver(config);

		var result = resolver.Resolve(cliProviderName: "ramnode", projectProviderName: "dreamcompute");

		Assert.Equal("ramnode", result.ConfigDefinition.Name);
	}

	[Fact]
	public void Resolve_TwoParam_UsesProjectProviderWhenNoCliFlag()
	{
		var (config, _) = BuildConfig(("ramnode", false), ("dreamcompute", false));
		var resolver = new HostingProviderResolver(config);

		var result = resolver.Resolve(cliProviderName: null, projectProviderName: "dreamcompute");

		Assert.Equal("dreamcompute", result.ConfigDefinition.Name);
	}

	[Fact]
	public void Resolve_TwoParam_UsesFirstNonDisabledProviderAsFallback()
	{
		var (config, _) = BuildConfig(("ramnode", false), ("dreamcompute", false));
		var resolver = new HostingProviderResolver(config);

		var result = resolver.Resolve(cliProviderName: null, projectProviderName: null);

		Assert.Equal("ramnode", result.ConfigDefinition.Name);
	}

	[Fact]
	public void Resolve_TwoParam_SkipsDisabledProviderInFallback()
	{
		var (config, _) = BuildConfig(("ramnode", true), ("dreamcompute", false));
		var resolver = new HostingProviderResolver(config);

		var result = resolver.Resolve(cliProviderName: null, projectProviderName: null);

		Assert.Equal("dreamcompute", result.ConfigDefinition.Name);
	}

	[Fact]
	public void Resolve_TwoParam_ThrowsWhenProjectProviderNotInList()
	{
		var (config, _) = BuildConfig(("ramnode", false));
		var resolver = new HostingProviderResolver(config);

		var ex = Assert.Throws<InvalidOperationException>(() =>
			resolver.Resolve(cliProviderName: null, projectProviderName: "bogus"));

		Assert.Contains("bogus", ex.Message);
	}

	[Fact]
	public void Resolve_TwoParam_ThrowsWhenNamedProviderIsDisabled()
	{
		var (config, _) = BuildConfig(("ramnode", false), ("dreamcompute", true));
		var resolver = new HostingProviderResolver(config);

		var ex = Assert.Throws<InvalidOperationException>(() =>
			resolver.Resolve(cliProviderName: null, projectProviderName: "dreamcompute"));

		Assert.Contains("disabled", ex.Message, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void ResolveExplicit_SingleProvider_ReturnsItWithoutExplicitName()
	{
		var (config, _) = BuildConfig(("ramnode", false));
		var resolver = new HostingProviderResolver(config);

		var result = resolver.ResolveExplicit(providerName: null);

		Assert.Equal("ramnode", result.ConfigDefinition.Name);
	}

	[Fact]
	public void ResolveExplicit_MultipleProviders_ThrowsWhenNameIsNull()
	{
		var (config, _) = BuildConfig(("ramnode", false), ("dreamcompute", false));
		var resolver = new HostingProviderResolver(config);

		var ex = Assert.Throws<InvalidOperationException>(() =>
			resolver.ResolveExplicit(providerName: null));

		Assert.Contains("--provider", ex.Message);
		Assert.Contains("ramnode", ex.Message);
		Assert.Contains("dreamcompute", ex.Message);
	}

	[Fact]
	public void ResolveExplicit_MultipleProviders_OneDisabled_ReturnsActiveWhenNameIsNull()
	{
		var (config, _) = BuildConfig(("ramnode", false), ("dreamcompute", true));
		var resolver = new HostingProviderResolver(config);

		// Only one active provider — should return it without requiring --provider
		var result = resolver.ResolveExplicit(providerName: null);

		Assert.Equal("ramnode", result.ConfigDefinition.Name);
	}

	[Fact]
	public void ResolveExplicit_ThrowsWhenProviderNotFound()
	{
		var (config, _) = BuildConfig(("ramnode", false));
		var resolver = new HostingProviderResolver(config);

		var ex = Assert.Throws<InvalidOperationException>(() =>
			resolver.ResolveExplicit(providerName: "bogus"));

		Assert.Contains("bogus", ex.Message);
	}

	[Fact]
	public void Resolve_GenericVps_UsesHostnameAndUserFromConfig_NoFileIO()
	{
		var root = Path.Combine(Path.GetTempPath(), "dapsman-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(root);

		var config = new DapsConfig
		{
			DapsRootPath = root,
			FullYamlPath = Path.Combine(root, "daps.yaml"),
			Providers =
			[
				new GenericVpsProviderDefinition { Name = "ovhcloud1", Hostname = "vps-12345678.vps.ovh.us", User = "ubuntu" },
			],
		};
		var resolver = new HostingProviderResolver(config);

		var result = resolver.Resolve(cliProviderName: null, projectProviderName: null);

		Assert.Equal("vps-12345678.vps.ovh.us", result.RemoteHost);
		Assert.Equal("ubuntu", result.RemoteUser);
		Assert.Equal("daps-key-ovhcloud1", result.KeyName);
	}
}
