using Dapsman.Domain;

namespace Dapsman.Application.Tests;

public sealed class InitServiceTests
{
    [Fact]
    public void CreatePlan_DelegatesToPlanBuilder()
    {
        var builder = new FakePlanBuilder("daps.yaml");
        var service = new InitService(builder, new FakeDapsYamlEditor());

        var plan = service.CreatePlan(new InitOptions
        {
            TemplateName = "wordpress",
            ProjectName = "mysite",
        });

        Assert.True(builder.Called);
        Assert.Equal("mysite", plan.ProjectName);
    }

    [Fact]
    public void Execute_CopiesTemplateFilesToDestination_WithUnixLineEndings()
    {
        var templateDir = TestHelper.CreateTempDirectory();
        File.WriteAllText(Path.Combine(templateDir, "README.md"), "hello\r\nthere");
        var subDir = Path.Combine(templateDir, "sub");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "file.txt"), "content");

        var destDir = Path.Combine(Path.GetTempPath(), "dapsman-tests", Guid.NewGuid().ToString("N"));
        var dapsYaml = Path.Combine(TestHelper.CreateTempDirectory(), "daps.yaml");
        File.WriteAllText(dapsYaml, "projects:\n");

        var plan = new InitPlan
        {
            TemplateName = "wordpress",
            TemplatePath = templateDir,
            ProjectName = "mysite",
            DestinationPath = destDir,
            DapsYamlPath = dapsYaml,
            DapsYamlProjectRelativePath = "../mysite",
        };

        new InitService(new FakePlanBuilder(dapsYaml), new FakeDapsYamlEditor()).Execute(plan);

        Assert.True(File.Exists(Path.Combine(destDir, "README.md")));
        Assert.True(File.Exists(Path.Combine(destDir, "sub", "file.txt")));

        var destReadme = File.ReadAllText(Path.Combine(destDir, "README.md"));
        Assert.Equal("hello\nthere", destReadme.Trim());
    }

    [Fact]
    public void Execute_ThrowsIfDestinationAlreadyExists()
    {
        var templateDir = TestHelper.CreateTempDirectory();
        var destDir = TestHelper.CreateTempDirectory(); // already exists
        var dapsYaml = Path.Combine(TestHelper.CreateTempDirectory(), "daps.yaml");
        File.WriteAllText(dapsYaml, "projects:\n");

        var plan = new InitPlan
        {
            TemplateName = "wordpress",
            TemplatePath = templateDir,
            ProjectName = "mysite",
            DestinationPath = destDir,
            DapsYamlPath = dapsYaml,
            DapsYamlProjectRelativePath = "../mysite",
        };

        Assert.Throws<InvalidOperationException>(() =>
            new InitService(new FakePlanBuilder(dapsYaml), new FakeDapsYamlEditor()).Execute(plan));
    }

    [Fact]
    public void Execute_SkipsSecretsFolder()
    {
        var templateDir = TestHelper.CreateTempDirectory();
        var destDir = Path.Combine(Path.GetTempPath(), "dapsman-tests", Guid.NewGuid().ToString("N"));
        var dapsYaml = Path.Combine(TestHelper.CreateTempDirectory(), "daps.yaml");
        File.WriteAllText(dapsYaml, "projects:\n");

        // Put a fake secret in the template that will be copied to the destination and deleted
        var secretsDir = Path.Combine(templateDir, "_secrets");
        Directory.CreateDirectory(secretsDir);
        var secret = Path.Combine(secretsDir, "password.txt");
        File.WriteAllText(secret, "gobbledygook");

        var runner = new FakeBashRunner();
        var plan = new InitPlan
        {
            TemplateName = "wordpress",
            TemplatePath = templateDir,
            ProjectName = "mysite",
            DestinationPath = destDir,
            DapsYamlPath = dapsYaml,
            DapsYamlProjectRelativePath = "../mysite",
        };

        new InitService(new FakePlanBuilder(dapsYaml), new FakeDapsYamlEditor()).Execute(plan);

        Assert.False(Directory.Exists(Path.Combine(destDir, "_secrets")));
    }

    [Fact]
    public void Execute_CallsAddProjectOnEditor()
    {
        var templateDir = TestHelper.CreateTempDirectory();
        var destDir = Path.Combine(Path.GetTempPath(), "dapsman-tests", Guid.NewGuid().ToString("N"));
        var dapsYaml = Path.Combine(TestHelper.CreateTempDirectory(), "daps.yaml");
        File.WriteAllText(dapsYaml, "projects:\n  existing:\n    path: ../existing\n");

        var plan = new InitPlan
        {
            TemplateName = "wordpress",
            TemplatePath = templateDir,
            ProjectName = "mysite",
            DestinationPath = destDir,
            DapsYamlPath = dapsYaml,
            DapsYamlProjectRelativePath = "../mysite",
        };

        var editor = new FakeDapsYamlEditor();
        new InitService(new FakePlanBuilder(dapsYaml), editor).Execute(plan);

        Assert.Single(editor.AddCalls);
        Assert.Equal("mysite", editor.AddCalls[0].ProjectName);
        Assert.Equal("../mysite", editor.AddCalls[0].RelativePath);
    }

    private sealed class FakePlanBuilder(string dapsYamlPath) : IInitPlanBuilder
    {
        public bool Called { get; private set; }

        public InitPlan BuildInitPlan(InitOptions options)
        {
            Called = true;
            return new InitPlan
            {
                TemplateName = options.TemplateName,
                TemplatePath = ".",
                ProjectName = options.ProjectName,
                DestinationPath = Path.Combine(Path.GetTempPath(), options.ProjectName),
                DapsYamlPath = dapsYamlPath,
                DapsYamlProjectRelativePath = $"../{options.ProjectName}",
            };
        }
    }
}
