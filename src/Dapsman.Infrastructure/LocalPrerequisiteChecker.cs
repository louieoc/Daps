using System.ComponentModel;
using System.Diagnostics;

namespace Dapsman.Infrastructure;

public sealed class LocalPrerequisiteChecker
{
	public void EnsureLocalBuildPrerequisites()
	{
		EnsureCommandExists("docker", "Docker Desktop");
		EnsureDockerRunning();
		EnsureCommandExists("git", "Git");
		EnsureBashExists();
	}

	private static void EnsureDockerRunning()
	{
		var startInfo = new ProcessStartInfo
		{
			FileName = "docker",
			Arguments = "info",
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true,
		};

		bool isRunning;
		try
		{
			using var process = Process.Start(startInfo);
			isRunning = process is not null && process.WaitForExit(5000) && process.ExitCode == 0;
		}
		catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
		{
			_ = ex;
			isRunning = false;
		}

		if (!isRunning)
			throw new InvalidOperationException("Docker is not running. Start Docker Desktop and try again.");
	}

	private static void EnsureBashExists()
	{
		try
		{
			_ = WorkstationBashRunner.ResolveBashExecutable();
		}
		catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
		{
			throw new InvalidOperationException("Bash is not available. Install Git Bash or provide bash on PATH.", ex);
		}
	}

	private static void EnsureCommandExists(string command, string label)
	{
		if (TryRunVersionCheck(command))
		{
			return;
		}

		throw new InvalidOperationException($"{label} is not available. Expected command: '{command} --version'.");
	}

	private static bool TryRunVersionCheck(string command)
	{
		var startInfo = new ProcessStartInfo
		{
			FileName = command,
			Arguments = "--version",
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true,
		};

		try
		{
			using var process = Process.Start(startInfo);
			if (process is null)
			{
				return false;
			}

			process.WaitForExit(3000);
			return process.ExitCode == 0;
		}
		catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
		{
			_ = ex;
			return false;
		}
	}
}
