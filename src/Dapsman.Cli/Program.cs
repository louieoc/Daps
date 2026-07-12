return await ProgramEntry.RunAsync(args);

internal static class ProgramEntry
{
	public static async Task<int> RunAsync(string[] args)
	{
		try
		{
			var parsed = CliArguments.Parse(args);
			var runner = new DapsmanRunner(parsed);
			runner.ResolveDependencies();
			return await runner.RunAsync();
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"ERROR: {ex.Message}");
			return 1;
		}
	}
}
