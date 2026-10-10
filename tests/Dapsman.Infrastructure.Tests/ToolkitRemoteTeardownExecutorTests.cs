using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure.Tests;

public sealed class ToolkitRemoteTeardownExecutorTests
{
	[Fact]
	public void Execute_WithARemovedPage_UploadsItOverTheRemoteCaddyFileAndReloads()
	{
		var script = GenerateTeardownScript(removedCaddyContent: """
alpha.example.com {
	respond `gone` 410
}
""");

		Assert.Contains("scp ", script);
		Assert.Contains("$remote:/srv/daps/caddy_sites/alpha.prod.caddy", script);
		Assert.DoesNotContain("rm -f '/srv/daps/caddy_sites/alpha.prod.caddy'", script);
		Assert.Contains("caddy reload --config /etc/caddy/Caddyfile", script);
	}

	[Fact]
	public void Execute_WithoutARemovedPage_DeletesTheRemoteCaddyFileAndReloads()
	{
		// No domain to render a removed page with — the project is not in daps.yaml, or has no
		// local prod caddy file — so the retired host stops serving the domain entirely.
		var script = GenerateTeardownScript(removedCaddyContent: null);

		Assert.Contains("rm -f '/srv/daps/caddy_sites/alpha.prod.caddy'", script);
		Assert.DoesNotContain("scp ", script);
		Assert.Contains("caddy reload --config /etc/caddy/Caddyfile", script);
	}

	[Fact]
	public void Execute_TargetsTheHostFromThePlanAndRemovesTheProjectFolder()
	{
		var script = GenerateTeardownScript(removedCaddyContent: null);

		Assert.Contains("remote=\"root@ramnode.example.com\"", script);
		Assert.Contains("ssh_key=\"~/.ssh/daps-key-ramnode\"", script);
		Assert.Contains("rm -rf '/srv/projects/alpha'", script);
		Assert.Contains("compose -f '/srv/projects/alpha/_docker/compose_alpha.yaml'", script);
		Assert.DoesNotContain("sudo", script);
	}

	[Fact]
	public void Execute_NonRootUser_RemovesFilesWithSudo()
	{
		// Containers write into the project folder as their own users (e.g. www-data), which a
		// non-root remote user cannot delete.
		var script = GenerateTeardownScript(removedCaddyContent: null, isRoot: false);

		Assert.Contains("sudo rm -rf '/srv/projects/alpha'", script);
		Assert.Contains("sudo rm -f '/srv/daps/caddy_sites/alpha.prod.caddy'", script);
		Assert.Contains("sudo docker compose -f", script);
	}

	[Fact]
	public void Execute_ReloadsCaddyOnlyWhenItIsRunning()
	{
		var script = GenerateTeardownScript(removedCaddyContent: null);

		var reloadLine = Assert.Single(script.Split('\n'), line => line.Contains("caddy reload"));
		Assert.Contains("ps -q -f name=daps-caddy-1 -f status=running", reloadLine);
		Assert.Contains("skipped reload", reloadLine);
	}

	/// <summary>
	/// The generated script is deleted as soon as Execute returns, so it has to be read from
	/// disk at the moment the runner is asked to run it.
	/// </summary>
	private static string GenerateTeardownScript(string? removedCaddyContent, bool isRoot = true)
	{
		var dapsRoot = Path.Combine(Path.GetTempPath(), "dapsman-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dapsRoot);
		try
		{
			string? captured = null;
			var runner = new CapturingBashRunner(() =>
				captured = File.ReadAllText(Directory.GetFiles(dapsRoot, "teardown.sh", SearchOption.AllDirectories).Single()));

			new ToolkitRemoteTeardownExecutor(runner).Execute(new RemoteTeardownPlan
			{
				ProjectName = "alpha",
				DapsRootPath = dapsRoot,
				ProviderName = "ramnode",
				ToolkitContainerName = "daps-toolkit-1",
				RemoteHost = "ramnode.example.com",
				RemoteUser = isRoot ? "root" : "ubuntu",
				SshKeyName = "daps-key-ramnode",
				IsRoot = isRoot,
				CaddySiteFileName = "alpha.prod.caddy",
				RemovedCaddyContent = removedCaddyContent,
				RemoteProjectPath = "/srv/projects/alpha",
			});

			return captured ?? throw new InvalidOperationException("The teardown script was never run.");
		}
		finally
		{
			Directory.Delete(dapsRoot, recursive: true);
		}
	}

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
}
