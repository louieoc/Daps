using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure.Tests;

public sealed class RemoteDeployPlanBuilderTests
{
	[Fact]
	public void BuildRemoteDeployPlan_UsesDapsOrderAndAllProjectsForStaleCaddyBaseline()
	{
		var root = CreateTempDirectory();
		Directory.CreateDirectory(Path.Combine(root, "hosting"));
		Directory.CreateDirectory(Path.Combine(root, "caddy"));
		Directory.CreateDirectory(Path.Combine(root, "docker"));
		Directory.CreateDirectory(Path.Combine(root, "secrets", "ssh"));
		File.WriteAllText(Path.Combine(root, "caddy", "Caddyfile"), "import /etc/caddy/sites/*.caddy");
		File.WriteAllText(Path.Combine(root, "docker", "compose_daps.yaml"), "services: {}");
		File.WriteAllText(Path.Combine(root, "hosting", "openstack_ramnode_instance_vars.sh"), """
export OS_SERVER_IP="10.0.0.20"
export OS_SERVER_USER="root"
export OS_KEY_NAME="daps-key-ramnode"
""");
		File.WriteAllText(Path.Combine(root, "hosting", "ramnode_openrc"), "# openrc stub");

		var alpha = CreateProject(root, "alpha-project", "alpha", includeProdCaddy: true);
		var beta = CreateProject(root, "beta-project", "beta", includeProdCaddy: true, includeUploadManifest: true);

		var config = new DapsConfig
		{
			DapsRootPath = root,
			FullYamlPath = Path.Combine(root, "daps.yaml"),
			Providers =
			[
				new OpenstackProviderDefinition
				{
					Name = "ramnode",
					OpenRcPath = Path.Combine(root, "hosting", "ramnode_openrc"),
				},
			],
			Projects = new[]
			{
				new ProjectDefinition { Name = "alpha", Path = alpha },
				new ProjectDefinition { Name = "beta", Path = beta },
			},
		};

		var containerManager = new FakeContainerManager(null);
		var planner = new RemoteDeployPlanBuilder(
			new HostingProviderResolver(config),
			new DockerResolver(config),
			new ToolkitResolver(config, containerManager),
			new CaddyResolver(config, containerManager),
			new ProjectResolver(config)
		);
		var plan = planner.BuildRemoteDeployPlan(config, new RemoteDeployOptions
		{
			ProviderName = "ramnode",
			ProjectFilters = new[] { "beta" },
		});

		Assert.Single(plan.ProjectPlans);
		Assert.Equal("beta", plan.ProjectPlans[0].ProjectName);
		Assert.Single(plan.ProjectPlans[0].UploadFiles);
		Assert.EndsWith(Path.Combine("beta-project", "secrets", "upload.txt"), plan.ProjectPlans[0].UploadFiles[0].SourcePath, StringComparison.OrdinalIgnoreCase);
		Assert.Equal("/srv/projects/beta/upload.txt", plan.ProjectPlans[0].UploadFiles[0].RemoteDestinationPath);
		Assert.Contains("alpha.prod.caddy", plan.ExpectedProdCaddyFileNames);
		Assert.Contains("beta.prod.caddy", plan.ExpectedProdCaddyFileNames);
		Assert.Equal("daps-toolkit-1", plan.ToolkitContainerName);
	}

	[Fact]
	public void BuildRemoteDeployPlan_UsesDefaultKeyNameWhenNotSet()
	{
		var root = CreateTempDirectory();
		Directory.CreateDirectory(Path.Combine(root, "hosting"));
		Directory.CreateDirectory(Path.Combine(root, "caddy"));
		Directory.CreateDirectory(Path.Combine(root, "docker"));
		Directory.CreateDirectory(Path.Combine(root, "secrets", "ssh"));
		File.WriteAllText(Path.Combine(root, "caddy", "Caddyfile"), "import /etc/caddy/sites/*.caddy");
		File.WriteAllText(Path.Combine(root, "docker", "compose_daps.yaml"), "services: {}");
		File.WriteAllText(Path.Combine(root, "hosting", "openstack_ramnode_instance_vars.sh"), """
export OS_SERVER_IP="10.0.0.20"
""");
		File.WriteAllText(Path.Combine(root, "hosting", "ramnode_openrc"), "# openrc stub");

		var config = new DapsConfig
		{
			DapsRootPath = root,
			FullYamlPath = Path.Combine(root, "daps.yaml"),
			Providers =
			[
				new OpenstackProviderDefinition
				{
					Name = "ramnode",
					OpenRcPath = Path.Combine(root, "hosting", "ramnode_openrc"),
				},
			],
			Projects = Array.Empty<ProjectDefinition>(),
		};

		var containerManager = new FakeContainerManager(null);
		var planner = new RemoteDeployPlanBuilder(
			new HostingProviderResolver(config),
			new DockerResolver(config),
			new ToolkitResolver(config, containerManager),
			new CaddyResolver(config, containerManager),
			new ProjectResolver(config)
		);
		var plan = planner.BuildRemoteDeployPlan(config, new RemoteDeployOptions
		{
			ProviderName = "ramnode",
		});

		Assert.Equal("daps-key-ramnode", plan.SshKeyName);
	}

