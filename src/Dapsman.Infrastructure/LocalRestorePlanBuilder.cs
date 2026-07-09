using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure;

public sealed class LocalRestorePlanBuilder : IRestorePlanBuilder
{
	public const string RestoreLocalScript = "restore-local.toolkit.sh";

	private readonly IToolkitResolver _toolkitResolver;
	private readonly IRestorePointsDiscoverer _discoverer;

	public LocalRestorePlanBuilder(
		IToolkitResolver toolkitResolver,
		IRestorePointsDiscoverer discoverer)
	{
		_toolkitResolver = toolkitResolver;
		_discoverer = discoverer;
	}

	public PlanResult<RestorePlan> BuildPlan(DapsProject project, RestoreOptions options)
	{
		var restoreScriptHostPath = Path.Combine(project.WorkstationScriptsPath, RestoreLocalScript);
		if (!File.Exists(restoreScriptHostPath))
		{
			return PlanResult<RestorePlan>.NotSupported(
				$"Project '{project.Definition.Name}' has no restore script ({RestoreLocalScript}); local restore is not supported for this project.");
		}

		if (project.ToolkitPath is null)
		{
			return PlanResult<RestorePlan>.NotSupported(
				$"Project '{project.Definition.Name}' has no toolkit bind mount defined (compose_daps_*.dev.yaml); cannot resolve toolkit paths.");
		}

		var restorePoints = _discoverer.Discover(project);
		if (restorePoints.Count == 0)
		{
			return PlanResult<RestorePlan>.NotSupported(
				$"No backup files found. Run 'dapsman prod backup' first, or check '{Path.Combine(project.WorkstationBackupsPath, "from_prod")}'.");
		}

		var selected = SelectRestorePoint(restorePoints, options.SelectedRestorePoint);

		if (!selected.IsComplete)
			throw new InvalidOperationException(
				$"Restore point {selected.Index} ({selected.DisplayLabel}) is incomplete — not all backup files are present.");

		var toolkitDef = _toolkitResolver.Resolve();

		return PlanResult<RestorePlan>.Supported(new RestorePlan
		{
			ProjectName = project.Definition.Name,
			ToolkitContainerName = toolkitDef.ContainerName,
			RestorePoint = selected,
			RestoreScriptToolkitPath = $"{project.ToolkitScriptsPath}/{RestoreLocalScript}",
			BackupsToolkitPath = $"{project.ToolkitBackupsPath}/from_prod",
			ProdUrl = options.ProdUrl,
		});
	}

	private static RestorePoint SelectRestorePoint(IReadOnlyList<RestorePoint> points, string? selector)
	{
		if (selector is null)
		{
			return points.FirstOrDefault(r => r.IsComplete)
				?? throw new InvalidOperationException("No complete restore points found.");
		}

		if (int.TryParse(selector, out var idx))
		{
			return points.FirstOrDefault(r => r.Index == idx)
				?? throw new InvalidOperationException($"Restore point {idx} not found. Run --list-restore-points to see available options.");
		}

		return points.FirstOrDefault(r => r.Timestamp == selector)
			?? throw new InvalidOperationException($"No restore point with timestamp '{selector}' found. Run --list-restore-points to see available options.");
	}
}
