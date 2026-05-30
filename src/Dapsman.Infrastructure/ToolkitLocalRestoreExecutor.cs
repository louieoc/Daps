using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure;

public sealed class ToolkitLocalRestoreExecutor : IRestoreExecutor
{
	private readonly IBashRunner _toolkitBashRunner;

	public ToolkitLocalRestoreExecutor(IBashRunner toolkitBashRunner)
	{
		_toolkitBashRunner = toolkitBashRunner;
	}

	public void Execute(RestorePlan plan)
	{
		_toolkitBashRunner.RunScript(
			plan.RestoreScriptToolkitPath,
			workingDirectory: "/",
			env: plan.EnvVars);
	}
}
