using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure.Tests;

public sealed class LocalBackupPlanBuilderTests
{
	[Fact]
	public void BuildPlan_ScriptPresent_PointsAtTheProjectsToolkitScript()
	{
		var plan = BuildPlan(withBackupScript: true).Plan!;

		Assert.Equal("/srv/projects/alpha/_scripts/backup-local.toolkit.sh", plan.BackupScriptToolkitPath);
	}

	[Fact]
	public void BuildPlan_ScriptPresent_WritesToFromLocal()
	{
		var plan = BuildPlan(withBackupScript: true).Plan!;

		Assert.Equal("/srv/projects/alpha/_backups/from_local", plan.BackupDestinationToolkitPath);
	}

	[Fact]
	public void BuildPlan_ScriptPresent_PassesTheLocalEnvToTheScript()
	{
		var plan = BuildPlan(withBackupScript: true).Plan!;

		Assert.Equal("--env local --to \"/srv/projects/alpha/_backups/from_local\"", plan.ScriptArguments);
	}

	[Fact]
	public void BuildPlan_ScriptPresent_PassesTheProjectNameAsTheOnlyEnvVar()
	{
		var plan = BuildPlan(withBackupScript: true).Plan!;

		Assert.Equal(new Dictionary<string, string> { ["DAPS_PROJECT"] = "alpha" }, plan.EnvVars);
	}

	[Fact]
	public void BuildPlan_NoScript_IsNotSupported()
	{
		var result = BuildPlan(withBackupScript: false);

		Assert.False(result.IsSupported);
	}

	[Fact]
	public void BuildPlan_NoScript_NamesTheMissingScriptInTheReason()
	{
		var result = BuildPlan(withBackupScript: false);

		Assert.Contains(result.Warnings, w => w.Contains(LocalBackupPlanBuilder.BackupLocalScript));
	}

	[Fact]
	public void BuildPlan_NoToolkitBindMount_IsNotSupported()
	{
		var result = BuildPlan(withBackupScript: true, withToolkitBindMount: false);

		Assert.False(result.IsSupported);
	}

	private static PlanResult<LocalBackupPlan> BuildPlan(bool withBackupScript, bool withToolkitBindMount = true)
	{
		var root = Path.Combine(Path.GetTempPath(), "dapsman-tests", Guid.NewGuid().ToString("N"));
		var projectRoot = Path.Combine(root, "alpha-project");
		var dockerRoot = Path.Combine(projectRoot, "_docker");
		Directory.CreateDirectory(dockerRoot);
		File.WriteAllText(Path.Combine(dockerRoot, "compose_alpha.yaml"), "services: {}");
		// The compose file itself must exist — ProjectResolver throws without it. ToolkitPath is
		// null when it exists but mounts nothing whose target matches the project name.
		File.WriteAllText(Path.Combine(dockerRoot, "compose_daps_alpha.dev.yaml"), withToolkitBindMount
			? """
services:
  toolkit:
    volumes:
      - ../../alpha-project:/srv/projects/alpha
"""
			: """
services:
  toolkit:
    volumes:
      - ../../alpha-project:/srv/somewhere-else
""");

		var scriptsRoot = Path.Combine(projectRoot, "_scripts");
		Directory.CreateDirectory(scriptsRoot);
		if (withBackupScript)
		{
			File.WriteAllText(Path.Combine(scriptsRoot, LocalBackupPlanBuilder.BackupLocalScript), "#!/usr/bin/env bash\n");
		}

		var config = new DapsConfig
		{
			DapsRootPath = root,
			FullYamlPath = Path.Combine(root, "daps.yaml"),
			Projects = new[] { new ProjectDefinition { Name = "alpha", Path = projectRoot } },
		};

		var project = new ProjectResolver(config).Resolve("alpha");
		var builder = new LocalBackupPlanBuilder(new ToolkitResolver(config, new FakeContainerManager(null)));

		return builder.BuildPlan(project, new BackupOptions { ProjectName = "alpha" });
	}
}
