using System.Text;
using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure;

public sealed class ToolkitRemoteDeployExecutor : IRemoteDeployExecutor
{
	private readonly IBashRunner _bashRunner;

	public ToolkitRemoteDeployExecutor(IBashRunner bashRunner)
	{
		_bashRunner = bashRunner;
	}

	/// <summary>Docker's Go template for the daemon's own platform, e.g. "linux/amd64".</summary>
	private const string DockerServerPlatformFormat = "'{{.Server.Os}}/{{.Server.Arch}}'";

	/// <summary>The same shape read off a loaded image, for comparison against the host's.</summary>
	private const string DockerImagePlatformFormat = "'{{.Os}}/{{.Architecture}}'";

	public void ExecuteBuildImages(IReadOnlyList<BuildImageCommandPlan> commands, string dapsRootPath, string? targetPlatform)
	{
		// Passed as environment variables rather than script arguments because
		// build-docker-images.toolkit.sh already exists in every project created before this
		// change. A script that does not read a variable keeps working exactly as it did;
		// adding a positional argument would have shifted arguments under those scripts instead.
		foreach (var command in commands)
		{
			var env = new Dictionary<string, string> { ["DAPS_PROJECT"] = command.ProjectName };
			if (targetPlatform is not null)
			{
				env["DAPS_TARGET_PLATFORM"] = targetPlatform;
			}

			_bashRunner.RunScript(command.ToolkitScriptPath, dapsRootPath, env: env);
		}
	}

	public void Execute(RemoteDeployPlan plan)
	{
		var staging = CreateDeployStaging(plan);
		try
		{
			// Generated here with \n, but normalized anyway: this file runs in the toolkit.
			var script = BuildRemoteDeployScript(plan, staging.ToolkitPath).Replace("\r\n", "\n");
			File.WriteAllText(staging.ScriptHostPath, script);
			_bashRunner.RunScript(staging.ScriptToolkitPath, plan.DapsRootPath);
		}
		finally
		{
			TryDeleteStaging(staging.HostPath);
		}
	}

	private static DeployStaging CreateDeployStaging(RemoteDeployPlan plan)
	{
		var id = Guid.NewGuid().ToString("N");
		var hostPath = Path.Combine(plan.DapsRootPath, ".dapsman", "deploy", id);
		Directory.CreateDirectory(hostPath);

		var caddyPath = Path.Combine(hostPath, "caddy");
		Directory.CreateDirectory(caddyPath);
		CopyFile(plan.CaddyfileSourcePath, Path.Combine(caddyPath, "Caddyfile"));

		var caddySitesPath = Path.Combine(hostPath, "caddy_sites");
		Directory.CreateDirectory(caddySitesPath);
		foreach (var caddyFile in plan.CaddySiteFilesToUpload)
		{
			CopyFile(caddyFile.SourcePath, Path.Combine(caddySitesPath, caddyFile.DestinationFileName));
		}

		var dockerPath = Path.Combine(hostPath, "docker");
		Directory.CreateDirectory(dockerPath);
		foreach (var dapsCompose in plan.DapsComposeFilesToUpload)
		{
			CopyFile(dapsCompose, Path.Combine(dockerPath, Path.GetFileName(dapsCompose)));
		}

		var projectsRoot = Path.Combine(hostPath, "projects");
		Directory.CreateDirectory(projectsRoot);
		foreach (var projectPlan in plan.ProjectPlans)
		{
			var projectDockerPath = Path.Combine(projectsRoot, projectPlan.ProjectName, "_docker");
			Directory.CreateDirectory(projectDockerPath);
			var exportsPath = Path.Combine(projectDockerPath, "image-exports");
			Directory.CreateDirectory(exportsPath);
			var uploadsPath = Path.Combine(projectsRoot, projectPlan.ProjectName, "_uploads");
			Directory.CreateDirectory(uploadsPath);

			if (projectPlan.RemoteScriptFilesToUpload.Count > 0)
			{
				var scriptsPath = Path.Combine(projectsRoot, projectPlan.ProjectName, "_scripts");
				Directory.CreateDirectory(scriptsPath);
				foreach (var scriptFile in projectPlan.RemoteScriptFilesToUpload)
				{
					// These are uploaded to the remote host and run by bash there, so they
					// must be LF. A project checked out on Windows can have CRLF endings.
					ConfigUtils.CopyFileAndReplaceLineEndingsForLinux(
						scriptFile,
						Path.Combine(scriptsPath, Path.GetFileName(scriptFile)));
				}
			}

			foreach (var composeFile in projectPlan.ComposeFilesToUpload)
			{
				CopyFile(composeFile, Path.Combine(projectDockerPath, Path.GetFileName(composeFile)));
			}

			foreach (var exportFile in CurrentImageExports(projectPlan))
			{
				CopyFile(exportFile, Path.Combine(exportsPath, Path.GetFileName(exportFile)));
			}

			for (var i = 0; i < projectPlan.UploadFiles.Count; i++)
			{
				var upload = projectPlan.UploadFiles[i];
				var stagedName = $"{i:D3}_{Path.GetFileName(upload.SourcePath)}";
				CopyFile(upload.SourcePath, Path.Combine(uploadsPath, stagedName));
			}
		}

		return new DeployStaging
		{
			HostPath = hostPath,
			ToolkitPath = $"/srv/daps/.dapsman/deploy/{id}",
			ScriptHostPath = Path.Combine(hostPath, "deploy.sh"),
			ScriptToolkitPath = $"/srv/daps/.dapsman/deploy/{id}/deploy.sh",
		};
	}

