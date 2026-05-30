using System.Globalization;
using System.Text.Json;
using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure;

public sealed class ConventionRestorePointsDiscoverer : IRestorePointsDiscoverer
{
	public const string ListRestorePointsScript = "list-restore-points.toolkit.sh";

	private readonly IBashRunner _toolkitBashRunner;

	public ConventionRestorePointsDiscoverer(IBashRunner toolkitBashRunner)
	{
		_toolkitBashRunner = toolkitBashRunner;
	}

	public IReadOnlyList<RestorePoint> Discover(DapsProject project)
	{
		var scriptHostPath = Path.Combine(project.WorkstationScriptsPath, ListRestorePointsScript);
		if (!File.Exists(scriptHostPath) || project.ToolkitPath is null)
			return [];

		var env = new Dictionary<string, string>
		{
			["DAPS_PROJECT"] = project.Definition.Name,
			["DAPS_BACKUPS_PATH"] = $"{project.ToolkitBackupsPath}/from_prod",
		};

		var json = _toolkitBashRunner.CaptureScript(
			$"{project.ToolkitScriptsPath}/{ListRestorePointsScript}",
			workingDirectory: "/",
			env: env);

		return ParseJson(json.Trim());
	}

	private static IReadOnlyList<RestorePoint> ParseJson(string json)
	{
		// Login shell (-lc) in the toolkit container may emit startup text before the script's JSON.
		// Strip anything before the first '[' so profile output doesn't break the parse.
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
