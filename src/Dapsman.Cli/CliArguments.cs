internal sealed class CliArguments
{
	public required bool IsInit { get; init; }
	public required bool IsLocalBuild { get; init; }
	public required bool IsLocalCaddyRestart { get; init; }
	public required bool IsLocalRestore { get; init; }
	public required bool IsLocalTeardown { get; init; }
	public required bool ListRestorePoints { get; init; }
	public string? SelectedRestorePoint { get; init; }
	public string? ProdUrl { get; init; }
	public required bool IsProdCaddyRestart { get; init; }
	public required bool IsProdProvision { get; init; }
	public required bool IsProdDeploy { get; init; }
	public required bool IsProdSyncFromLocal { get; init; }
	public required bool IsLocalSyncFromProd { get; init; }
	public required bool IsProdBackup { get; init; }
	public required bool IsProdOffline { get; init; }
	public required bool IsProdOnline { get; init; }
	public required bool IsProdTeardown { get; init; }
	public required bool IsProdUnprovision { get; init; }
	public required bool BuildImages { get; init; }
	public required bool Rebuild { get; init; }
	public required bool AssumeYes { get; init; }
	public required bool DryRun { get; init; }
	public required string ConfigPath { get; init; }
	public required IReadOnlyList<string> ProjectFilters { get; init; }
	public string? TemplateName { get; init; }
	public string? ProjectName { get; init; }
	public string? DestinationPath { get; init; }
	public bool Overlay { get; init; }
	public string? ProviderName { get; init; }
	public string? SetVarsScriptPath { get; init; }
	public string? CreateScriptPath { get; init; }
	public bool Upgrade { get; init; }

	public static CliArguments Parse(string[] args)
	{
		var tokens = new Queue<string>(args);
		var projectFilters = new List<string>();

		var isInit = false;
		var isLocalBuild = false;
		var isLocalCaddyRestart = false;
		var isLocalRestore = false;
		var isLocalTeardown = false;
		var listRestorePoints = false;
		string? selectedRestorePoint = null;
		string? prodUrl = null;
		var isProdCaddyRestart = false;
		var isProdProvision = false;
		var isProdDeploy = false;
		var isProdSyncFromLocal = false;
		var isLocalSyncFromProd = false;
		var isProdBackup = false;
		var isProdOffline = false;
		var isProdOnline = false;
		var isProdTeardown = false;
		var isProdUnprovision = false;
		var buildImages = false;
		var rebuild = false;
		var assumeYes = false;
		var dryRun = false;
		var overlay = false;
		var configPath = Path.Combine(Environment.CurrentDirectory, "daps.yaml");
		string? templateName = null;
		string? projectName = null;
		string? destinationPath = null;
		string? providerName = null;
		string? setVarsScriptPath = null;
		string? createScriptPath = null;
		var upgrade = false;

		if (tokens.Count >= 1)
		{
			var first = tokens.Peek();

			if (string.Equals(first, "init", StringComparison.OrdinalIgnoreCase))
			{
				tokens.Dequeue();
				isInit = true;
			}
			else if (tokens.Count >= 2)
			{
				var group = tokens.Dequeue();
				var action = tokens.Dequeue();

				if (string.Equals(group, "local", StringComparison.OrdinalIgnoreCase) &&
					string.Equals(action, "build", StringComparison.OrdinalIgnoreCase))
				{
					isLocalBuild = true;
				}
				else if (string.Equals(group, "local", StringComparison.OrdinalIgnoreCase) &&
						 string.Equals(action, "caddy", StringComparison.OrdinalIgnoreCase) &&
						 tokens.Count > 0 &&
						 string.Equals(tokens.Peek(), "restart", StringComparison.OrdinalIgnoreCase))
				{
					tokens.Dequeue(); // consume "restart"
					isLocalCaddyRestart = true;
				}
				else if (string.Equals(group, "local", StringComparison.OrdinalIgnoreCase) &&
						 string.Equals(action, "restore", StringComparison.OrdinalIgnoreCase))
				{
					isLocalRestore = true;
				}
				else if (string.Equals(group, "local", StringComparison.OrdinalIgnoreCase) &&
						 string.Equals(action, "teardown", StringComparison.OrdinalIgnoreCase))
				{
					isLocalTeardown = true;
				}
				else if (string.Equals(group, "prod", StringComparison.OrdinalIgnoreCase) &&
						 string.Equals(action, "caddy", StringComparison.OrdinalIgnoreCase) &&
						 tokens.Count > 0 &&
						 string.Equals(tokens.Peek(), "restart", StringComparison.OrdinalIgnoreCase))
				{
					tokens.Dequeue(); // consume "restart"
					isProdCaddyRestart = true;
				}
				else if (string.Equals(group, "prod", StringComparison.OrdinalIgnoreCase) &&
						 string.Equals(action, "provision", StringComparison.OrdinalIgnoreCase))
				{
					isProdProvision = true;
				}
				else if (string.Equals(group, "prod", StringComparison.OrdinalIgnoreCase) &&
						 string.Equals(action, "deploy", StringComparison.OrdinalIgnoreCase))
				{
					isProdDeploy = true;
				}
				else if (string.Equals(group, "prod", StringComparison.OrdinalIgnoreCase) &&
						 string.Equals(action, "sync-from-local", StringComparison.OrdinalIgnoreCase))
				{
					isProdSyncFromLocal = true;
				}
				else if (string.Equals(group, "local", StringComparison.OrdinalIgnoreCase) &&
						 string.Equals(action, "sync-from-prod", StringComparison.OrdinalIgnoreCase))
				{
					isLocalSyncFromProd = true;
				}
				else if (string.Equals(group, "prod", StringComparison.OrdinalIgnoreCase) &&
						 string.Equals(action, "backup", StringComparison.OrdinalIgnoreCase))
				{
					isProdBackup = true;
				}
				else if (string.Equals(group, "prod", StringComparison.OrdinalIgnoreCase) &&
						 string.Equals(action, "offline", StringComparison.OrdinalIgnoreCase))
				{
					isProdOffline = true;
				}
				else if (string.Equals(group, "prod", StringComparison.OrdinalIgnoreCase) &&
						 string.Equals(action, "online", StringComparison.OrdinalIgnoreCase))
				{
					isProdOnline = true;
				}
				else if (string.Equals(group, "prod", StringComparison.OrdinalIgnoreCase) &&
						 string.Equals(action, "teardown", StringComparison.OrdinalIgnoreCase))
				{
					isProdTeardown = true;
				}
				else if (string.Equals(group, "prod", StringComparison.OrdinalIgnoreCase) &&
						 string.Equals(action, "unprovision", StringComparison.OrdinalIgnoreCase))
				{
					isProdUnprovision = true;
				}
			}
		}

		while (tokens.Count > 0)
		{
			var token = tokens.Dequeue();
			switch (token)
			{
				case "--build":
					buildImages = true;
					break;
				case "--rebuild":
					// --rebuild implies --build: the containers are being recreated from
					// scratch, so the images should be rebuilt too.
					rebuild = true;
					buildImages = true;
					break;
				case "--yes":
					assumeYes = true;
					break;
				case "--dry-run":
					dryRun = true;
					break;
				case "--overlay":
					overlay = true;
					break;
				case "--upgrade":
					upgrade = true;
					break;
				case "--list-restore-points":
					listRestorePoints = true;
					break;
				case "--restore-point":
					if (tokens.Count == 0)
						throw new ArgumentException("Missing value for --restore-point.");
					selectedRestorePoint = tokens.Dequeue();
					break;
				case "--prod-url":
					if (tokens.Count == 0)
						throw new ArgumentException("Missing value for --prod-url.");
					prodUrl = tokens.Dequeue();
					break;
				case "--project":
					if (tokens.Count == 0)
					{
						throw new ArgumentException("Missing value for --project.");
					}
					projectFilters.Add(tokens.Dequeue());
					break;
				case "--config":
					if (tokens.Count == 0)
					{
						throw new ArgumentException("Missing value for --config.");
					}
					configPath = tokens.Dequeue();
					break;
				case "--provider":
					if (tokens.Count == 0)
					{
						throw new ArgumentException("Missing value for --provider.");
					}
					providerName = tokens.Dequeue();
					break;
				case "--set-vars-script":
					if (tokens.Count == 0)
					{
						throw new ArgumentException("Missing value for --set-vars-script.");
					}
					setVarsScriptPath = tokens.Dequeue();
					break;
				case "--create-script":
					if (tokens.Count == 0)
					{
						throw new ArgumentException("Missing value for --create-script.");
					}
					createScriptPath = tokens.Dequeue();
					break;
				case "--template":
					if (tokens.Count == 0)
					{
						throw new ArgumentException("Missing value for --template.");
					}
					templateName = tokens.Dequeue();
					break;
				case "--name":
					if (tokens.Count == 0)
					{
						throw new ArgumentException("Missing value for --name.");
					}
					projectName = tokens.Dequeue();
					break;
				case "--path":
					if (tokens.Count == 0)
					{
						throw new ArgumentException("Missing value for --path.");
					}
					destinationPath = tokens.Dequeue();
					break;
				default:
					throw new ArgumentException($"Unknown argument: {token}");
			}
		}

		return new CliArguments
		{
			IsInit = isInit,
			IsLocalBuild = isLocalBuild,
			IsLocalCaddyRestart = isLocalCaddyRestart,
			IsLocalRestore = isLocalRestore,
			IsLocalTeardown = isLocalTeardown,
			ListRestorePoints = listRestorePoints,
			SelectedRestorePoint = selectedRestorePoint,
			ProdUrl = prodUrl,
			IsProdCaddyRestart = isProdCaddyRestart,
			IsProdProvision = isProdProvision,
			IsProdDeploy = isProdDeploy,
			IsProdSyncFromLocal = isProdSyncFromLocal,
			IsLocalSyncFromProd = isLocalSyncFromProd,
			IsProdBackup = isProdBackup,
			IsProdOffline = isProdOffline,
			IsProdOnline = isProdOnline,
			IsProdTeardown = isProdTeardown,
			IsProdUnprovision = isProdUnprovision,
			BuildImages = buildImages,
			Rebuild = rebuild,
			AssumeYes = assumeYes,
			DryRun = dryRun,
			ConfigPath = configPath,
			ProjectFilters = projectFilters,
			TemplateName = templateName,
			ProjectName = projectName,
			DestinationPath = destinationPath,
			Overlay = overlay,
			ProviderName = providerName,
			SetVarsScriptPath = setVarsScriptPath,
			CreateScriptPath = createScriptPath,
			Upgrade = upgrade,
		};
	}
}
