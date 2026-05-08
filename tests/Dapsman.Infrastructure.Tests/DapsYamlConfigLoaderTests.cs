using Dapsman.Domain;

namespace Dapsman.Infrastructure.Tests;

public sealed class DapsYamlConfigLoaderTests
{
    [Fact]
    public void Load_ReadsProjectsAndResolvesPaths()
    {
        var root = CreateTempDirectory();

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

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "dapsman-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
