using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure;

public sealed class WorkstationLocalTeardownExecutor(IBashRunner bashRunner) : ILocalTeardownExecutor
{
	public void Execute(LocalTeardownPlan plan)
	{
		// Step 1: stop containers and delete volumes
		if (plan.ComposeFiles.Count > 0)
		{
			var flags = string.Join(" ", plan.ComposeFiles.Select(f => $"-f \"{f}\""));
			bashRunner.RunShell($"docker compose {flags} down -v || true", Environment.CurrentDirectory);
		}

		// Step 2: delete caddy site files
		foreach (var file in plan.CaddySiteFilesToDelete)
		{
			if (File.Exists(file))
				File.Delete(file);
		}

		// Step 3: reload Caddy (tolerate failure if container is not running)
		try
		{
			bashRunner.RunShell(
				$"docker exec {plan.CaddyContainerName} caddy reload --config {plan.CaddyConfigPath}",
				Environment.CurrentDirectory);
		}
		catch
		{
			Console.WriteLine($"- caddy reload skipped ('{plan.CaddyContainerName}' not running)");
		}

		// Step 4: delete project folder
		if (Directory.Exists(plan.ProjectPath))
			Directory.Delete(plan.ProjectPath, recursive: true);
	}
}
