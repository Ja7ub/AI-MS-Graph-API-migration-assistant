if (args.Length < 1)
{
    Console.WriteLine("Usage: GraphMigrationBot <repository-path> [documentation-url]");
    return;
}

string repoPath = args[0];

string documentationUrl = args.Length > 1 ? args[1] : "https://learn.microsoft.com/en-us/graph/migrate-azure-ad-graph-planning-checklist";

var migrationBot = new GraphMigrationBot();

Console.WriteLine($"Scanning repository: {repoPath}");
List<string> findings = migrationBot.ScanRepository(repoPath);
//List<string> findings = [" - File: C:\\Users\\jhavlicek\\source\\repos\\appworkflowservice-master-AHS\\sync_pstn_avs-appworkflowservice\\Oaa\\UnitTests\\CallController\\DialScopeUserSearchResultFilterTests.cs, Line: 79, Code: _aadTokenProvider.Setup(x => x.GraphResourceUri).Returns(\"https://graph.windows.net/\")"];

if (findings.Count == 0)
{
    Console.WriteLine("No deprecated MS Graph usage found.");
    return;
}

Console.WriteLine("\nIdentified potential migration points:");
foreach (var finding in findings)
{
    Console.WriteLine($" - {finding}");
}

Console.WriteLine("\nGenerating migration suggestions...");
await migrationBot.GenerateMigrationSuggestions(findings, documentationUrl);