	/// <summary>
	/// The tarballs in the project's image-exports folder as of now, not as of planning.
	/// The plan is built before the build-images step runs, so on a project's first deploy
	/// its ImageExportFilesToUpload is empty: the tar the build has just saved would be left
	/// behind and the remote compose up would try to pull the image from a registry instead.
	/// </summary>
	private static IReadOnlyList<string> CurrentImageExports(RemoteProjectDeployPlan projectPlan)
	{
		if (!Directory.Exists(projectPlan.ImageExportsSourcePath))
			return projectPlan.ImageExportFilesToUpload;

		return Directory.EnumerateFiles(projectPlan.ImageExportsSourcePath)
			.OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	/// <summary>
	/// Verbatim copy. Used for files that are carried to the remote but not executed
	/// there — compose and caddy files (both tolerate CRLF), image tarballs and manifest
	/// uploads (binary, or of unknown type). For text destined to be run on Linux use
	/// ConfigUtils.CopyFileAndReplaceLineEndingsForLinux instead.
	/// </summary>
	private static void CopyFile(string sourcePath, string destinationPath)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
		File.Copy(sourcePath, destinationPath, overwrite: true);
	}

	private static void TryDeleteStaging(string hostStagingPath)
	{
		try
		{
			if (Directory.Exists(hostStagingPath))
			{
				Directory.Delete(hostStagingPath, recursive: true);
			}
		}
		catch
		{
			// no-op
		}
	}

