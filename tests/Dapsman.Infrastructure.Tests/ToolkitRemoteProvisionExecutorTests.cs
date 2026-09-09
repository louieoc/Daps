using Dapsman.Domain;

namespace Dapsman.Infrastructure.Tests;

public sealed class ToolkitRemoteProvisionExecutorTests
{
	[Fact]
	public void Execute_StagesScriptsWithLfEndings_AndRunsStagedProvisionScript()
	{
		var root = TestHelper.CreateTempDirectory();
		var scripts = Path.Combine(root, "scripts");
		Directory.CreateDirectory(scripts);

		// A Windows checkout can leave CRLF in the working tree. On Linux that fails with
		// "set: pipefail: invalid option name" the moment the script runs.
		var crlfScript = Path.Combine(scripts, "configure-host.sh");
		File.WriteAllText(crlfScript, "#!/usr/bin/env bash\r\nset -euo pipefail\r\necho hi\r\n");

		var runner = new RecordingBashRunner(root);
		var executor = new ToolkitRemoteProvisionExecutor(runner);

		executor.Execute(new RemoteProvisionPlan
		{
			ProviderName = "ramnode",
			DefaultKeyName = "daps-key-ramnode",
			ToolkitContainerName = "daps-toolkit-1",
			ProviderDetails = [],
			ToolkitCommand = "echo one && echo two",
			DapsRootPath = root,
			ScriptFilesToStage = [crlfScript],
		});

		Assert.DoesNotContain("\r\n", runner.StagedConfigureScript, StringComparison.Ordinal);
		Assert.Contains("set -euo pipefail\necho hi", runner.StagedConfigureScript, StringComparison.Ordinal);

		Assert.DoesNotContain("\r\n", runner.StagedProvisionScript, StringComparison.Ordinal);
		Assert.Contains("echo one && echo two", runner.StagedProvisionScript, StringComparison.Ordinal);

		Assert.Equal("/srv/daps/.dapsman/provision/provision.sh", runner.ScriptPath);
		Assert.True(runner.Interactive, "ssh-copy-id prompts for a password, which needs a TTY.");
	}

	[Fact]
	public void Execute_RemovesStagingDirectoryAfterwards()
	{
		var root = TestHelper.CreateTempDirectory();
		var scripts = Path.Combine(root, "scripts");
		Directory.CreateDirectory(scripts);
		var script = Path.Combine(scripts, "configure-host.sh");
		File.WriteAllText(script, "echo hi\n");

		var executor = new ToolkitRemoteProvisionExecutor(new RecordingBashRunner(root));

		executor.Execute(new RemoteProvisionPlan
		{
			ProviderName = "ramnode",
			DefaultKeyName = "daps-key-ramnode",
			ToolkitContainerName = "daps-toolkit-1",
			ProviderDetails = [],
			ToolkitCommand = "echo ok",
			DapsRootPath = root,
			ScriptFilesToStage = [script],
		});

		Assert.False(Directory.Exists(Path.Combine(root, ".dapsman", "provision")));
	}

	/// <summary>
	/// Captures the staged files at the moment the runner is invoked, since the
	/// executor deletes the staging directory once it returns.
	/// </summary>
	private sealed class RecordingBashRunner : Application.IBashRunner
	{
		private readonly string _root;

		public RecordingBashRunner(string root) => _root = root;

		public string StagedConfigureScript { get; private set; } = "";
		public string StagedProvisionScript { get; private set; } = "";
		public string ScriptPath { get; private set; } = "";
		public bool Interactive { get; private set; }

		public void RunScript(string scriptPath, string workingDirectory, string? arguments = null, bool interactive = false, IReadOnlyDictionary<string, string>? env = null)
		{
			ScriptPath = scriptPath;
			Interactive = interactive;

			var staging = Path.Combine(_root, ".dapsman", "provision");
			var configure = Path.Combine(staging, "configure-host.sh");
			if (File.Exists(configure))
			{
				StagedConfigureScript = File.ReadAllText(configure);
			}

			var provision = Path.Combine(staging, "provision.sh");
			if (File.Exists(provision))
			{
				StagedProvisionScript = File.ReadAllText(provision);
			}
		}

		public void RunShell(string shellExpression, string workingDirectory, bool interactive = false)
			=> throw new InvalidOperationException("Provisioning should run a staged script, not an inline shell expression.");

		public string CaptureScript(string scriptPath, string workingDirectory, IReadOnlyDictionary<string, string>? env = null)
			=> "";
	}
}
