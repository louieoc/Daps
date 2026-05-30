namespace Dapsman.Application.Tests;

public sealed class FakeBashRunner : IBashRunner
{
	public List<(string ScriptPath, string WorkingDirectory, string? Arguments, bool Interactive, IReadOnlyDictionary<string, string>? Env)> ScriptCalls { get; } = new();
	public List<(string ShellExpression, string WorkingDirectory, bool Interactive)> ShellCalls { get; } = new();

	public void RunScript(string scriptPath, string workingDirectory, string? arguments = null, bool interactive = false, IReadOnlyDictionary<string, string>? env = null)
	{
		ScriptCalls.Add((scriptPath, workingDirectory, arguments, interactive, env));
	}

	public void RunShell(string shellExpression, string workingDirectory, bool interactive = false)
	{
		ShellCalls.Add((shellExpression, workingDirectory, interactive));
	}

	public string CaptureScript(string scriptPath, string workingDirectory, IReadOnlyDictionary<string, string>? env = null)
	{
		throw new NotImplementedException();
	}
}
