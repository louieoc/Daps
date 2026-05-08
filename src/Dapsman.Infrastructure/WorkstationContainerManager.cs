using Dapsman.Application;

namespace Dapsman.Infrastructure;

public class WorkstationContainerManager : IContainerManager
{
	public IReadOnlyList<string> GetContainerNames()
	{
		var startInfo = new System.Diagnostics.ProcessStartInfo
		{
			FileName = "docker",
			Arguments = "ps --format \"{{.Names}}\"",
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true,
		};

		using var process = System.Diagnostics.Process.Start(startInfo);
		if (process is null)
		{
			throw new InvalidOperationException("Failed to query running Docker containers.");
		}

		var output = process.StandardOutput.ReadToEnd();
		var error = process.StandardError.ReadToEnd();
		process.WaitForExit();

		if (process.ExitCode != 0)
		{
			throw new InvalidOperationException($"Failed to query running Docker containers: {error}".Trim());
		}

		var names = output
			.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
			.ToList();

		return names;
	}
}
