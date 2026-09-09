using Dapsman.Application;

namespace Dapsman.Infrastructure.Tests;

public sealed class InitPlanBuilderTests
{
	[Fact]
	public void BuildInitPlan_ResolvesTemplateFromDapsRoot()
	{
		var root = CreateTempDapsRoot("wordpress");
		WriteTemplateYamlFile(root, "wordpress", "mywpsite", new[] {"wp-content", "_secrets", "_backup"});

		var dapsYaml = Path.Combine(root, "daps.yaml");
		var config = new Domain.DapsConfig { DapsRootPath = root, FullYamlPath = dapsYaml };
		File.WriteAllText(dapsYaml, "projects:\n");

		var plan = new InitPlanBuilder(config, new ProjectResolver(config), new DockerResolver(config), new FakeHostPortManager(), new TemplateConfigLoader()).BuildInitPlan(new InitOptions
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
		WriteTemplateYamlFile(root, "wordpress", "mywpsite", new[] {"wp-content", "_secrets", "_backup"});

		var dapsYaml = Path.Combine(root, "daps.yaml");
		var config = new Domain.DapsConfig { DapsRootPath = root, FullYamlPath = dapsYaml };
		File.WriteAllText(dapsYaml, "projects:\n");

		var plan = new InitPlanBuilder(config, new ProjectResolver(config), new DockerResolver(config), new FakeHostPortManager(), new TemplateConfigLoader()).BuildInitPlan(new InitOptions
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
		WriteTemplateYamlFile(root, "wordpress", "mywpsite", new[] {"wp-content", "_secrets", "_backup"});

		var dapsYaml = Path.Combine(root, "daps.yaml");
		var config = new Domain.DapsConfig { DapsRootPath = root, FullYamlPath = dapsYaml };
		File.WriteAllText(dapsYaml, "projects:\n");
		var customDest = Path.Combine(Path.GetTempPath(), "custom-dest-" + Guid.NewGuid().ToString("N"));

		var plan = new InitPlanBuilder(config, new ProjectResolver(config), new DockerResolver(config), new FakeHostPortManager(), new TemplateConfigLoader()).BuildInitPlan(new InitOptions
		{
			TemplateName = "wordpress",
			ProjectName = "mysite",
			DestinationPath = customDest,
		});

		Assert.Equal(customDest, plan.DestinationPath);
	}

	[Fact]
	public void BuildInitPlan_DerivesExcludedFoldersFromTemplateYaml()
	{
		var root = CreateTempDapsRoot("wordpress");
		WriteTemplateYamlFile(root, "wordpress", "mywpsite", new[] {"wp-content", "_secrets", "_backup"});
		
		var dapsYaml = Path.Combine(root, "daps.yaml");
		var config = new Domain.DapsConfig { DapsRootPath = root, FullYamlPath = dapsYaml };
		File.WriteAllText(dapsYaml, "projects:\n");

		var plan = new InitPlanBuilder(config, new ProjectResolver(config), new DockerResolver(config), new FakeHostPortManager(), new TemplateConfigLoader()).BuildInitPlan(new InitOptions
		{
			TemplateName = "wordpress",
			ProjectName = "mysite",
		});

		Assert.NotNull(plan.ExcludedFromTemplateTransform);
		Assert.Equal("wp-content", plan.ExcludedFromTemplateTransform[0]);
	}

	[Fact]
	public void BuildInitPlan_DerivesPlaceholderTextFromTemplateYaml()
	{
		var root = CreateTempDapsRoot("wordpress");
		WriteTemplateYamlFile(root, "wordpress", "mywpsite", new[] {"wp-content", "_secrets", "_backup"});

		var dockerPath = Path.Combine(root, "templates", "wordpress", "_docker");
		Directory.CreateDirectory(dockerPath!);

		var dapsYaml = Path.Combine(root, "daps.yaml");
		var config = new Domain.DapsConfig { DapsRootPath = root, FullYamlPath = dapsYaml };
		File.WriteAllText(dapsYaml, "projects:\n");

		var plan = new InitPlanBuilder(config, new ProjectResolver(config), new DockerResolver(config), new FakeHostPortManager(), new TemplateConfigLoader()).BuildInitPlan(new InitOptions
		{
			TemplateName = "wordpress",
			ProjectName = "mysite",
		});

		Assert.NotNull(plan.PlaceholderText);
		Assert.Equal("mywpsite", plan.PlaceholderText);
	}

	[Fact]
	public void BuildInitPlan_WhenPlaceholderIsNotInTemplateYaml_DerivesPlaceholderTextFromProjectDapsDockerComposeFile()
	{
		var root = CreateTempDapsRoot("wordpress");
		WriteTemplateYamlFile(root, "wordpress", null, new[] {"wp-content", "_secrets", "_backup"});

		var dockerPath = Path.Combine(root, "templates", "wordpress", "_docker");
		Directory.CreateDirectory(dockerPath!);
		File.WriteAllText(Path.Combine(dockerPath, "compose_daps_mywpsite.dev.yaml"), "hey");
		File.WriteAllText(Path.Combine(dockerPath, "compose_mywpsite.dev.yaml"), "hey");
		File.WriteAllText(Path.Combine(dockerPath, "compose_mywpsite.prod.yaml"), "hey");

		var dapsYaml = Path.Combine(root, "daps.yaml");
		var config = new Domain.DapsConfig { DapsRootPath = root, FullYamlPath = dapsYaml };
		File.WriteAllText(dapsYaml, "projects:\n");

		var plan = new InitPlanBuilder(config, new ProjectResolver(config), new DockerResolver(config), new FakeHostPortManager(), new TemplateConfigLoader()).BuildInitPlan(new InitOptions
		{
			TemplateName = "wordpress",
			ProjectName = "mysite",
		});

		Assert.NotNull(plan.PlaceholderText);
		Assert.Equal("mywpsite", plan.PlaceholderText);
	}

	[Fact]
	public void BuildInitPlan_InitScriptPathIsNullWhenAbsent()
	{
		var root = CreateTempDapsRoot("wordpress");
		WriteTemplateYamlFile(root, "wordpress", "mywpsite", new[] {"wp-content", "_secrets", "_backup"});

		var dapsYaml = Path.Combine(root, "daps.yaml");
		var config = new Domain.DapsConfig { DapsRootPath = root, FullYamlPath = dapsYaml };
		File.WriteAllText(dapsYaml, "projects:\n");

		var plan = new InitPlanBuilder(config, new ProjectResolver(config), new DockerResolver(config), new FakeHostPortManager(), new TemplateConfigLoader()).BuildInitPlan(new InitOptions
		{
			TemplateName = "wordpress",
			ProjectName = "mysite",
		});
	}

	[Fact]
	public void BuildInitPlan_RelativePathIsSiblingDotDot()
	{
		var root = CreateTempDapsRoot("wordpress");
		WriteTemplateYamlFile(root, "wordpress", "mywpsite", new[] {"wp-content", "_secrets", "_backup"});

		var dapsYaml = Path.Combine(root, "daps.yaml");
		var config = new Domain.DapsConfig { DapsRootPath = root, FullYamlPath = dapsYaml };
		File.WriteAllText(dapsYaml, "projects:\n");

		var plan = new InitPlanBuilder(config, new ProjectResolver(config), new DockerResolver(config), new FakeHostPortManager(), new TemplateConfigLoader()).BuildInitPlan(new InitOptions
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
			new InitPlanBuilder(config, new ProjectResolver(config), new DockerResolver(config), new FakeHostPortManager(), new TemplateConfigLoader()).BuildInitPlan(new InitOptions
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

	private static void WriteTemplateYamlFile(string root, string templateName, string? placeholderText = null, string[]? exclusions = null)
	{
		var templateYamlPath = Path.Combine(root, "templates", templateName, "template.yaml");
		Directory.CreateDirectory(Path.GetDirectoryName(templateYamlPath)!);
        var yaml = string.Empty;

		if (placeholderText is not null)
		{
			yaml = $"placeholder: {placeholderText}\n";
		}
		if (exclusions is not null)
		{
			yaml += $"exclude-from-transform: [{string.Join(',', exclusions)}]\n";
		}

		File.WriteAllText(templateYamlPath, yaml);
	}
}
