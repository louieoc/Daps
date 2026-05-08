namespace Dapsman.Infrastructure;

public static class ConfigUtils
{
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

	public static string ResolvePath(string root, string path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return path;
		}

		return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(root, path));
	}
}