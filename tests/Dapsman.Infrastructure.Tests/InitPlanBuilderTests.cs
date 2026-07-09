using Dapsman.Application;

namespace Dapsman.Infrastructure.Tests;

public sealed class InitPlanBuilderTests
{
	[Fact]
	public void BuildInitPlan_ResolvesTemplateFromDapsRoot()
	{
		var root = CreateTempDapsRoot("wordpress");
		var dapsYaml = Path.Combine(root, "daps.yaml");
		var config = new Domain.DapsConfig { DapsRootPath = root, FullYamlPath = dapsYaml };
		File.WriteAllText(dapsYaml, "projects:\n");

		var plan = new InitPlanBuilder(config, new ProjectResolver(config), new DockerResolver(config), new FakeHostPortManager()).BuildInitPlan(new InitOptions
		{
			TemplateName = "wordpress",
			ProjectName = "mysite",
		});

		Assert.Equal(Path.Combine(root, "templates", "wordpress"), plan.TemplatePath);
	}

	[Fact]
	public void BuildInitPlan_DefaultDestinationIsSiblingOfDapsRoot()
	{
		var root = CreateTempDapsRoot("wordpress");
		var dapsYaml = Path.Combine(root, "daps.yaml");
		var config = new Domain.DapsConfig { DapsRootPath = root, FullYamlPath = dapsYaml };
		File.WriteAllText(dapsYaml, "projects:\n");

		var plan = new InitPlanBuilder(config, new ProjectResolver(config), new DockerResolver(config), new FakeHostPortManager()).BuildInitPlan(new InitOptions
		{
			TemplateName = "wordpress",
			ProjectName = "mysite",
		});

		var expectedParent = Path.GetDirectoryName(root)!;
		Assert.Equal(Path.Combine(expectedParent, "mysite"), plan.DestinationPath);
	}

	[Fact]
	public void BuildInitPlan_ExplicitDestinationOverridesDefault()
	{
		var root = CreateTempDapsRoot("wordpress");
		var dapsYaml = Path.Combine(root, "daps.yaml");
		var config = new Domain.DapsConfig { DapsRootPath = root, FullYamlPath = dapsYaml };
		File.WriteAllText(dapsYaml, "projects:\n");
		var customDest = Path.Combine(Path.GetTempPath(), "custom-dest-" + Guid.NewGuid().ToString("N"));

		var plan = new InitPlanBuilder(config, new ProjectResolver(config), new DockerResolver(config), new FakeHostPortManager()).BuildInitPlan(new InitOptions
		{
			TemplateName = "wordpress",
			ProjectName = "mysite",
			DestinationPath = customDest,
		});

		Assert.Equal(customDest, plan.DestinationPath);
	}

	[Fact]
	public void BuildInitPlan_DetectsInitScriptWhenPresent()
	{
		var root = CreateTempDapsRoot("wordpress");
		var scriptPath = Path.Combine(root, "templates", "wordpress", "_scripts", "init-template.toolkit.sh");
		Directory.CreateDirectory(Path.GetDirectoryName(scriptPath)!);
		File.WriteAllText(scriptPath, "#!/usr/bin/env bash\n");
		var dapsYaml = Path.Combine(root, "daps.yaml");
		var config = new Domain.DapsConfig { DapsRootPath = root, FullYamlPath = dapsYaml };
		File.WriteAllText(dapsYaml, "projects:\n");

		var plan = new InitPlanBuilder(config, new ProjectResolver(config), new DockerResolver(config), new FakeHostPortManager()).BuildInitPlan(new InitOptions
		{
			TemplateName = "wordpress",
			ProjectName = "mysite",
		});

		Assert.NotNull(plan.InitScriptPath);
		Assert.EndsWith("init-template.toolkit.sh", plan.InitScriptPath, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void BuildInitPlan_InitScriptPathIsNullWhenAbsent()
	{
		var root = CreateTempDapsRoot("wordpress");
		var dapsYaml = Path.Combine(root, "daps.yaml");
		var config = new Domain.DapsConfig { DapsRootPath = root, FullYamlPath = dapsYaml };
		File.WriteAllText(dapsYaml, "projects:\n");

		var plan = new InitPlanBuilder(config, new ProjectResolver(config), new DockerResolver(config), new FakeHostPortManager()).BuildInitPlan(new InitOptions
		{
			TemplateName = "wordpress",
			ProjectName = "mysite",
		});

		Assert.Null(plan.InitScriptPath);
	}

	[Fact]
	public void BuildInitPlan_RelativePathIsSiblingDotDot()
	{
		var root = CreateTempDapsRoot("wordpress");
		var dapsYaml = Path.Combine(root, "daps.yaml");
		var config = new Domain.DapsConfig { DapsRootPath = root, FullYamlPath = dapsYaml };
		File.WriteAllText(dapsYaml, "projects:\n");

		var plan = new InitPlanBuilder(config, new ProjectResolver(config), new DockerResolver(config), new FakeHostPortManager()).BuildInitPlan(new InitOptions
		{
			TemplateName = "wordpress",
			ProjectName = "mysite",
		});

		Assert.Equal("../mysite", plan.DapsYamlProjectRelativePath);
	}

	[Fact]
	public void BuildInitPlan_ThrowsWhenTemplateNotFound()
	{
		var root = Path.Combine(Path.GetTempPath(), "dapsman-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(root);
		var dapsYaml = Path.Combine(root, "daps.yaml");
		var config = new Domain.DapsConfig { DapsRootPath = root, FullYamlPath = dapsYaml };
		File.WriteAllText(dapsYaml, "projects:\n");

		var ex = Assert.Throws<InvalidOperationException>(() =>
			new InitPlanBuilder(config, new ProjectResolver(config), new DockerResolver(config), new FakeHostPortManager()).BuildInitPlan(new InitOptions
			{
				TemplateName = "nonexistent",
				ProjectName = "mysite",
			}));

		Assert.Contains("Template not found", ex.Message, StringComparison.OrdinalIgnoreCase);
	}

	private static string CreateTempDapsRoot(string templateName)
	{
		var root = Path.Combine(Path.GetTempPath(), "dapsman-tests", Guid.NewGuid().ToString("N"));
		var templateDir = Path.Combine(root, "templates", templateName);
		Directory.CreateDirectory(templateDir);
		return root;
	}
}
