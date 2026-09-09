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

public sealed class FakeDapsYamlEditor : IDapsYamlEditor
{
	public List<(string YamlPath, string ProjectName, string RelativePath)> AddCalls { get; } = [];
	public List<(string YamlPath, string ProjectName)> RemoveCalls { get; } = [];

	public void AddProject(string yamlPath, string projectName, string relativePath)
		=> AddCalls.Add((yamlPath, projectName, relativePath));

	public void RemoveProject(string yamlPath, string projectName)
		=> RemoveCalls.Add((yamlPath, projectName));
}

public sealed class FakeDockerExecutor : IDockerExecutor
{
	public bool Called { get; private set; }
	public string? LastArguments { get; private set; }
	public string? EnsuredNetworkName { get; private set; }

	public void EnsureNetworkExists(string networkName, string workingDirectory)
	{
		EnsuredNetworkName = networkName;
	}

	public void RunDocker(string arguments, string workingDirectory)
    {
		Called = true;
		LastArguments = arguments;
    }
}
