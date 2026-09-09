using System.Globalization;
using System.Text.Json;
using Dapsman.Application;
using Dapsman.Domain;

namespace Dapsman.Infrastructure;

public sealed class RestorePointsDiscoverer : IRestorePointsDiscoverer
{
	public const string ListRestorePointsScript = "list-restore-points.toolkit.sh";

	private readonly IBashRunner _workstationBashRunner;

	public RestorePointsDiscoverer(IBashRunner workstationBashRunner)
	{
		_workstationBashRunner = workstationBashRunner;
	}

	public IReadOnlyList<RestorePoint> Discover(DapsProject project)
	{
		var scriptHostPath = Path.Combine(project.WorkstationScriptsPath, ListRestorePointsScript);
		if (!File.Exists(scriptHostPath))
			return [];

		// Each source directory is scanned separately; the list script only ever sees one directory.
		var found = new List<RestorePoint>();
		foreach (var sourceDirectory in BackupSources.All)
		{
			var backupsPath = Path.Combine(project.WorkstationBackupsPath, sourceDirectory);
			if (!Directory.Exists(backupsPath))
				continue;

			var env = new Dictionary<string, string>
			{
				["DAPS_PROJECT"] = project.Definition.Name,
				["DAPS_BACKUPS_PATH"] = backupsPath.Replace('\\', '/'),
			};

			var json = _workstationBashRunner.CaptureScript(
				scriptHostPath,
				workingDirectory: project.Definition.Path,
				env: env);

			found.AddRange(ParseJson(json.Trim(), sourceDirectory));
		}

		// Indexes from the per-directory scans are discarded; the merged list is renumbered
		// most-recent-first so --restore-point <n> means the same thing across sources.
		return found
			.OrderByDescending(r => r.ParsedTimestamp)
			.Select((r, i) => r with { Index = i + 1 })
			.ToList();
	}

	private static IEnumerable<RestorePoint> ParseJson(string json, string sourceDirectory)
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
				SourceDirectory = sourceDirectory,
				Timestamp = timestamp,
				ParsedTimestamp = parsed,
				IsComplete = el.GetProperty("complete").GetBoolean(),
			});
		}

		return result;
	}
}
