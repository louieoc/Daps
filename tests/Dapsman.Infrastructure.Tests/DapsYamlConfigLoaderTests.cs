using Dapsman.Domain;

namespace Dapsman.Infrastructure.Tests;

public sealed class DapsYamlConfigLoaderTests
{
    [Fact]
    public void Load_ReadsProjectsAndResolvesPaths()
    {
        var root = TestHelper.CreateTempDirectory();

        var yaml = """
providers:
  demo: openstack
    openrc: ./hosting/demo_openrc.sh
projects:
  alpha:
    path: ../alpha
""";

        var hostingDir = Path.Combine(root, "hosting");
        Directory.CreateDirectory(hostingDir);
        File.WriteAllText(Path.Combine(root, "daps.yaml"), yaml);

        var loader = new DapsYamlConfigLoader();
        var config = loader.Load(Path.Combine(root, "daps.yaml"));

        Assert.Single(config.Providers);
        var provider = Assert.IsType<OpenstackProviderDefinition>(config.Providers[0]);
        Assert.Equal("demo", provider.Name);
        Assert.Single(config.Projects);
        Assert.Equal("alpha", config.Projects[0].Name);
        Assert.EndsWith(Path.Combine("..", "alpha"), Path.GetRelativePath(root, config.Projects[0].Path));
        Assert.EndsWith(Path.Combine("hosting", "demo_openrc.sh"), Path.GetRelativePath(root, provider.OpenRcPath));
    }

    [Fact]
    public void Load_DisabledProject_SetsDisabledTrue()
    {
        var root = TestHelper.CreateTempDirectory();

        var yaml = """
providers:
projects:
  alpha:
    path: ../alpha
    disabled: true
  beta:
    path: ../beta
""";

        File.WriteAllText(Path.Combine(root, "daps.yaml"), yaml);

        var loader = new DapsYamlConfigLoader();
        var config = loader.Load(Path.Combine(root, "daps.yaml"));

        Assert.Equal(2, config.Projects.Count);
        Assert.True(config.Projects.Single(p => p.Name == "alpha").Disabled);
        Assert.False(config.Projects.Single(p => p.Name == "beta").Disabled);
    }

    [Fact]
    public void Load_DisabledBeforePath_SetsDisabledTrue()
    {
        var root = TestHelper.CreateTempDirectory();

        var yaml = """
providers:
projects:
  alpha:
    disabled: true
    path: ../alpha
""";

        File.WriteAllText(Path.Combine(root, "daps.yaml"), yaml);

        var loader = new DapsYamlConfigLoader();
        var config = loader.Load(Path.Combine(root, "daps.yaml"));

        Assert.Single(config.Projects);
        Assert.True(config.Projects[0].Disabled);
    }

    [Fact]
    public void Load_ParsesProviderOnProject()
    {
        var root = TestHelper.CreateTempDirectory();

        var yaml = """
providers:
  ramnode: openstack
    openrc: ./hosting/ramnode_openrc.sh
projects:
  alpha:
    path: ../alpha
    provider: ramnode
""";

        File.WriteAllText(Path.Combine(root, "daps.yaml"), yaml);

        var loader = new DapsYamlConfigLoader();
        var config = loader.Load(Path.Combine(root, "daps.yaml"));

        Assert.Equal("ramnode", config.Projects[0].Provider);
    }

    [Fact]
    public void Load_ProjectWithNoProvider_HasNullProvider()
    {
        var root = TestHelper.CreateTempDirectory();

        var yaml = """
providers:
projects:
  alpha:
    path: ../alpha
""";

        File.WriteAllText(Path.Combine(root, "daps.yaml"), yaml);

        var loader = new DapsYamlConfigLoader();
        var config = loader.Load(Path.Combine(root, "daps.yaml"));

        Assert.Null(config.Projects[0].Provider);
    }

    [Fact]
    public void Load_ParsesDisabledOnProvider()
    {
        var root = TestHelper.CreateTempDirectory();

        var yaml = """
providers:
  ramnode: openstack
    openrc: ./hosting/ramnode_openrc.sh
  dreamcompute: openstack
    openrc: ./hosting/dreamcompute_openrc.sh
    disabled: true
projects:
""";

        File.WriteAllText(Path.Combine(root, "daps.yaml"), yaml);

        var loader = new DapsYamlConfigLoader();
        var config = loader.Load(Path.Combine(root, "daps.yaml"));

        Assert.Equal(2, config.Providers.Count);
        Assert.False(config.Providers.Single(p => p.Name == "ramnode").Disabled);
        Assert.True(config.Providers.Single(p => p.Name == "dreamcompute").Disabled);
    }

    [Fact]
    public void Load_ProviderDisabledBeforeOpenrc_SetsDisabledTrue()
    {
        var root = TestHelper.CreateTempDirectory();

        var yaml = """
providers:
  ramnode: openstack
    disabled: true
    openrc: ./hosting/ramnode_openrc.sh
projects:
""";

        File.WriteAllText(Path.Combine(root, "daps.yaml"), yaml);

        var loader = new DapsYamlConfigLoader();
        var config = loader.Load(Path.Combine(root, "daps.yaml"));

        Assert.Single(config.Providers);
        Assert.True(config.Providers[0].Disabled);
    }

    [Fact]
    public void Load_ParsesGenericVpsProvider()
    {
        var root = TestHelper.CreateTempDirectory();

        var yaml = """
providers:
  ovhcloud1: generic-vps
    hostname: vps-12345678.vps.ovh.us
    user: ubuntu
projects:
""";

        File.WriteAllText(Path.Combine(root, "daps.yaml"), yaml);

        var loader = new DapsYamlConfigLoader();
        var config = loader.Load(Path.Combine(root, "daps.yaml"));

        Assert.Single(config.Providers);
        var provider = Assert.IsType<GenericVpsProviderDefinition>(config.Providers[0]);
        Assert.Equal("ovhcloud1", provider.Name);
        Assert.Equal("vps-12345678.vps.ovh.us", provider.Hostname);
        Assert.Equal("ubuntu", provider.User);
    }

    [Fact]
    public void Load_GenericVpsProviderWithoutUser_IsNotAdded()
    {
        var root = TestHelper.CreateTempDirectory();

        var yaml = """
providers:
  ovhcloud1: generic-vps
    hostname: vps-12345678.vps.ovh.us
projects:
""";

        File.WriteAllText(Path.Combine(root, "daps.yaml"), yaml);

        var loader = new DapsYamlConfigLoader();
        var config = loader.Load(Path.Combine(root, "daps.yaml"));

        Assert.Empty(config.Providers);
    }

    [Fact]
    public void Load_GenericVpsProvider_DisabledBeforeFields_SetsDisabledTrue()
    {
        var root = TestHelper.CreateTempDirectory();

        var yaml = """
providers:
  ovhcloud1: generic-vps
    disabled: true
    hostname: vps-12345678.vps.ovh.us
    user: ubuntu
projects:
""";

        File.WriteAllText(Path.Combine(root, "daps.yaml"), yaml);

        var loader = new DapsYamlConfigLoader();
        var config = loader.Load(Path.Combine(root, "daps.yaml"));

        Assert.Single(config.Providers);
        Assert.True(config.Providers[0].Disabled);
    }
}