	[Fact]
	public void BuildRemoteDeployPlan_StripsInlineCommentsFromExportValues()
	{
		var root = CreateTempDirectory();
		Directory.CreateDirectory(Path.Combine(root, "hosting"));
		Directory.CreateDirectory(Path.Combine(root, "caddy"));
		Directory.CreateDirectory(Path.Combine(root, "docker"));
		Directory.CreateDirectory(Path.Combine(root, "secrets", "ssh"));
		File.WriteAllText(Path.Combine(root, "caddy", "Caddyfile"), "import /etc/caddy/sites/*.caddy");
		File.WriteAllText(Path.Combine(root, "docker", "compose_daps.yaml"), "services: {}");
		File.WriteAllText(Path.Combine(root, "hosting", "openstack_ramnode_instance_vars.sh"), """
export OS_SERVER_IP=10.0.0.20 # comment
export OS_KEY_NAME=daps-key-ramnode # another comment
""");
		File.WriteAllText(Path.Combine(root, "hosting", "ramnode_openrc"), "# openrc stub");

		var config = new DapsConfig
		{
			DapsRootPath = root,
			FullYamlPath = Path.Combine(root, "daps.yaml"),
			Providers =
			[
				new OpenstackProviderDefinition
				{
					Name = "ramnode",
					OpenRcPath = Path.Combine(root, "hosting", "ramnode_openrc"),
				},
			],
			Projects = Array.Empty<ProjectDefinition>(),
		};

		var containerManager = new FakeContainerManager(null);
		var planner = new RemoteDeployPlanBuilder(
			new HostingProviderResolver(config),
			new DockerResolver(config),
			new ToolkitResolver(config, containerManager),
			new CaddyResolver(config, containerManager),
			new ProjectResolver(config)
		);
		var plan = planner.BuildRemoteDeployPlan(config, new RemoteDeployOptions
		{
			ProviderName = "ramnode",
		});

		Assert.Equal("10.0.0.20", plan.RemoteHost);
		Assert.Equal("daps-key-ramnode", plan.SshKeyName);
	}

	private static string CreateProject(string root, string dirName, string projectName, bool includeProdCaddy, bool includeUploadManifest = false)
	{
		var projectRoot = Path.Combine(root, dirName);
		var dockerRoot = Path.Combine(projectRoot, "_docker");
		Directory.CreateDirectory(dockerRoot);
		Directory.CreateDirectory(Path.Combine(dockerRoot, "image-exports"));
		File.WriteAllText(Path.Combine(dockerRoot, $"compose_{projectName}.yaml"), "services: {}");
		File.WriteAllText(Path.Combine(dockerRoot, $"compose_{projectName}.prod.yaml"), "services: {}");
		File.WriteAllText(Path.Combine(dockerRoot, $"compose_daps_{projectName}.dev.yaml"), """
services:
  toolkit:
    volumes:
      - ../_scripts:/srv/projects/placeholder/_scripts
""");

		var scriptsRoot = Path.Combine(projectRoot, "_scripts");
		Directory.CreateDirectory(scriptsRoot);
		File.WriteAllText(Path.Combine(scriptsRoot, "build-docker-images.toolkit.sh"), "#!/usr/bin/env bash\n");
		if (includeUploadManifest)
		{
			var secretsRoot = Path.Combine(projectRoot, "secrets");
			Directory.CreateDirectory(secretsRoot);
			File.WriteAllText(Path.Combine(secretsRoot, "upload.txt"), "secret");
			File.WriteAllText(Path.Combine(scriptsRoot, "deploy-prod-uploads"), "\"../secrets/upload.txt\" \"/srv/projects/beta/upload.txt\"\n");
		}

		var caddyRoot = Path.Combine(projectRoot, "_caddy_sites");
		Directory.CreateDirectory(caddyRoot);
		if (includeProdCaddy)
		{
			File.WriteAllText(Path.Combine(caddyRoot, $"{projectName}.prod.caddy"), "# caddy");
		}

		return projectRoot;
	}

	private static string CreateTempDirectory()
	{
		var path = Path.Combine(Path.GetTempPath(), "dapsman-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(path);
		return path;
	}
}
