using Dapsman.Domain;

namespace Dapsman.Application.Tests;

public sealed class InitServiceTests
{
    [Fact]
    public void CreatePlan_DelegatesToPlanBuilder()
    {
        var builder = new FakePlanBuilder("daps.yaml");
        var service = new InitService(builder, new FakeBashRunner());

        var plan = service.CreatePlan(new InitOptions
        {
            TemplateName = "wordpress",
            ProjectName = "mysite",
        });

        Assert.True(builder.Called);
        Assert.Equal("mysite", plan.ProjectName);
    }

    [Fact]
    public void Execute_CopiesTemplateFilesToDestination()
    {
        var templateDir = CreateTempDirectory();
        File.WriteAllText(Path.Combine(templateDir, "README.md"), "hello");
        var subDir = Path.Combine(templateDir, "sub");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "file.txt"), "content");

        var destDir = Path.Combine(Path.GetTempPath(), "dapsman-tests", Guid.NewGuid().ToString("N"));
        var dapsYaml = Path.Combine(CreateTempDirectory(), "daps.yaml");
        File.WriteAllText(dapsYaml, "projects:\n");

        var plan = new InitPlan
        {
            TemplateName = "wordpress",
            TemplatePath = templateDir,
            ProjectName = "mysite",
            DestinationPath = destDir,
            DapsYamlPath = dapsYaml,
            DapsYamlProjectRelativePath = "../mysite",
            InitScriptPath = null,
        };

        new InitService(new FakePlanBuilder(dapsYaml), new FakeBashRunner()).Execute(plan);

        Assert.True(File.Exists(Path.Combine(destDir, "README.md")));
        Assert.True(File.Exists(Path.Combine(destDir, "sub", "file.txt")));
    }

    [Fact]
    public void Execute_ThrowsIfDestinationAlreadyExists()
    {
        var templateDir = CreateTempDirectory();
        var destDir = CreateTempDirectory(); // already exists
        var dapsYaml = Path.Combine(CreateTempDirectory(), "daps.yaml");
        File.WriteAllText(dapsYaml, "projects:\n");

        var plan = new InitPlan
        {
            TemplateName = "wordpress",
            TemplatePath = templateDir,
            ProjectName = "mysite",
            DestinationPath = destDir,
            DapsYamlPath = dapsYaml,
            DapsYamlProjectRelativePath = "../mysite",
            InitScriptPath = null,
        };

        Assert.Throws<InvalidOperationException>(() =>
            new InitService(new FakePlanBuilder(dapsYaml), new FakeBashRunner()).Execute(plan));
    }

    [Fact]
    public void Execute_RunsInitScriptWithProjectNameAsArgument()
    {
        var templateDir = CreateTempDirectory();
        var destDir = Path.Combine(Path.GetTempPath(), "dapsman-tests", Guid.NewGuid().ToString("N"));
        var dapsYaml = Path.Combine(CreateTempDirectory(), "daps.yaml");
        File.WriteAllText(dapsYaml, "projects:\n");

        // Put a fake init script in the template that will be copied to the destination
        var scriptsDir = Path.Combine(templateDir, "_scripts");
        Directory.CreateDirectory(scriptsDir);
        var initScript = Path.Combine(scriptsDir, "init-template.toolkit.sh");
        File.WriteAllText(initScript, "#!/usr/bin/env bash\n");

        var runner = new FakeBashRunner();
        var plan = new InitPlan
        {
            TemplateName = "wordpress",
            TemplatePath = templateDir,
            ProjectName = "mysite",
            DestinationPath = destDir,
            DapsYamlPath = dapsYaml,
            DapsYamlProjectRelativePath = "../mysite",
            InitScriptPath = initScript,
        };

        new InitService(new FakePlanBuilder(dapsYaml), runner).Execute(plan);

        Assert.Single(runner.ScriptCalls);
        Assert.Equal("mysite", runner.ScriptCalls[0].Arguments);
        // Script path is relative to destination, not template
        Assert.StartsWith(destDir, runner.ScriptCalls[0].ScriptPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Execute_RegistersProjectInDapsYaml()
    {
        var templateDir = CreateTempDirectory();
        var destDir = Path.Combine(Path.GetTempPath(), "dapsman-tests", Guid.NewGuid().ToString("N"));
        var dapsYaml = Path.Combine(CreateTempDirectory(), "daps.yaml");
        File.WriteAllText(dapsYaml, "projects:\n  existing:\n    path: ../existing\n");

        var plan = new InitPlan
        {
            TemplateName = "wordpress",
            TemplatePath = templateDir,
            ProjectName = "mysite",
            DestinationPath = destDir,
            DapsYamlPath = dapsYaml,
            DapsYamlProjectRelativePath = "../mysite",
            InitScriptPath = null,
        };

        new InitService(new FakePlanBuilder(dapsYaml), new FakeBashRunner()).Execute(plan);

        var yaml = File.ReadAllText(dapsYaml);
        Assert.Contains("mysite:", yaml);
        Assert.Contains("../mysite", yaml);
        Assert.Contains("existing:", yaml); // original entry preserved
    }

    [Fact]
    public void Execute_ThrowsIfProjectAlreadyRegisteredInDapsYaml()
    {
        var templateDir = CreateTempDirectory();
        var destDir = Path.Combine(Path.GetTempPath(), "dapsman-tests", Guid.NewGuid().ToString("N"));
        var dapsYaml = Path.Combine(CreateTempDirectory(), "daps.yaml");
        File.WriteAllText(dapsYaml, "projects:\n  mysite:\n    path: ../mysite\n");

        var plan = new InitPlan
        {
            TemplateName = "wordpress",
            TemplatePath = templateDir,
            ProjectName = "mysite",
            DestinationPath = destDir,
            DapsYamlPath = dapsYaml,
            DapsYamlProjectRelativePath = "../mysite",
            InitScriptPath = null,
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            new InitService(new FakePlanBuilder(dapsYaml), new FakeBashRunner()).Execute(plan));

        Assert.Contains("already registered", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "dapsman-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
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
                InitScriptPath = null,
            };
        }
    }
}
