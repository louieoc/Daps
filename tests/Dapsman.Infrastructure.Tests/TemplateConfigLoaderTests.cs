namespace Dapsman.Infrastructure.Tests;

public sealed class TemplateConfigLoaderTests
{
    [Fact]
    public void Load_WhenPlaceholderNotDefined_Throws()
    {
        var root = TestHelper.CreateTempDirectory();

        var yaml = """
exclude-from-transform: [wp-content, _secrets, _backup]
""";

        var templateRoot = Path.Combine(root, "templates", "mytemplate");
        Directory.CreateDirectory(templateRoot);
        File.WriteAllText(Path.Combine(templateRoot, "template.yaml"), yaml);

        var loader = new TemplateConfigLoader();

        var ex = Assert.Throws<InvalidOperationException>(() => loader.Load(templateRoot));
    }

    [Fact]
    public void Load_ReadsExcludedPaths()
    {
        var root = TestHelper.CreateTempDirectory();

        var yaml = """
placeholder: hey
exclude-from-transform: [wp-content, _secrets, _backup]
""";

        var templateRoot = Path.Combine(root, "templates", "mytemplate");
        Directory.CreateDirectory(templateRoot);
        File.WriteAllText(Path.Combine(templateRoot, "template.yaml"), yaml);

        var loader = new TemplateConfigLoader();
        var config = loader.Load(templateRoot);

        Assert.Equal(3, config.ExcludedFromTransformation.Length);
        Assert.Equal("wp-content", config.ExcludedFromTransformation.First());
    }

    [Theory]
    [InlineData("exclude-from-transform: [wp-content, _secrets, ]")]
    [InlineData("exclude-from-transform: [ ]")]
    [InlineData("exclude-from-transform:")]
    public void Load_DropsBlankExcludedPaths(string excludeLine)
    {
        // A blank entry becomes an exclusion of the template root itself, which silently skips
        // placeholder replacement for every file in the template.
        var root = TestHelper.CreateTempDirectory();

        var yaml = $"""
placeholder: hey
{excludeLine}
""";

        var templateRoot = Path.Combine(root, "templates", "mytemplate");
        Directory.CreateDirectory(templateRoot);
        File.WriteAllText(Path.Combine(templateRoot, "template.yaml"), yaml);

        var loader = new TemplateConfigLoader();
        var config = loader.Load(templateRoot);

        Assert.DoesNotContain(config.ExcludedFromTransformation, v => string.IsNullOrWhiteSpace(v));
    }

    [Fact]
    public void DeriveProjectPlaceholder_ReturnsExpectedValue()
    {
        var root = TestHelper.CreateTempDirectory();
        var templateRoot = Path.Combine(root, "templates", "mytemplate");
        var dockerDir = Path.Combine(templateRoot, "_docker");
        Directory.CreateDirectory(dockerDir);
        var dockerFile = "compose_daps_mywpsite.dev.yaml";
        File.WriteAllText(Path.Combine(dockerDir, dockerFile), "hey");

        var expectedPlaceholder = "mywpsite";
        var actualPlaceholder = TemplateConfigLoader.DeriveProjectPlaceholder(templateRoot);

        Assert.Equal(expectedPlaceholder, actualPlaceholder);
    }
}
