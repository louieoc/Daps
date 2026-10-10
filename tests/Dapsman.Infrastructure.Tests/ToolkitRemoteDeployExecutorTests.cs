using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure.Tests;

public sealed class ToolkitRemoteDeployExecutorTests
{
	[Fact]
	public void ExecuteBuildImages_PassesTheProjectNameToEachScript()
	{
		var runner = new FakeBashRunner();
		var executor = new ToolkitRemoteDeployExecutor(runner);

		executor.ExecuteBuildImages(
			[
				Command("alpha"),
				Command("beta"),
			],
			dapsRootPath: "/srv/daps",
			targetPlatform: null);

		Assert.Collection(
			runner.ScriptRuns,
			run => Assert.Equal("alpha", run.Env!["DAPS_PROJECT"]),
			run => Assert.Equal("beta", run.Env!["DAPS_PROJECT"]));

		// Absent, not empty: a script that reads it should see it unset for a local-native build.
		Assert.DoesNotContain("DAPS_TARGET_PLATFORM", runner.ScriptRuns[0].Env!.Keys);
	}

	[Fact]
	public void ExecuteBuildImages_PassesTheTargetPlatformAlongsideTheProjectName()
	{
		var runner = new FakeBashRunner();
		var executor = new ToolkitRemoteDeployExecutor(runner);

		executor.ExecuteBuildImages([Command("alpha")], "/srv/daps", "linux/amd64");

		var env = Assert.Single(runner.ScriptRuns).Env!;
		Assert.Equal("alpha", env["DAPS_PROJECT"]);
		Assert.Equal("linux/amd64", env["DAPS_TARGET_PLATFORM"]);
	}

	[Fact]
	public void Execute_MakesTheProjectScriptsDirectoryWritableByTheRemoteUser()
	{
		// The scripts are scp'd as the remote user. When _scripts is created with sudo after
		// the project directory is chowned it stays root-owned and the upload fails with
		// "Permission denied", leaving the deploy half-finished.
		var script = GenerateDeployScript(isRoot: false);

		var mkdirLine = Assert.Single(
			script.Split('\n'),
			line => line.Contains("mkdir -p") && line.Contains("/srv/projects/alpha/_docker"));
		Assert.Contains("/srv/projects/alpha/_scripts", mkdirLine);
		Assert.Contains("sudo chown -R \"$(id -un)\" /srv/projects/alpha", mkdirLine);
	}

	[Fact]
	public void Execute_InstallsUploadsWithSudoWithoutTakingOwnershipOfTheirDirectory()
	{
		// An upload destination can be any absolute path. Chowning its directory would hand
		// e.g. /etc to the remote user, so only the file itself is given to them.
		var script = GenerateDeployScript(isRoot: false);

		Assert.Contains("sudo install -o \"$(id -un)\" -m 600", script);
		Assert.DoesNotContain("chown \"$(id -un)\" \"$(dirname", script);
	}

	[Fact]
	public void Execute_DoesNotUseSudoWhenTheRemoteUserIsRoot()
	{
		var script = GenerateDeployScript(isRoot: true);

		Assert.DoesNotContain("sudo", script);
	}

	[Fact]
	public void Execute_UploadsImageExportsSavedAfterThePlanWasBuilt()
	{
		// The plan is built before build-images runs. On a project's first deploy it lists no
		// exports, and the remote compose up then tried to pull the image from a registry.
		var script = GenerateDeployScript(isRoot: false, exportsSavedAfterPlanning: ["alpha-web.tar"]);

		Assert.Contains("/_docker/image-exports/alpha-web.tar\" \"$remote:/srv/projects/alpha/_docker/image-exports/alpha-web.tar\"", script);
	}

	private static string GenerateDeployScript(bool isRoot, string[]? exportsSavedAfterPlanning = null)
	{
		var dapsRoot = Path.Combine(Path.GetTempPath(), "dapsman-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dapsRoot);
		try
		{
			var imageExports = Path.Combine(dapsRoot, "image-exports");
			Directory.CreateDirectory(imageExports);
			foreach (var name in exportsSavedAfterPlanning ?? [])
				File.WriteAllText(Path.Combine(imageExports, name), "tar");

			var caddyfile = Path.Combine(dapsRoot, "Caddyfile");
			File.WriteAllText(caddyfile, "# test\n");
			var prerequisites = Path.Combine(dapsRoot, "prerequisites.prod.sh");
			File.WriteAllText(prerequisites, "#!/usr/bin/env bash\n");
			var uploadSource = Path.Combine(dapsRoot, "credentials.json");
			File.WriteAllText(uploadSource, "{}");

			string? captured = null;
			var runner = new CapturingBashRunner(() =>
				captured = File.ReadAllText(Directory.GetFiles(dapsRoot, "deploy.sh", SearchOption.AllDirectories).Single()));

			new ToolkitRemoteDeployExecutor(runner).Execute(new RemoteDeployPlan
			{
				DapsRootPath = dapsRoot,
				ProviderName = "testprovider",
				ToolkitContainerName = "daps-toolkit-1",
				RemoteHost = "host.example",
				RemoteUser = isRoot ? "root" : "ubuntu",
				SshKeyName = "daps-key-test",
				IsRoot = isRoot,
				BuildImageCommands = [],
				SelectedBuildImageCommands = [],
				CaddyfileSourcePath = caddyfile,
				CaddySiteFilesToUpload = [],
				ExpectedProdCaddyFileNames = [],
				DapsComposeFilesToUpload = [],
				Warnings = [],
				ProjectPlans =
				[
					new RemoteProjectDeployPlan
					{
						ProjectName = "alpha",
						ComposeFilesToUpload = [],
						ComposeFileNamesForRemoteRun = ["compose_alpha.yaml"],
						ImageExportsSourcePath = imageExports,
						ImageExportFilesToUpload = [],
						UploadFiles = [new ProjectUploadFilePlan { SourcePath = uploadSource, RemoteDestinationPath = "/srv/projects/alpha/config/credentials.json" }],
						RemoteScriptFilesToUpload = [prerequisites],
					},
				],
			});

			return captured ?? throw new InvalidOperationException("The deploy script was never run.");
		}
		finally
		{
			Directory.Delete(dapsRoot, recursive: true);
		}
	}

	/// <summary>
	/// The generated script is deleted as soon as Execute returns, so it has to be read
	/// from disk at the moment the runner is asked to run it.
	/// </summary>
	private sealed class CapturingBashRunner(Action onRunScript) : IBashRunner
	{
		public void RunScript(string scriptPath, string workingDirectory, string? arguments = null, bool interactive = false, IReadOnlyDictionary<string, string>? env = null)
			=> onRunScript();

		public void RunShell(string shellExpression, string workingDirectory, bool interactive = false)
		{
		}

		public string CaptureScript(string scriptPath, string workingDirectory, IReadOnlyDictionary<string, string>? env = null)
			=> "";
	}

	private static BuildImageCommandPlan Command(string projectName) => new()
	{
		ProjectName = projectName,
		ToolkitScriptPath = $"/srv/projects/{projectName}/_scripts/build-docker-images.toolkit.sh",
		HasExistingExports = false,
	};
}