	private static string BuildRemoteDeployScript(RemoteDeployPlan plan, string stagingToolkitPath)
	{
		var sb = new StringBuilder();
		sb.AppendLine("#!/usr/bin/env bash");
		sb.AppendLine("set -euo pipefail");
		sb.AppendLine();
		sb.AppendLine($"remote=\"{EscapeBash(plan.RemoteUser)}@{EscapeBash(plan.RemoteHost)}\"");
		sb.AppendLine($"ssh_key=\"~/.ssh/{EscapeBash(plan.SshKeyName)}\"");
		sb.AppendLine("ssh_opts=(-o StrictHostKeyChecking=accept-new -o BatchMode=yes -i \"$ssh_key\")");
		sb.AppendLine();
		var docker = plan.IsRoot ? "docker" : "sudo docker";
		var sudo = plan.IsRoot ? "" : "sudo ";
		var setupDirs = $"{sudo}mkdir -p /srv/daps/caddy /srv/daps/caddy_sites /srv/daps/docker /srv/projects";
		if (!plan.IsRoot)
			setupDirs += $" && {sudo}chown -R \"$(id -un)\" /srv/daps /srv/projects";
		sb.AppendLine($"ssh \"${{ssh_opts[@]}}\" \"$remote\" '{setupDirs}'");
		sb.AppendLine();
		sb.AppendLine($"scp \"${{ssh_opts[@]}}\" \"{EscapeBash(stagingToolkitPath)}/caddy/Caddyfile\" \"$remote:/srv/daps/caddy/Caddyfile\"");

		sb.AppendLine();
		sb.AppendLine("ssh \"${ssh_opts[@]}\" \"$remote\" 'bash -s' <<'REMOTE_CLEAN'");
		sb.AppendLine("set -euo pipefail");
		sb.AppendLine($"{sudo}mkdir -p /srv/daps/caddy_sites");
		sb.AppendLine("mapfile -t expected < <(cat <<'EOF_EXPECTED'");
		foreach (var fileName in plan.ExpectedProdCaddyFileNames)
		{
			sb.AppendLine(fileName);
		}
		sb.AppendLine("EOF_EXPECTED");
		sb.AppendLine(")");
		sb.AppendLine("for existing in /srv/daps/caddy_sites/*.prod.caddy; do");
		sb.AppendLine("  [ -e \"$existing\" ] || break");
		sb.AppendLine("  base=$(basename \"$existing\")");
		sb.AppendLine("  keep=0");
		sb.AppendLine("  for item in \"${expected[@]}\"; do");
		sb.AppendLine("    if [[ \"$item\" == \"$base\" ]]; then");
		sb.AppendLine("      keep=1");
		sb.AppendLine("      break");
		sb.AppendLine("    fi");
		sb.AppendLine("  done");
		sb.AppendLine("  if [[ \"$keep\" == \"0\" ]]; then");
		sb.AppendLine("    rm -f \"$existing\"");
		sb.AppendLine("  fi");
		sb.AppendLine("done");
		sb.AppendLine("REMOTE_CLEAN");
		sb.AppendLine();

		foreach (var caddyFile in plan.CaddySiteFilesToUpload)
		{
			sb.AppendLine($"scp \"${{ssh_opts[@]}}\" \"{EscapeBash(stagingToolkitPath)}/caddy_sites/{EscapeBash(caddyFile.DestinationFileName)}\" \"$remote:/srv/daps/caddy_sites/{EscapeBash(caddyFile.DestinationFileName)}\"");
		}

		foreach (var dapsFile in plan.DapsComposeFilesToUpload)
		{
			sb.AppendLine($"scp \"${{ssh_opts[@]}}\" \"{EscapeBash(stagingToolkitPath)}/docker/{EscapeBash(Path.GetFileName(dapsFile))}\" \"$remote:/srv/daps/docker/{EscapeBash(Path.GetFileName(dapsFile))}\"");
		}

		foreach (var projectPlan in plan.ProjectPlans)
		{
			var projectDir = $"/srv/projects/{EscapeBash(projectPlan.ProjectName)}";
			// _scripts is listed here, before the chown, so the recursive chown covers it.
			// Creating it later with sudo would leave it root-owned and the unprivileged
			// scp of the prerequisite scripts would fail with "Permission denied".
			var mkdirProject = $"{sudo}mkdir -p {projectDir}/_docker {projectDir}/_docker/image-exports {projectDir}/_scripts";
			if (!string.IsNullOrEmpty(sudo))
				mkdirProject += $" && {sudo}chown -R \"$(id -un)\" {projectDir}";
			sb.AppendLine($"ssh \"${{ssh_opts[@]}}\" \"$remote\" '{mkdirProject}'");
			foreach (var composeFile in projectPlan.ComposeFilesToUpload)
			{
				sb.AppendLine($"scp \"${{ssh_opts[@]}}\" \"{EscapeBash(stagingToolkitPath)}/projects/{EscapeBash(projectPlan.ProjectName)}/_docker/{EscapeBash(Path.GetFileName(composeFile))}\" \"$remote:/srv/projects/{EscapeBash(projectPlan.ProjectName)}/_docker/{EscapeBash(Path.GetFileName(composeFile))}\"");
			}

			foreach (var exportFile in CurrentImageExports(projectPlan))
			{
				sb.AppendLine($"scp \"${{ssh_opts[@]}}\" \"{EscapeBash(stagingToolkitPath)}/projects/{EscapeBash(projectPlan.ProjectName)}/_docker/image-exports/{EscapeBash(Path.GetFileName(exportFile))}\" \"$remote:/srv/projects/{EscapeBash(projectPlan.ProjectName)}/_docker/image-exports/{EscapeBash(Path.GetFileName(exportFile))}\"");
			}

			for (var i = 0; i < projectPlan.UploadFiles.Count; i++)
			{
				var upload = projectPlan.UploadFiles[i];
				var stagedName = $"{i:D3}_{Path.GetFileName(upload.SourcePath)}";
				var remoteDestination = EscapeBash(upload.RemoteDestinationPath);
				var remoteTempPath = EscapeBash($"/tmp/dapsman-upload-{projectPlan.ProjectName}-{i:D3}");
				// The destination can be any absolute path, so sudo is scoped to the file itself:
				// install hands it to the remote user and the directory's owner is left alone.
				sb.AppendLine($"ssh \"${{ssh_opts[@]}}\" \"$remote\" '{sudo}mkdir -p \"$(dirname \"{remoteDestination}\")\"'");
				sb.AppendLine($"scp \"${{ssh_opts[@]}}\" \"{EscapeBash(stagingToolkitPath)}/projects/{EscapeBash(projectPlan.ProjectName)}/_uploads/{EscapeBash(stagedName)}\" \"$remote:{remoteTempPath}\"");
				sb.AppendLine($"ssh \"${{ssh_opts[@]}}\" \"$remote\" 'if [ -d \"{remoteDestination}\" ]; then {sudo}rm -rf \"{remoteDestination}\"; fi'");
				sb.AppendLine($"ssh \"${{ssh_opts[@]}}\" \"$remote\" '{sudo}rm -f \"{remoteDestination}\" && {sudo}install -o \"$(id -un)\" -m 600 \"{remoteTempPath}\" \"{remoteDestination}\" && rm -f \"{remoteTempPath}\"'");
				sb.AppendLine($"ssh \"${{ssh_opts[@]}}\" \"$remote\" 'if [ ! -f \"{remoteDestination}\" ]; then echo \"Expected upload destination to be a file: {remoteDestination}\" >&2; exit 1; fi'");
			}

			if (projectPlan.RemoteScriptFilesToUpload.Count > 0)
			{
				// _scripts already exists and is owned by the remote user: it is created with the
				// project directories above, before the chown.
				var remoteScriptsDir = $"/srv/projects/{EscapeBash(projectPlan.ProjectName)}/_scripts";
				foreach (var scriptFile in projectPlan.RemoteScriptFilesToUpload)
				{
					var fileName = EscapeBash(Path.GetFileName(scriptFile));
					sb.AppendLine($"scp \"${{ssh_opts[@]}}\" \"{EscapeBash(stagingToolkitPath)}/projects/{EscapeBash(projectPlan.ProjectName)}/_scripts/{fileName}\" \"$remote:{remoteScriptsDir}/{fileName}\"");
					sb.AppendLine($"ssh \"${{ssh_opts[@]}}\" \"$remote\" 'chmod +x {remoteScriptsDir}/{fileName}'");
				}
			}
		}

		sb.AppendLine();
		sb.AppendLine("ssh \"${ssh_opts[@]}\" \"$remote\" 'bash -s' <<'REMOTE_DOCKER'");
		sb.AppendLine("set -euo pipefail");
		sb.AppendLine($"{docker} network inspect daps_net >/dev/null 2>&1 || {docker} network create daps_net >/dev/null");
		sb.AppendLine($"host_platform=\"$({docker} version --format {DockerServerPlatformFormat})\"");
		var dapsRemoteComposeArgs = string.Join(" ", plan.DapsComposeFilesToUpload.Select(f => $"-f /srv/daps/docker/{Path.GetFileName(f)}"));
		sb.AppendLine($"{docker} compose {dapsRemoteComposeArgs} up -d");
		foreach (var projectPlan in plan.ProjectPlans)
		{
			foreach (var prereqName in projectPlan.RemotePrerequisiteScriptNames)
			{
				// Template prerequisite scripts read the project name from DAPS_PROJECT instead of
				// having it baked in at init. This runs inside a quoted heredoc, so the assignment
				// is emitted literally and evaluated on the remote host.
				sb.AppendLine($"DAPS_PROJECT='{EscapeBash(projectPlan.ProjectName)}' bash /srv/projects/{EscapeBash(projectPlan.ProjectName)}/_scripts/{EscapeBash(prereqName)}");
			}

			sb.AppendLine($"for image_file in /srv/projects/{projectPlan.ProjectName}/_docker/image-exports/*; do");
			sb.AppendLine("  [ -e \"$image_file\" ] || continue");
			sb.AppendLine($"  loaded_output=\"$({docker} load -i \"$image_file\")\"");
			sb.AppendLine("  echo \"$loaded_output\"");
			// A tarball built on a different architecture loads without complaint and only fails
			// when the container starts, as "exec format error" -- a message that names neither
			// architecture nor the image. Comparing here turns it into a deploy-time error that
			// says what to do. Image names cannot contain spaces, so word splitting is safe.
			sb.AppendLine("  for image_ref in $(echo \"$loaded_output\" | sed -n 's/^Loaded image: //p'); do");
			sb.AppendLine($"    image_platform=\"$({docker} image inspect \"$image_ref\" --format {DockerImagePlatformFormat})\"");
			sb.AppendLine("    if [ \"$image_platform\" != \"$host_platform\" ]; then");
			sb.AppendLine("      echo \"ERROR: image '$image_ref' was built for $image_platform but this host runs $host_platform.\" >&2");
			sb.AppendLine("      echo \"Rebuild it for the host with: dapsman prod deploy --build\" >&2");
			sb.AppendLine("      exit 1");
			sb.AppendLine("    fi");
			sb.AppendLine("  done");
			sb.AppendLine("done");
			var projectComposeArgs = string.Join(" ", projectPlan.ComposeFileNamesForRemoteRun.Select(f => $"-f /srv/projects/{projectPlan.ProjectName}/_docker/{f}"));
			sb.AppendLine($"{docker} compose {projectComposeArgs} up -d --force-recreate --renew-anon-volumes");
		}
		sb.AppendLine("REMOTE_DOCKER");

		return sb.ToString();
	}

	private static string EscapeBash(string value)
	{
		return value.Replace("\\", "/").Replace("\"", "\\\"");
	}

	private sealed class DeployStaging
	{
		public required string HostPath { get; init; }
		public required string ToolkitPath { get; init; }
		public required string ScriptHostPath { get; init; }
		public required string ScriptToolkitPath { get; init; }
	}
}
