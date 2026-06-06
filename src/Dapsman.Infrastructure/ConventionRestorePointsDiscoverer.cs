using System.Globalization;
using System.Text.Json;
using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure;

public sealed class ConventionRestorePointsDiscoverer : IRestorePointsDiscoverer
{
	public const string ListRestorePointsScript = "list-restore-points.toolkit.sh";

	private readonly IBashRunner _workstationBashRunner;

	public ConventionRestorePointsDiscoverer(IBashRunner workstationBashRunner)
	{
		_workstationBashRunner = workstationBashRunner;
	}

	public IReadOnlyList<RestorePoint> Discover(DapsProject project)
	{
		var scriptHostPath = Path.Combine(project.WorkstationScriptsPath, ListRestorePointsScript);
		if (!File.Exists(scriptHostPath))
			return [];

		var backupsPath = Path.Combine(project.WorkstationBackupsPath, "from_prod")
			.Replace('\\', '/');

		var env = new Dictionary<string, string>
		{
			["DAPS_PROJECT"] = project.Definition.Name,
			["DAPS_BACKUPS_PATH"] = backupsPath,
		};

		var json = _workstationBashRunner.CaptureScript(
			scriptHostPath,
			workingDirectory: project.Definition.Path,
			env: env);

		return ParseJson(json.Trim());
	}

	private static IReadOnlyList<RestorePoint> ParseJson(string json)
	{
		var start = json.IndexOf('[');
		if (start > 0)
			json = json[start..];

		if (string.IsNullOrEmpty(json) || json == "[]")
			return [];

		using var doc = JsonDocument.Parse(json);
		var result = new List<RestorePoint>();

		foreach (var el in doc.RootElement.EnumerateArray())
		{
			var timestamp = el.GetProperty("timestamp").GetString()!;
			if (!DateTimeOffset.TryParseExact(timestamp, "yyyyMMdd_HHmmss",
				CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
				continue;

			result.Add(new RestorePoint
			{
				Index = el.GetProperty("index").GetInt32(),
				Env = el.GetProperty("env").GetString()!,
				Timestamp = timestamp,
				ParsedTimestamp = parsed,
				IsComplete = el.GetProperty("complete").GetBoolean(),
			});
		}

		return result;
	}
}
