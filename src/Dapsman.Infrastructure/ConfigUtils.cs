using System.Text.RegularExpressions;

namespace Dapsman.Infrastructure;

public static class ConfigUtils
{
	private static readonly Regex SafeProjectName = new("^[A-Za-z0-9._-]+$", RegexOptions.Compiled);

	/// <summary>
	/// Project names reach the remote host inside `rm -rf` and `docker compose -f`. A name read
	/// from daps.yaml is trusted, but prod teardown also accepts one that is not in daps.yaml at
	/// all, so the shape is enforced rather than assumed.
	/// </summary>
	public static string RequireSafeProjectName(string? name)
	{
		if (string.IsNullOrWhiteSpace(name))
			throw new ArgumentException("Project name is required.");

		// Excluded separately: both match the character class but are path traversal, not names.
		if (name is "." or "..")
			throw new ArgumentException($"Invalid project name '{name}'.");

		if (!SafeProjectName.IsMatch(name))
			throw new ArgumentException(
				$"Invalid project name '{name}'. Use only letters, digits, '.', '_' and '-'.");

		return name;
	}

	public static bool TryParseKeyValue(string line, out string key, out string value)
	{
		var split = line.Split(':', 2, StringSplitOptions.TrimEntries);
		if (split.Length != 2)
		{
			key = string.Empty;
			value = string.Empty;
			return false;
		}

		key = split[0];
		value = Unquote(split[1]);
		return true;
	}

	public static int CountLeadingSpaces(string input)
	{
		var count = 0;
		while (count < input.Length && input[count] == ' ')
		{
			count++;
		}

		return count;
	}

	public static string StripTrailingComment(string input)
	{
		var inSingle = false;
		var inDouble = false;

		for (var i = 0; i < input.Length; i++)
		{
			var ch = input[i];
			if (ch == '\'' && !inDouble)
			{
				inSingle = !inSingle;
				continue;
			}

			if (ch == '"' && !inSingle)
			{
				inDouble = !inDouble;
				continue;
			}

			if (ch == '#' && !inSingle && !inDouble)
			{
				return input[..i];
			}
		}

		return input;
	}

	public static string StripComment(string input)
	{
		var hashIndex = input.IndexOf('#');
		return hashIndex >= 0 ? input[..hashIndex] : input;
	}

	public static string Unquote(string input)
	{
		var value = input.Trim();
		if (value.Length >= 2)
		{
			var first = value[0];
			var last = value[^1];
			if ((first == '\'' && last == '\'') || (first == '"' && last == '"'))
			{
				return value[1..^1];
			}
		}

		return value;
	}

	public static string RequireFile(string path, string error)
	{
		var full = Path.GetFullPath(path);
		if (!File.Exists(full))
		{
			throw new FileNotFoundException(error, full);
		}

		return full;
	}

	public static IEnumerable<string> RequireFiles(IReadOnlyList<string> paths, string error)
	{
		foreach (var file in paths)
		{
			yield return RequireFile(file, error);
		}
	}

	public static string? FirstExisting(IEnumerable<string> candidates)
	{
		foreach (var candidate in candidates)
		{
			if (File.Exists(candidate))
			{
				return candidate;
			}
		}

		return null;
	}

	/// <summary>
	/// Copies a text file, rewriting CRLF line endings as LF.
	///
	/// Use this — rather than File.Copy — wherever a text file is being staged for a
	/// Linux destination: the toolkit container or a remote host. On Windows,
	/// core.autocrlf can leave CRLF endings in the working tree, and a CRLF shell
	/// script fails the moment it runs on Linux:
	///
	///     /tmp/provision-generic-vps.sh: line 1: set: pipefail: invalid option name
	///
	/// Git Bash tolerates CRLF, so the failure only ever appears on the far side.
	///
	/// Text only. Never call this on a binary file — Docker image tarballs in
	/// particular — since the rewrite would corrupt it.
	/// </summary>
	public static void CopyFileAndReplaceLineEndingsForLinux(string sourcePath, string destinationPath)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
		File.WriteAllText(destinationPath, File.ReadAllText(sourcePath).Replace("\r\n", "\n"));
	}

	public static string ResolvePath(string root, string path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return path;
		}

		return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(root, path));
	}

	public static string GetDockerCommandPrefix(string username)
	{
		return string.Equals(username, "root", StringComparison.OrdinalIgnoreCase) ? "docker" : "sudo docker";
	}
}