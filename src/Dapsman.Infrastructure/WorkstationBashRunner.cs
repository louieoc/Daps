using System.ComponentModel;
using System.Diagnostics;
using Dapsman.Application;

namespace Dapsman.Infrastructure;

public sealed class WorkstationBashRunner : IBashRunner
{
    public void RunScript(string scriptPath, string workingDirectory, string? arguments = null, bool interactive = false, IReadOnlyDictionary<string, string>? env = null)
    {
        var args = string.IsNullOrEmpty(arguments) ? $"\"{scriptPath}\"" : $"\"{scriptPath}\" {arguments}";
        RunBash(args, workingDirectory, noPathConversion: false, env);
    }

    public void RunShell(string shellExpression, string workingDirectory, bool interactive = false)
    {
        var bashExecutable = ResolveBashExecutable();
        var startInfo = new ProcessStartInfo
        {
            FileName = bashExecutable,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            CreateNoWindow = false,
        };
        // ArgumentList lets the OS handle quoting of each argument, so the shell expression
        // is passed intact regardless of embedded double quotes or Windows backslash paths.
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(shellExpression);
        // MSYS_NO_PATHCONV=1 prevents Git Bash from mangling Unix-style paths inside the expression.
        startInfo.Environment["MSYS_NO_PATHCONV"] = "1";

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start bash process.");
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Bash process failed with exit code {process.ExitCode}.");
    }

    private static void RunBash(string args, string workingDirectory, bool noPathConversion, IReadOnlyDictionary<string, string>? env = null)
    {
        var bashExecutable = ResolveBashExecutable();
        var startInfo = new ProcessStartInfo
        {
            FileName = bashExecutable,
            Arguments = args,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            CreateNoWindow = false,
        };

        if (noPathConversion)
        {
            startInfo.Environment["MSYS_NO_PATHCONV"] = "1";
        }

        if (env is not null)
        {
            foreach (var (key, value) in env)
                startInfo.Environment[key] = value;
        }

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            throw new InvalidOperationException("Failed to start bash process.");
        }

        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Bash process failed with exit code {process.ExitCode}.");
        }
    }

    public string CaptureScript(string scriptPath, string workingDirectory, IReadOnlyDictionary<string, string>? env = null)
    {
        var bashExecutable = ResolveBashExecutable();
        var startInfo = new ProcessStartInfo
        {
            FileName = bashExecutable,
            Arguments = $"\"{scriptPath}\"",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = false,
            CreateNoWindow = false,
        };

        if (env is not null)
            foreach (var (key, value) in env)
                startInfo.Environment[key] = value;

        using var process = Process.Start(startInfo);
        if (process is null)
            throw new InvalidOperationException("Failed to start bash process.");

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Bash process failed with exit code {process.ExitCode}.");

        return output;
    }

    public static string ResolveBashExecutable()
    {
        if (CanRun("bash"))
        {
            return "bash";
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var candidates = new[]
        {
            Path.Combine(programFiles, "Git", "bin", "bash.exe"),
            Path.Combine(programFiles, "Git", "usr", "bin", "bash.exe"),
            Path.Combine(programFilesX86, "Git", "bin", "bash.exe"),
            Path.Combine(programFilesX86, "Git", "usr", "bin", "bash.exe"),
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate) && CanRun(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Bash is required in workstation context.");
    }

    private static bool CanRun(string executable)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = "--version",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return false;
            }

            process.WaitForExit(2000);
            return process.ExitCode == 0;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            _ = ex;
            return false;
        }
    }
}
